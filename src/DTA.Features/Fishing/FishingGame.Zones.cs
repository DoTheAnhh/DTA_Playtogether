using DTA.Game.Data;
using DTA.Game.World;
using DTA.Runtime.Core;

namespace DTA.Features.Fishing;

/// <summary>Phần vùng câu của <see cref="FishingGame"/>: vùng đặc biệt trên bản đồ, khoảng quăng, ID vùng chỗ phao rơi, giả vùng câu.</summary>
public sealed partial class FishingGame
{
    private static readonly Logger L = Log.For("fishing");
    private readonly FakeZoneLedger _fake = new();
    private List<(long Info, uint Id)>? _mapZones;
    private long _infoClass, _zoneClass;

    /// <summary>Vùng câu đặc biệt (vật thể bản đồ tên chứa "fishingzone") + tâm vùng.</summary>
    public List<ZoneSpot> ZoneSpots() => Session.Map.Things()
        .Where(t => t.Asset.Contains("fishingzone") && !MapObjects.GoneStates.Contains(t.State))
        .Select(t => new ZoneSpot(t.Asset, t.X, t.Y, t.Z)).ToList();

    /// <summary>Khoảng phao rơi trước mặt khi quăng (FishingSystem.CastingDistance, m); null nếu không đọc được.</summary>
    public float? CastingDistance() => Session.Optional<float?>(() =>
        Session.Managed.F32(Session.System("sysFishing"), "CastingDistance") is { } v and > 0.5f and < 60 ? v : null, "khoảng quăng");

    /// <summary>
    /// Vùng câu quanh chỗ phao vừa rơi: {FishingZoneInfo: ID}. Lúc quăng game dò collider vùng nước vào FishingSystem.FishingColliderCache; mỗi
    /// collider nằm trên GameObject có FishingZone giữ FishingZoneList - danh sách ID game gửi máy chủ khi quăng (FishingCastingQ).
    /// </summary>
    public Dictionary<long, uint> CastZones() => Session.Optional(() =>
    {
        var m = Session.Managed;
        var colliders = m.ArrayItems(m.Ptr(Session.System("sysFishing"), "FishingColliderCache")).Where(c => c != 0).Take(64).ToList();
        var zones = Session.World.Scripts(colliders).Values.Select(s => s.GetValueOrDefault("FishingZone")).Where(z => z != 0).Distinct();
        var result = new Dictionary<long, uint>();
        foreach (var zone in zones)
        {
            var infos = m.ListItems(m.Ptr(zone, "FishingZoneList"));
            if (infos.Count == 0) continue;
            if (_infoClass == 0) _infoClass = m.ClassOf(infos[0]);
            var idAt = Session.Il2Cpp.Field(m.ClassOf(infos[0]), "FishingZoneID");
            foreach (var (info, raw) in Session.Memory.ReadObjects(infos, idAt + 4)) result[info] = Bin.U32(raw, idAt);
        }
        return result;
    }, "vùng câu") ?? [];

    /// <summary>
    /// ID vùng câu của các vùng đặc biệt ĐANG NẠP quanh nhân vật: [(tên tài nguyên, ID)]. Vật thể vùng (MapObjectManager.CollectObjectComp)
    /// chỉ được game tạo khi lại gần; FishingZone (giữ FishingZoneList) nằm ở GameObject con nên duyệt cả cây con.
    /// </summary>
    public List<(string Asset, uint Id)> SpecialZoneIds() => Session.Optional(() =>
    {
        var m = Session.Managed;
        var result = new List<(string, uint)>();
        foreach (var thing in Session.Map.Things().Where(t => t.Asset.Contains("fishingzone")))
        {
            var klass = m.ClassOf(thing.Ref);
            var comp = klass != 0 && Session.Il2Cpp.HasField(klass, "CollectObjectComp") ? m.Ptr(thing.Ref, "CollectObjectComp") : 0;
            if (comp == 0) continue;
            foreach (var (name, zone) in Session.World.DescendantScripts(comp).Where(s => s.Class == "FishingZone"))
                foreach (var info in m.ListItems(m.Ptr(zone, "FishingZoneList")))
                    if (m.I32(info, "FishingZoneID") is > 0 and var id) result.Add((thing.Asset, (uint)id));
        }
        return result;
    }, "vùng đặc biệt") ?? [];

    /// <summary>ID gốc của vùng (kể cả vùng đã bị ghi giả).</summary>
    public uint RealZone(long info, uint current) => _fake.Original(info, current);

    public bool HasFakeZone => _fake.Active;

    /// <summary>
    /// Vùng game GỬI MÁY CHỦ ở lần quăng gần nhất (FishingSystem.CastingFishingZoneID - lấy từ danh sách ID vùng lúc dựng FishingCastingQ);
    /// null nếu không đọc được. Đây là vùng hiệu lực thật phía client.
    /// </summary>
    public uint? SentZone() => Session.Optional(() => Session.Managed.I32(Session.System("sysFishing"), "CastingFishingZoneID") is { } v ? (uint?)v : null, "vùng đã gửi");

