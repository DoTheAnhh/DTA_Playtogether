using DTA.Engine.Bots;
using DTA.Engine.Movement;
using DTA.Game.Actor;
using DTA.Game.Geometry;

namespace DTA.Features.Fishing;

/// <summary>Phần vùng câu của <see cref="FishingBot"/>: dịch chuyển tới vùng đặc biệt, giả vùng câu, ghi nhớ ID vùng đã gặp.</summary>
public sealed partial class FishingBot
{
    public const string ZonesKey = "fishing_zones";
    private const float ZoneReach = 2.5f, ZoneTolerance = 0.8f, SpecialNear = 12;
    private const double LearnEvery = 20;
    private double _learned = double.NegativeInfinity;
    private const int FakeMissLimit = 3;
    private int _fakeMisses;
    private uint? _realZone;
    private static readonly DTA.Runtime.Core.Logger ZoneLog = DTA.Runtime.Core.Log.For("fishing");
    /// <summary>Vùng giả máy chủ đã từ chối ở bản đồ này (game báo "không thể câu ở đây"): (ID vùng, mã bản đồ).</summary>
    private (int Zone, int Map)? _fakeRejected;

    /// <summary>ID vùng giả đã biết chắc dùng được - luôn có trong danh sách (tên lấy theo bảng loại vùng <see cref="ZoneSpot.Known"/>).</summary>
    private static readonly int[] TestedZones = [3011, 3012, 3013, 3014, 3018];

    /// <summary>
    /// ID vùng có sẵn: vùng bạch tuộc 501 / 601 (đọc FishingZone lúc vùng hiện trên bản đồ, bản 2.32) + các ID người dùng đã thử
    /// (<see cref="TestedZones"/> - trùng mã loại vật thể spawn_fishingzone_* của vùng đó).
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> SeedZones = new Dictionary<string, string>
    {
        ["501"] = "Vùng bạch tuộc", ["601"] = "Vùng bạch tuộc",
    }.Concat(TestedZones.Select(id => KeyValuePair.Create(id.ToString(),
        ZoneSpot.Known.Values.FirstOrDefault(z => z.Id == id).Name ?? $"Vùng {id}"))).ToDictionary();

    /// <summary>
    /// Danh sách Giả vùng câu {ID: tên vị trí}: ID dò sẵn + ID đã gặp / tự nhập (lưu trong bộ đệm phiên bản game; trùng ID thì lấy bản đã gặp).
    /// </summary>
    public static Dictionary<string, string> KnownZones(DTA.Runtime.Storage.VersionCache cache)
    {
        var known = new Dictionary<string, string>(SeedZones);
        foreach (var (id, label) in cache.Get<Dictionary<string, string>>(ZonesKey) ?? []) known[id] = label;
        return known;
    }

    /// <summary>Trước mỗi lần quăng: tới vùng câu đã chọn (nếu bật), ghi ID vùng giả (nếu bật) hoặc trả gốc nếu vừa tắt.</summary>
    private void BeforeCast()
    {
        LearnSpecialZones();
        if (options.ZoneTele) FocusZone();
        var map = Session.Camera.Map()?.Id ?? 0;
        if (options.FakeZoneOn && options.FakeZone != 0 && _fakeRejected != (options.FakeZone, map))
        {
            if (!_game.MapZonesScanned)
            {
                Events.Status("Đang dò mọi vùng câu của bản đồ để giả vùng (lần đầu mỗi bản đồ, khoảng 1-2 phút)...", Level.Info);
                var count = _game.ScanMapZones(p => Events.Status($"Đang dò vùng câu của bản đồ... {p}", Level.Info));
                Events.Status(count > 0 ? $"Đã dò {count} vùng câu - quăng nào cũng tính là vùng {options.FakeZone}" : "Không dò được vùng câu nào trên bản đồ", count > 0 ? Level.Ok : Level.Warn);
            }
            _game.ApplyFakeZone(options.FakeZone);
        }
        else if (_game.HasFakeZone) _game.RestoreZones();
    }

