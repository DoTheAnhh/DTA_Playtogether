using DTA.Game.Geometry;
using DTA.Game.Session;
using DTA.Runtime.Core;

namespace DTA.Features.Excavation;

/// <summary>
/// Đọc game riêng của việc đào: điểm đào (sysCollect._excavationManagerDic), trạng thái đào, độ vươn xẻng, bảng loại cổ vật, nút nhận quà.
/// Hòn đảo bị mất ở <see cref="ExcavationGame"/>.Island.cs.
/// </summary>
public sealed partial class ExcavationGame(GameSession session)
{
    public const string KindTable = "TableExcavationObjectListImpl", AssetPrefix = "spawn_excavation_";
    public const float DefaultOffset = 0.5f;
    private static readonly string[] SpotFields = ["MaxHP", "CurrentHP", "SpawnObjectId", "_digRangeSqr", "ObjectUid", "m_CachedPtr"];
    private static readonly string[] ClaimFields = ["_headUpSelectButton", "_reqReward"];
    private static readonly string[] KindColumns = ["ExcavationObjectId", "AssetName", "ExcavationObjectHp", "ExcavationObjectRange", "RewardType", "IsSync"];
    private static readonly Dictionary<string, string> KindNames = new()
    {
        ["can"] = "Lon cũ", ["strange_pot"] = "Bình kỳ lạ", ["box_hieroglyph"] = "Hộp chữ tượng hình", ["broken_pot"] = "Bình vỡ",
        ["chest"] = "Rương kho báu", ["chest_desert"] = "Rương sa mạc", ["pyramid"] = "Kim tự tháp", ["sphinx"] = "Tượng Nhân sư",
        ["tutankhamen"] = "Mặt nạ Tutankhamun", ["moai"] = "Tượng Moai", ["goryeo_celadon"] = "Bình gốm Cao Ly",
        ["gold_mannequin"] = "Ma-nơ-canh vàng", ["strange_haechi"] = "Tượng Haechi", ["golden_throne"] = "Ngai vàng",
        ["sarcophagus"] = "Quan tài đá", ["jangseung_female"] = "Cột Jangseung nữ", ["jangseung_male"] = "Cột Jangseung nam",
    };

    private int[]? _spotLayout;
    private readonly Dictionary<(int, long), Vec3> _positions = [];
    private float? _offset;
    private long _controlClass;

    public GameSession Session { get; } = session;
    /// <summary>Loại cổ vật theo mã (nhớ theo phiên bản game).</summary>
    public Dictionary<int, RelicKind> Kinds { get; private set; } = [];

    /// <summary>Nạp bảng loại cổ vật (bộ đệm phiên bản; chưa có thì đọc bảng game).</summary>
    public void LoadKinds()
    {
        var saved = Session.Cache.Get<Dictionary<int, RelicKind>>("relics");
        if (saved is not { Count: > 0 })
        {
            saved = Session.Optional(ReadKinds, "bảng cổ vật") ?? [];
            if (saved.Count > 0) Session.Cache.Put("relics", saved);
        }
        Kinds = saved;
    }

    /// <summary>Bảng loại cổ vật của game (RewardType 1 = đào xong tự phát quà).</summary>
    private Dictionary<int, RelicKind>? ReadKinds()
    {
        var rows = Session.Tables.Rows(KindTable).Select(r => r.Row).ToList();
        if (rows.Count == 0) return null;
        var klass = Session.Managed.ClassOf(rows[0]);
        var at = KindColumns.Select(c => Session.Il2Cpp.Field(klass, c)).ToArray();
        var data = Session.Memory.ReadObjects(rows, at.Max() + 8);
        var names = Session.Memory.Strings(data.Values.Select(d => Bin.U64(d, at[1])), 80);
        return data.Values.ToDictionary(d => Bin.I32(d, at[0]), d => new RelicKind(names.GetValueOrDefault(Bin.U64(d, at[1]), ""), Bin.I32(d, at[2]),
            MathF.Round(Bin.F32(d, at[3]), 2), Bin.I32(d, at[4]) == 1, d[at[5]] != 0));
    }