    /// <summary>
    /// Bật / đổi vùng giả: ghi <paramref name="zoneId"/> vào mọi FishingZoneInfo CÒN SỐNG của bản đồ (đã quét) + quanh chỗ phao. Lúc quăng game
    /// dựng FishingCastingQ.FishingZoneIDList từ chính các ô này nên gói gửi đi mang vùng giả. Trả số ô đã ghi.
    /// </summary>
    public int ApplyFakeZone(int zoneId)
    {
        var known = CastZones().Concat((_mapZones ?? []).Select(z => KeyValuePair.Create(z.Info, z.Id))).DistinctBy(z => z.Key)
            .ToDictionary(z => z.Key, z => z.Value);
        var writes = _fake.Apply(Live(known.Keys), zoneId, known);
        Write(writes);
        if (writes.Count > 0) L.Info($"Giả vùng {zoneId}: ghi {writes.Count} ô vùng câu");
        return writes.Count;
    }

    /// <summary>Trả ID vùng gốc cho mọi FishingZoneInfo đã ghi giả (chỉ object còn sống).</summary>
    public void RestoreZones()
    {
        if (!_fake.Active) return;
        var writes = _fake.Restore(Live(_fake.Touched));
        Write(writes);
        L.Info($"Tắt giả vùng: trả ID gốc {writes.Count} ô");
    }

    /// <summary>Ghi ID vùng vào các ô FishingZoneInfo (1 lượt lệnh).</summary>
    private void Write(List<(long Info, uint Id)> writes)
    {
        var at = InfoIdOffset();
        if (writes.Count > 0 && at > 0) Session.Memory.WriteMany(writes.Select(w => (w.Info + at, Bin.Pack((int)w.Id))).ToList());
    }

    /// <summary>
    /// Đọc lại 1 lượt: chỉ giữ object CÒN là FishingZoneInfo (con trỏ class đúng) -> {object: ID hiện tại}. Object đã bị game giải phóng / dùng
    /// lại vùng nhớ thì bỏ - ghi vào đó là hỏng bộ nhớ game, văng game.
    /// </summary>
    private Dictionary<long, uint> Live(IEnumerable<long> infos)
    {
        var at = InfoIdOffset();
        if (at <= 0) return [];
        return Session.Memory.ReadObjects(infos, at + 4).Where(p => (long)Bin.U64(p.Value, 0) == _infoClass)
            .ToDictionary(p => p.Key, p => Bin.U32(p.Value, at));
    }

    /// <summary>Offset FishingZoneID trong FishingZoneInfo (biết class từ vùng chỗ phao hoặc lần quét); 0 nếu chưa biết class.</summary>
    private int InfoIdOffset() => _infoClass != 0 ? Session.Il2Cpp.Field(_infoClass, "FishingZoneID") : 0;

    /// <summary>Đổi cảnh: các FishingZoneInfo cũ không còn - quên sổ ghi, phải quét lại.</summary>
    public void SceneChanged()
    {
        _fake.Forget();
        _mapZones = null;
    }

    /// <summary>Đã quét xong mọi vùng câu của bản đồ hiện tại (<see cref="ScanMapZones"/>).</summary>
    public bool MapZonesScanned => _mapZones != null;

    /// <summary>
    /// Quét mọi FishingZoneInfo của bản đồ hiện tại: chép heap về máy (~1-2 phút - 1 lần mỗi bản đồ), lấy các FishingZone CÒN SỐNG (có object
    /// native) rồi danh sách FishingZoneList của chúng - không nhặt object rác chưa dọn. Game chỉ tìm vùng câu bằng truy vấn vật lý lúc quăng
    /// rồi xoá ngay trong cùng khung hình, không có đường tham chiếu nào tới được - nên phải quét. Trả số vùng tìm được.
    /// </summary>
    public int ScanMapZones(Action<string>? progress = null)
    {
        if (_infoClass == 0) _infoClass = HeapScan.FindClass(Session, "FishingZoneInfo");
        if (_zoneClass == 0) _zoneClass = HeapScan.FindClass(Session, "FishingZone");
        if (_infoClass == 0 || _zoneClass == 0)
        {
            _mapZones = [];
            return 0;
        }
        var m = Session.Managed;
        var cached = Session.Il2Cpp.Field(_zoneClass, "m_CachedPtr");
        var zones = HeapScan.FindObjects(Session, [_zoneClass], progress)[_zoneClass];
        var alive = Session.Memory.ReadObjects(zones, cached + 8)
            .Where(p => (long)Bin.U64(p.Value, 0) == _zoneClass && Bin.U64(p.Value, cached) != 0).Select(p => p.Key);
        var infos = alive.SelectMany(z => m.ListItems(m.Ptr(z, "FishingZoneList"))).Where(i => i != 0).Distinct().ToList();
        _mapZones = Live(infos).Select(p => (p.Key, _fake.Original(p.Key, p.Value))).ToList();
        L.Info($"Quét vùng câu bản đồ: {_mapZones.Count} ô, ID gốc {string.Join(", ", _mapZones.Select(z => z.Id).Distinct().Order())}");
        return _mapZones.Count;
    }
}
