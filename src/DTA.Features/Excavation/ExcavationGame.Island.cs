using DTA.Game.Geometry;
using DTA.Game.Ui;
using DTA.Runtime.Core;

namespace DTA.Features.Excavation;

/// <summary>Rương / đồ đang trồi lên trên đảo: loại, đã có quà, vị trí, nút mở (HeadUpBoxOpen), độ bền còn / tối đa (-1 = không đọc được).</summary>
public sealed record Treasure(int Type, bool Reward, Vec3 Position, long Hub, int Durability, int MaxDurability);

/// <summary>Phần Hòn đảo bị mất của <see cref="ExcavationGame"/>.</summary>
public sealed partial class ExcavationGame
{
    public const float IslandReach = 1.2f;
    private const float ChestOther = 1.5f, ChestMine = 2.5f;
    private static readonly TimeSpan IslandPositionsTtl = TimeSpan.FromSeconds(1), ChestPositionsTtl = TimeSpan.FromSeconds(0.5);
    private static readonly string[] TreasureFields = ["currentTypeIndex", "_durability", "_durabilityMax", "isReward", "headUpBoxOpen"];
    private static readonly Dictionary<int, string> IslandNames = new()
    {
        [1] = "Đồ thường", [2] = "Rương kho báu thường", [3] = "Rương kho báu đồng", [4] = "Rương kho báu bạc", [5] = "Rương kho báu vàng", [6] = "Rương kho báu kim cương",
    };

    private bool? _island;
    private readonly Dictionary<long, Vec3> _islandPositions = [], _chestPositions = [];
    private DateTime _islandAt, _chestAt;

    /// <summary>Đang ở Hòn đảo bị mất (đọc bản đồ 1 lần, nhớ tới khi đổi kiểu điều khiển).</summary>
    public bool IsIsland
    {
        get
        {
            if (_island is { } known) return known;
            if (Session.ControlClass != 0 && Session.Il2Cpp.Is(Session.ControlClass, "ActorTreasureHuntPlayer")) return (_island = true).Value;
            if (Session.Camera.Map() is not { } map) return false;
            _island = map.Id == 21001 || map.Name.Contains("treasure", StringComparison.OrdinalIgnoreCase) || map.Name.Contains("lost", StringComparison.OrdinalIgnoreCase);
            return _island.Value;
        }
    }

    /// <summary>Quên dữ liệu theo cảnh (đổi bản đồ).</summary>
    public void SceneChanged()
    {
        _positions.Clear();
        _islandPositions.Clear();
        _chestPositions.Clear();
        _island = null;
    }

    private static string IslandKindName(int kind) => IslandNames.GetValueOrDefault(kind) ?? (kind >= 2 ? $"Rương đảo loại {kind}" : "Điểm đào đảo");

    private long TreasureManager() => Session.Managed.Ptr(Session.Control, "treasureManager");

    /// <summary>Loại rương / đồ đang đào (diggingBoxType); khác điểm <paramref name="spotUid"/> (diggingBoxUid) thì 0.</summary>
    public int DiggingBoxType(int spotUid = 0)
    {
        if (!IsIsland) return 0;
        var m = Session.Managed;
        if (spotUid != 0 && m.I32(Session.Control, "diggingBoxUid") is { } uid and not 0 && uid != spotUid) return 0;
        return m.I32(Session.Control, "diggingBoxType") ?? 0;
    }