    /// <summary>Tên hiển thị loại cổ vật.</summary>
    public string KindName(int kind)
    {
        if (IsIsland) return IslandKindName(kind);
        if (!Kinds.TryGetValue(kind, out var info)) return $"Cổ vật loại {kind}";
        var shortName = info.Asset.StartsWith(AssetPrefix) ? info.Asset[AssetPrefix.Length..] : info.Asset;
        return KindNames.GetValueOrDefault(shortName) ?? (shortName.Length > 0 ? char.ToUpper(shortName[0]) + shortName[1..].Replace('_', ' ') : "Cổ vật");
    }

    /// <summary>Loại đã lộ có phải rương (đảo: loại ≥ 2; thường: tên có chest / treasure / box).</summary>
    public bool IsChest(int kind) => kind != 0 && (IsIsland ? kind >= 2 : Kinds.TryGetValue(kind, out var info) && new[] { "chest", "treasure", "box" }.Any(info.Asset.ToLowerInvariant().Contains));

    /// <summary>Đổi object điều khiển: kiểm kiểu, quên dữ liệu theo cảnh.</summary>
    private void CheckControl()
    {
        if (Session.ControlClass == 0) Session.LocateActor();
        if (Session.ControlClass == _controlClass) return;
        _controlClass = Session.ControlClass;
        _island = null;
        if (!Session.Il2Cpp.Is(_controlClass, "ActorDefaultControl") && !Session.Il2Cpp.Is(_controlClass, "ActorTreasureHuntPlayer"))
            throw new GameError("Nhân vật đang ở trạng thái không đào được (đang lái xe?)");
    }

    /// <summary>Trạng thái đào (1 lượt đọc). Đảo: isDigging / IsDigFail.</summary>
    public ExcavateState State()
    {
        CheckControl();
        if (!IsIsland) return (ExcavateState)Session.ControlValue(Session.Il2Cpp.Field(_controlClass, "ExcavateState"));
        var digAt = Session.Il2Cpp.Field(_controlClass, "isDigging");
        var failAt = Session.Il2Cpp.Field(_controlClass, "IsDigFail");
        var data = Session.Memory.ReadMany([(Session.Control + digAt, 1), (Session.Control + failAt, 1)]);
        return data[1] is [1] ? ExcavateState.Miss : data[0] is [1] ? ExcavateState.Digging : ExcavateState.None;
    }

    /// <summary>Xẻng chạm đất trước mặt bao nhiêu mét (ForwardOffset của xẻng đang cầm; đảo 0,5).</summary>
    public float ShovelOffset()
    {
        if (IsIsland) return DefaultOffset;
        if (_offset is { } known) return known;
        var tool = Session.Tools.HeldTool();
        var klass = Session.Managed.ClassOf(tool);
        var offset = klass != 0 && Session.Il2Cpp.FullName(klass) == DTA.Game.Data.Tools.Shovel ? Session.Managed.F32(tool, "ForwardOffset") : null;
        if (offset is not { } o) return DefaultOffset;
        _offset = o is >= 0 and <= 3 ? o : DefaultOffset;
        return _offset.Value;
    }

    /// <summary>Điểm đào đang có: {uid: Spot}. Object điểm đào đặt đúng chỗ đào nên vị trí object = chỗ cần đứng (đọc 1 lần / điểm).</summary>
    public Dictionary<int, Spot> Spots()
    {
        if (IsIsland) return IslandSpots();
        var collect = Session.System("sysCollect");
        var entries = collect != 0 ? Session.Managed.DictItems(Session.Managed.Ptr(collect, "_excavationManagerDic")) : [];
        if (entries.Count == 0)
        {
            _positions.Clear();
            return [];
        }
        if (_spotLayout == null)
        {
            var klass = Session.Managed.ClassOf(entries[0].Value);
            _spotLayout = [.. SpotFields.Select(f => Session.Il2Cpp.Field(klass, f)), .. ClaimFields.Select(f => Session.Il2Cpp.HasField(klass, f) ? Session.Il2Cpp.Field(klass, f) : 0)];
        }
        var data = Session.Memory.ReadObjects(entries.Select(e => e.Value), _spotLayout.Max() + 8);
        var missing = entries.Where(e => data.ContainsKey(e.Value) && !_positions.ContainsKey(((int)e.Key, e.Value))).Select(e => e.Value).ToList();
        var fresh = Session.World.Poses(missing);
        var spots = new Dictionary<int, Spot>();
        var positions = new Dictionary<(int, long), Vec3>();
        foreach (var (key, manager) in entries)
        {
            var uid = (int)key;
            if (!data.TryGetValue(manager, out var raw)) continue;
            if (!_positions.TryGetValue((uid, manager), out var position))
            {
                if (!fresh.TryGetValue(manager, out var pose)) continue;
                position = pose.Position;
            }
            positions[(uid, manager)] = position;
            spots[uid] = Build(uid, manager, position, raw);
        }
        _positions.Clear();
        foreach (var p in positions) _positions[p.Key] = p.Value;
        return spots;
    }

