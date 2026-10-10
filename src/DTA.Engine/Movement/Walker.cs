using DTA.Game.Session;
using DTA.Runtime.Core;
using DTA.Runtime.Device;

namespace DTA.Engine.Movement;

/// <summary>Kết quả 1 lần đi: tới nơi / không có đường / bị tắt hoặc đích không còn cần / game mở bảng hoặc cần ẩn / mất vị trí hay hướng camera.</summary>
public enum WalkResult { Arrived, Stuck, Stopped, Blocked, Lost }

/// <summary>
/// Cho nhân vật tự đi tới 1 điểm bằng cần điều khiển của game (đặt ngón ảo lên cần rồi kéo, như người chơi) - không dịch chuyển gì.
/// Biết đường nhờ: địa hình của game, phần tự học (<see cref="NavMap"/>), chuyển động thật game báo từng nhịp (đẩy cần mà vận tốc theo
/// hướng đó ~0 = bị chặn, biết ngay trong ~0,2 s và đổi đường).
/// </summary>
public sealed partial class Walker(GameSession session, Touch touch, Func<bool> running)
{
    private static readonly Logger L = Log.For("walker");
    private readonly Dictionary<int, NavMap> _maps = [];
    private readonly Dictionary<int, List<(float X, float Z)>> _doors = [];
    private ((int, int) Here, (float, float) Target, float Accept, IReadOnlyDictionary<(int, int), float> Extra, (List<(float X, float Z)> Path, float Cost)? Plan)? _planned;
    private (int X, int Y, int Reach)? _stick;
    private int _current;

    /// <summary>Lần đi gần nhất bỏ cuộc vì bị vây kín.</summary>
    public bool Trapped { get; private set; }

    /// <summary>Lần đi gần nhất dừng vì game bật bảng ngay lúc đang đi (lỡ vào cổng).</summary>
    public bool WalkedIn { get; private set; }

    private static double Now => Environment.TickCount64 / 1000.0;

    /// <summary>Bản đồ đường đi của bản đồ đang đứng (lần đầu thì nạp địa hình + cửa).</summary>
    public NavMap Nav()
    {
        var mapId = session.Camera.Map()?.Id ?? 0;
        if (!_maps.ContainsKey(mapId))
        {
            _maps[mapId] = new NavMap(Path.Combine(DTA.Runtime.Storage.Paths.DataDir, $"nav-{mapId}.json"), MapStore.TerrainFor(session, mapId));
            _doors[mapId] = MapStore.DoorsFor(session, mapId);
        }
        _current = mapId;
        return _maps[mapId];
    }

    private IEnumerable<(float X, float Z)> Doors => _doors.GetValueOrDefault(_current) ?? [];

    /// <summary>
    /// Đường từ ô đang đứng tới đích: (điểm phải qua - tâm ô, điểm cuối là đích; giá). Đường ngắn nhất phải qua chỗ lạ / từng chặn mà có
    /// đường chắc chắn không dài hơn quá 2,5 lần (+15) thì đi đường chắc chắn.
    /// </summary>
    private (List<(float X, float Z)> Path, float Cost)? Plan(NavMap nav, (int, int) here, (float X, float Z) target, float accept, float? height,
                                                               IReadOnlyDictionary<(int, int), float> extra, float bound = float.PositiveInfinity)
    {
        if (_planned is { } p && p.Here == here && p.Target == target && p.Accept == accept && SameExtra(p.Extra, extra))
        {
            _planned = null;
            return p.Plan;
        }
        var found = nav.Find(here, target, accept, height, extra, bound: bound);
        if (found is { Sure: false })
        {
            var safe = nav.Find(here, target, accept, height, extra, safe: true);
            if (safe != null && safe.Cost <= found.Cost * NavMap.SafeRatio + NavMap.SafeExtra) found = safe;
        }
        return found == null ? null : (found.Cells.Skip(1).Select(NavMap.Center).Append(target).ToList(), found.Cost);
    }

    private static bool SameExtra(IReadOnlyDictionary<(int, int), float> a, IReadOnlyDictionary<(int, int), float> b) =>
        a.Count == b.Count && a.All(p => b.TryGetValue(p.Key, out var v) && v == p.Value);

    /// <summary>Quãng còn lại từ từng điểm của đường tới điểm cuối.</summary>
    private static float[] Tails(IReadOnlyList<(float X, float Z)> path)
    {
        var tails = new float[path.Count];
        for (var k = path.Count - 2; k >= 0; k--) tails[k] = tails[k + 1] + NavMap.Dist(path[k].X, path[k].Z, path[k + 1].X, path[k + 1].Z);
        return tails;
    }

    /// <summary>
    /// Quãng đi bộ (mét + phạt qua tường) từ <paramref name="position"/> tới cách đích ≤ accept; null nếu không có / không rẻ hơn
    /// <paramref name="bound"/>. Đường vừa tính được giữ cho <see cref="Go"/> tới đúng đích đó dùng lại.
    /// </summary>
    public float? Cost(DTA.Game.Geometry.Vec3 position, (float X, float Z) target, float accept, IEnumerable<(float X, float Z, float R)>? avoid = null, float bound = float.PositiveInfinity)
    {
        var nav = Nav();
        var here = NavMap.Cell(position.X, position.Z);
        accept = Math.Max(accept, NavMap.CellSize * 0.75f);
        var extra = nav.Around(avoid ?? [], Doors);
        var plan = Plan(nav, here, target, accept, position.Y, extra, bound);
        _planned = plan != null || float.IsPositiveInfinity(bound) ? (here, target, accept, extra, plan) : null;
        return plan?.Cost;
    }

    /// <summary>Điểm cách <paramref name="center"/> đúng distance mét mà đi thẳng vào center không vướng: ưu tiên hướng angle, lệch dần 2 bên.</summary>
    public (float X, float Z) OpenSpot((float X, float Z) center, float distance, float angle, float? height = null)
    {
        var nav = Nav();
        foreach (var turn in new[] { 0f, 0.7f, -0.7f, 1.4f, -1.4f, 2.1f, -2.1f, 2.8f, -2.8f })
        {
            float x = center.X + MathF.Cos(angle + turn) * distance, z = center.Z + MathF.Sin(angle + turn) * distance;
            if (nav.Standable(NavMap.Cell(x, z), height) && nav.Clear(x, z, center.X, center.Z, height)) return (x, z);
        }
        return (center.X + MathF.Cos(angle) * distance, center.Z + MathF.Sin(angle) * distance);
    }

    /// <summary>Đứng ở <paramref name="position"/> không với tới vật ở <paramref name="target"/> dù đã gần: ghi như 1 bức tường trước mặt.</summary>
    public void Obstructed((float X, float Z) position, (float X, float Z) target)
    {
        var nav = Nav();
        var here = NavMap.Cell(position.X, position.Z);
        if (NavMap.Cell(target.X, target.Z) == here) return;
        float dx = target.X - position.X, dz = target.Z - position.Z;
        nav.Block(here, Math.Abs(dx) >= Math.Abs(dz) ? (here.I + (dx > 0 ? 1 : -1), here.J) : (here.I, here.J + (dz > 0 ? 1 : -1)));
        nav.Save();
    }

    /// <summary>Nhảy: hàm game DialogJoyStick.OnPress_JumpButton; không được thì bấm nút nhảy trên màn hình.</summary>
    private void Jump()
    {
        if (session.Tools.Jump()) return;
        if (session.Ui.HudButton("reel") is { } p) touch.Tap(p.X, p.Y);
    }
}