    /// <summary>
    /// Điểm đào trên đảo (treasureManager._treasureCommonObjects): chưa tạo vật (AlreadyMakeTreasure 0) và đang hiện. Game dùng lại vật thể
    /// khi điểm mọc chỗ khác nên đọc lại cả loạt vị trí mỗi giây (vật mới thấy thì đọc ngay).
    /// </summary>
    private Dictionary<int, Spot> IslandSpots()
    {
        var m = Session.Managed;
        var entries = m.DictItems(m.Ptr(TreasureManager(), "_treasureCommonObjects"));
        if (entries.Count == 0) return [];
        var klass = m.ClassOf(entries[0].Value);
        int goAt = Session.Il2Cpp.Field(klass, "CommonGameObject"), madeAt = Session.Il2Cpp.Field(klass, "AlreadyMakeTreasure");
        var raw = Session.Memory.ReadObjects(entries.Select(e => e.Value), Math.Max(goAt + 8, madeAt + 1));
        var active = entries.Where(e => raw.TryGetValue(e.Value, out var r) && r[madeAt] == 0 && Bin.U64(r, goAt) != 0)
            .Select(e => (Uid: (int)e.Key, Obj: e.Value, Go: Bin.U64(raw[e.Value], goAt))).ToList();
        var shown = Session.World.ShownFlags(active.Select(a => a.Go).ToList());
        active = active.Where(a => shown.GetValueOrDefault(a.Go, true)).ToList();
        var stale = DateTime.UtcNow - _islandAt > IslandPositionsTtl;
        var wanted = active.Select(a => a.Go).Where(g => stale || !_islandPositions.ContainsKey(g)).ToList();
        if (wanted.Count > 0)
        {
            var fresh = Session.World.Poses(wanted);
            if (stale)
            {
                _islandAt = DateTime.UtcNow;
                _islandPositions.Clear();
            }
            foreach (var (go, pose) in fresh) _islandPositions[go] = pose.Position;
        }
        return active.Where(a => _islandPositions.TryGetValue(a.Go, out var p) && p != Vec3.Zero)
            .ToDictionary(a => a.Uid, a => IslandSpot(a.Uid, a.Obj, _islandPositions[a.Go]));
    }

    private static Spot IslandSpot(int uid, long obj, Vec3 p) => new(uid, obj, p.X, p.Z, 0, 1, 1, IslandReach, p.Y);


    /// <summary>Điểm đảo còn chờ đào: object còn gắn vật và AlreadyMakeTreasure = 0 (1 lượt đọc - hỏi được ở mọi nhịp khi chạy tới).</summary>
    public bool IslandAlive(Spot spot)
    {
        var klass = Session.Managed.ClassOf(spot.Ref);
        if (klass == 0) return false;
        int goAt = Session.Il2Cpp.Field(klass, "CommonGameObject"), madeAt = Session.Il2Cpp.Field(klass, "AlreadyMakeTreasure");
        var raw = Session.Memory.Read(spot.Ref, Math.Max(goAt + 8, madeAt + 1));
        return raw != null && Bin.U64(raw, goAt) != 0 && raw[madeAt] == 0;
    }

    /// <summary>
    /// Đọc lại điểm đảo: nhát đầu trúng thì game tạo vật (rương / đá trồi lên) - còn vật thì đang đào tiếp; không còn vật = xong (quà về
    /// túi). Độ bền rương đóng vai HP; có quà thì HP 0.
    /// </summary>
    private Spot? IslandReload(Spot spot)
    {
        var klass = Session.Managed.ClassOf(spot.Ref);
        var made = klass != 0 && Session.Memory.Read(spot.Ref + Session.Il2Cpp.Field(klass, "AlreadyMakeTreasure"), 1) is [not 0];
        var act = ActiveTreasure(spot);
        if (made && act == null) return null;
        var kind = act is { Type: > 0 } ? act.Type : DiggingBoxType(spot.Uid);
        int hp, max;
        if (act is { Reward: true }) (kind, hp, max) = (Math.Max(kind, 1), 0, Math.Max(act.MaxDurability, 1));
        else (hp, max) = act is { MaxDurability: > 0 } ? (Math.Max(act.Durability, 0), act.MaxDurability) : (1, 1);
        return spot with { Kind = kind, Hp = hp, MaxHp = max, Button = act?.Hub ?? 0, Claiming = false };
    }