    /// <summary>Spot từ dữ liệu thô; tầm đào chưa tính (điểm vừa lộ) thì lấy theo bảng loại cổ vật.</summary>
    private Spot Build(int uid, long manager, Vec3 position, byte[] raw)
    {
        var l = _spotLayout!;
        var kind = Bin.I32(raw, l[2]);
        var reach = MathF.Sqrt(Math.Max(Bin.F32(raw, l[3]), 0));
        if (kind != 0 && reach < 0.1f && Kinds.TryGetValue(kind, out var info)) reach = info.Reach;
        return new Spot(uid, manager, position.X, position.Z, kind, Bin.I32(raw, l[1]), Bin.I32(raw, l[0]), reach, position.Y,
            l[6] != 0 ? Bin.U64(raw, l[6]) : 0, l[7] != 0 && raw[l[7]] != 0);
    }

    /// <summary>
    /// Đọc lại 1 điểm đã biết (1 lượt đọc). Null nếu object bị huỷ / dùng cho điểm khác. Điểm vừa đào xong vẫn đọc được thêm 1 lúc (HP 0).
    /// </summary>
    public Spot? Reload(Spot spot)
    {
        if (IsIsland) return IslandReload(spot);
        if (_spotLayout is not { } l) return null;
        var raw = Session.Memory.Read(spot.Ref, l.Max() + 8);
        if (raw == null || Bin.U64(raw, l[5]) == 0 || Bin.U32(raw, l[4]) != (uint)spot.Uid) return null;
        return Build(spot.Uid, spot.Ref, new Vec3(spot.X, spot.Y, spot.Z), raw);
    }

    /// <summary>Nút nhận quà (HeadUpSelectButton / HeadUpBoxOpen trên đảo) đang có; 0 nếu chưa.</summary>
    public long ClaimButton(Spot spot) => IsIsland ? ActiveTreasure(spot)?.Hub ?? 0 : spot.Button;

    /// <summary>Điểm màn hình nút nhận quà trên đầu cổ vật đã đào xong (nút có camera riêng); null nếu chưa hiện.</summary>
    public DTA.Game.Ui.ScreenPoint? ClaimPoint(Spot spot)
    {
        if (IsIsland) return IslandClaimPoint(spot);
        if (spot.Button == 0) return null;
        return Session.Optional<DTA.Game.Ui.ScreenPoint?>(() =>
        {
            var m = Session.Managed;
            long button = m.Ptr(spot.Button, "selectButton"), camera = m.Ptr(spot.Button, "uiCamera");
            return button != 0 && camera != 0 ? Session.World.ComponentPoint(button, camera) : null;
        }, "nút nhận quà");
    }

    /// <summary>Gửi yêu cầu nhận quà cổ vật: CollectSystem.RequestExcvationReward(uid) - đúng hàm game gọi khi bấm bong bóng.</summary>
    public bool RequestReward(int uid)
    {
        var collect = Session.System("sysCollect");
        return uid > 0 && DTA.Runtime.Core.Bin.IsPtr(collect) && Session.Invoker.Call(DTA.Game.Invoke.Fn.RequestExcavationReward, collect, [(ulong)uid]);
    }
}