    /// <summary>
    /// Phao vừa xuống nước: nhớ ID các vùng câu ở đây (game không có sẵn danh sách vùng) kèm tên (vùng đặc biệt gần trong 12 m / vùng thường),
    /// tên vị trí = bản đồ (kèm loại vùng đặc biệt gần trong 12 m), rồi ghi ID giả cho vùng nước mới gặp (game đọc lại ID ở mỗi lần quăng).
    /// </summary>
    private void AfterCast()
    {
        LogZones();
        var zones = _game.CastZones();
        if (zones.Count == 0) return;
        var known = KnownZones(Session.Cache);
        var map = Session.Camera.Map()?.Name ?? "bản đồ ?";
        var place = Session.Player.Position();
        var near = place is { } p ? _game.ZoneSpots().MinBy(z => NavMap.Dist(z.X, z.Z, p.X, p.Z)) : null;
        var label = near != null && place is { } q && NavMap.Dist(near.X, near.Z, q.X, q.Z) <= SpecialNear ? $"{ZoneSpot.NameOf(near.Asset)} - {map}" : map;
        var fresh = zones.Select(z => _game.RealZone(z.Key, z.Value)).Where(z => z != 0 && !known.ContainsKey(z.ToString())).Distinct().ToList();
        if (fresh.Count > 0)
        {
            foreach (var zone in fresh) known[zone.ToString()] = label;
            Session.Cache.Put(ZonesKey, known);
        }
        if (options.FakeZoneOn && options.FakeZone != 0) _game.ApplyFakeZone(options.FakeZone);
    }

    /// <summary>
    /// Ghi nhật ký vùng câu của lần quăng vừa rồi: vùng thực tế (vùng game gửi ở lần quăng gần nhất KHÔNG giả), vùng giả đang chọn, vùng game
    /// thực sự gửi máy chủ (CastingFishingZoneID) - để kiểm vùng giả có vào được gói FishingCastingQ không.
    /// </summary>
    private void LogZones()
    {
        var sent = _game.SentZone();
        if (!_game.HasFakeZone && sent is > 0) _realZone = sent;
        var fake = options.FakeZoneOn && options.FakeZone != 0 ? options.FakeZone.ToString() : "tắt";
        ZoneLog.Info($"Vùng câu - thực tế: {_realZone?.ToString() ?? "?"}, giả: {fake}, game gửi: {sent?.ToString() ?? "?"}");
    }

    /// <summary>
    /// Quăng xuống nước mà không con nào cắn khi đang giả vùng: 3 lần liền = máy chủ không nhận vùng giả ở bản đồ này (game báo "không thể
    /// câu ở đây") -> trả vùng thật, thôi giả vùng đó ở bản đồ này (đổi vùng / bản đồ thì thử lại), báo người dùng.
    /// </summary>
    private void FakeZoneMissed()
    {
        if (!options.FakeZoneOn || !_game.HasFakeZone || ++_fakeMisses < FakeMissLimit) return;
        _fakeMisses = 0;
        _game.RestoreZones();
        _fakeRejected = (options.FakeZone, Session.Camera.Map()?.Id ?? 0);
        Events.Status($"Máy chủ không nhận vùng giả {options.FakeZone} ở bản đồ này (game báo không thể câu ở đây) - đã trả vùng thật", Level.Warn);
    }