    /// <summary>
    /// Rương đang trồi lên gần điểm (của mình: &lt;ActiveTreasure&gt; trong 2,5 m; người khác: chỉ khi sát tâm ≤ 1,5 m). Vị trí đọc theo lô,
    /// nhớ 0,5 s (đảo đông người có rất nhiều rương). Chỉ tin cờ isReward (con trỏ headUpBoxOpen có sẵn từ lúc trồi lên).
    /// </summary>
    public Treasure? ActiveTreasure(Spot? spot = null) => !IsIsland ? null : Session.Optional(() =>
    {
        var m = Session.Managed;
        var manager = TreasureManager();
        var reference = spot != null ? new Vec3(spot.X, spot.Y, spot.Z) : Session.Player.Position();
        if (manager == 0 || reference is not { } at) return null;
        var current = m.Ptr(manager, "ActiveTreasure");
        var items = m.ListItems(m.Ptr(manager, "activeTreasureList")).Prepend(current).Where(i => i != 0).Distinct().ToList();
        if (DateTime.UtcNow - _chestAt > ChestPositionsTtl)
        {
            _chestAt = DateTime.UtcNow;
            _chestPositions.Clear();
        }
        foreach (var (item, pose) in Session.World.Poses(items.Where(i => !_chestPositions.ContainsKey(i)).ToList())) _chestPositions[item] = pose.Position;
        var near = items.Where(i => _chestPositions.TryGetValue(i, out var p) && p != Vec3.Zero)
            .Select(i => (Dist: _chestPositions[i].FlatDistance(at), Item: i)).ToList();
        var mine = near.Where(n => n.Item == current && n.Dist <= ChestMine).ToList();
        var pick = (mine.Count > 0 ? mine : near.Where(n => n.Dist <= ChestOther)).OrderBy(n => n.Dist).FirstOrDefault();
        if (pick.Item == 0) return null;
        var klass = m.ClassOf(pick.Item);
        var offsets = TreasureFields.Where(f => Session.Il2Cpp.HasField(klass, f)).ToDictionary(f => f, f => Session.Il2Cpp.Field(klass, f));
        var raw = offsets.Count > 0 ? Session.Memory.Read(pick.Item, offsets.Values.Max() + 8) : null;
        int Int(string f, int fallback) => raw != null && offsets.TryGetValue(f, out var o) ? Bin.I32(raw, o) : fallback;
        var type = Int("currentTypeIndex", 0);
        if (type <= 0) type = DiggingBoxType(spot?.Uid ?? 0);
        var hub = raw != null && offsets.TryGetValue("headUpBoxOpen", out var h) ? Bin.U64(raw, h) : 0;
        var reward = raw != null && offsets.TryGetValue("isReward", out var r) && raw[r] != 0;
        return new Treasure(type, reward, _chestPositions[pick.Item], hub, Int("_durability", -1), Int("_durabilityMax", -1));
    }, "rương đảo");

    /// <summary>Điểm màn hình nút nhận quà trên đầu rương đảo: nút trong HeadUpBoxOpen (nhanh), không thì bong bóng sysHud trên đầu rương.</summary>
    private ScreenPoint? IslandClaimPoint(Spot spot) => Session.Optional<ScreenPoint?>(() =>
    {
        if (ActiveTreasure(spot) is not { } act) return null;
        var m = Session.Managed;
        if (act.Hub != 0 && Session.Il2Cpp.HasField(m.ClassOf(act.Hub), "selectButton"))
        {
            long button = m.Ptr(act.Hub, "selectButton"), camera = m.Ptr(act.Hub, "uiCamera");
            if (button != 0 && camera != 0 && Session.World.ComponentPoint(button, camera) is { } p) return p;
        }
        return Session.HeadUps.Above(act.Position with { Y = act.Position.Y + 1.2f });
    }, "nút nhận quà đảo");

    /// <summary>Nút mở cửa hàng xẻng trên đảo (DialogMiniGame.equipItemSlot.slotList[0].button); 0 nếu không có.</summary>
    public long ShopButton() => Session.Optional(() =>
    {
        var m = Session.Managed;
        var slot = m.Ptr(Session.Ui.Screens().GetValueOrDefault("DialogMiniGame"), "equipItemSlot");
        var items = slot != 0 && Session.Il2Cpp.HasField(m.ClassOf(slot), "slotList") ? m.ListItems(m.Ptr(slot, "slotList")) : [];
        if (items.Count == 0) return 0L;
        return Session.Il2Cpp.HasField(m.ClassOf(items[0]), "button") ? m.Ptr(items[0], "button") : items[0];
    }, "nút cửa hàng xẻng");
}