    /// <summary>
    /// Ghi ID các vùng câu đặc biệt đang nạp quanh nhân vật vào danh sách Giả vùng câu ("ID - loại vùng - bản đồ"), tối đa 1 lần / 20 s: đi
    /// ngang 1 vùng đặc biệt 1 lần là sau này đứng chỗ khác vẫn giả được vùng đó. Vùng đã ghi nhãn thường (bản đồ) thì đổi sang nhãn vùng.
    /// </summary>
    private void LearnSpecialZones()
    {
        if (Now - _learned < LearnEvery) return;
        _learned = Now;
        var found = _game.SpecialZoneIds();
        if (found.Count == 0) return;
        var known = KnownZones(Session.Cache);
        var map = Session.Camera.Map()?.Name ?? "bản đồ ?";
        var changed = false;
        foreach (var (asset, id) in found)
        {
            var label = $"{ZoneSpot.NameOf(asset)} - {map}";
            if (known.GetValueOrDefault(id.ToString()) == label) continue;
            known[id.ToString()] = label;
            changed = true;
        }
        if (changed) Session.Cache.Put(ZonesKey, known);
    }

    /// <summary>
    /// Dịch chuyển tới vùng câu đã chọn: đứng trên vòng tròn quanh tâm cách đúng khoảng quăng, mặt / mũi thuyền quay thẳng vào tâm -> phao rơi
    /// giữa vùng. Đi bộ: chỗ đứng phải có đất (không có thì lùi xa thêm tối đa 2,5 m - phao vẫn trong vùng); ngồi thuyền: chỗ đứng là mặt nước.
    /// Đang đứng đúng chỗ thì thôi.
    /// </summary>
    private void FocusZone()
    {
        var focus = options.ZoneFocus;
        var zones = _game.ZoneSpots().Where(z => focus == FishingOptions.AnyZone || z.Asset == focus).ToList();
        if (zones.Count == 0 || Session.Player.Facing() is not var (place, forward))
        {
            Events.Status($"Chưa thấy {(focus == FishingOptions.AnyZone ? "vùng câu đặc biệt" : ZoneSpot.NameOf(focus))} trên bản đồ - câu tại chỗ", Level.Quiet);
            return;
        }
        var zone = zones.MinBy(z => NavMap.Dist(z.X, z.Z, place.X, place.Z))!;
        var cast = _game.CastingDistance() ?? 4;
        float dx = zone.X - place.X, dz = zone.Z - place.Z, distance = MathF.Sqrt(dx * dx + dz * dz);
        if (distance > 0.1f && Math.Abs(distance - cast) <= ZoneTolerance && (dx * forward.X + dz * forward.Z) / distance >= 0.97f) return;
        var boat = Session.Vehicles.Current()?.IsBoat == true;
        var baseAngle = distance > 0.1f ? MathF.Atan2(-dz, -dx) : 0;
        Vec3? spot = null;
        foreach (var radius in boat ? [cast] : new[] { cast, cast + ZoneReach * 0.5f, cast + ZoneReach })
        {
            for (var k = 0; k < 36 && spot == null; k++)
            {
                var angle = baseAngle + 10 * ((k + 1) / 2) * (k % 2 == 1 ? 1 : -1) * MathF.PI / 180;
                float x = zone.X + radius * MathF.Cos(angle), z = zone.Z + radius * MathF.Sin(angle);
                var ground = Session.Ground.Resolve(x, z, place.Y);
                if (boat && ground.Kind != "navmesh_detail") spot = new Vec3(x, place.Y, z);
                else if (!boat && ground is { Y: { } y, Kind: "navmesh_detail" }) spot = new Vec3(x, y, z);
            }
            if (spot != null) break;
        }
        var name = ZoneSpot.NameOf(zone.Asset);
        if (spot is not { } target)
        {
            Events.Status($"{name} ở xa bờ - hãy ngồi thuyền để tới được vùng này", Level.Warn);
            return;
        }
        Events.Status($"Dịch chuyển tới {name}...", Level.Info);
        var result = Session.Teleport.Go(new TeleportRequest(target, Rotation: Quat.LookAt(target, new Vec3(zone.X, zone.Y, zone.Z)), Name: name), cancel: Cancel);
        Events.Status(result.Ok ? $"Đã tới {name} - quăng cần vào giữa vùng" : $"Không dịch chuyển tới {name} được ({result.Status}) - câu tại chỗ", result.Ok ? Level.Ok : Level.Warn);
    }
}
