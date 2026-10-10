using DTA.Game.Geometry;
using DTA.Game.Session;
using DTA.Runtime.Core;

namespace DTA.Game.World;

/// <summary>Kết quả tìm mặt đất: độ cao (null = không thấy), kiểu (navmesh_detail / navmesh_nearest / water_surface), lệch so với đích.</summary>
public readonly record struct Surface(float? Y, string Kind, float Delta);

/// <summary>
/// Lưới đi được (NavMesh) của bản đồ đang chơi + chỉ mục tam giác mặt đất chính xác để tính độ cao đáp khi dịch chuyển. Dùng chung theo phiên.
/// </summary>
public sealed class GroundReader(GameSession session)
{
    public const string SurfaceClass = "Unity.AI.Navigation.NavMeshSurface";
    private const int Entry = 48, MaxTileSize = 1 << 20;
    private const float GridCell = 4.0f, Footprint = 0.45f, LayerGap = 1.2f, SearchStep = 0.25f;
    private static readonly TimeSpan Recheck = TimeSpan.FromSeconds(2);
    private static readonly Logger L = Log.For("ground");

    private Dictionary<(int, int), List<Vec3[]>>? _grid;
    private string _mark = "";
    private DateTime _checked;

    /// <summary>
    /// (dấu nhận dạng, các mặt lưới) đang bật (NavMeshSurface.s_NavMeshSurfaces). Dấu = CRC chỗ đặt + mã băm từng ô; trùng
    /// <paramref name="known"/> thì không đọc lại ~0,6 MB dữ liệu (Surfaces = null). Null nếu bản đồ không có lưới / không đọc được.
    /// </summary>
    public (string Mark, List<NavSurface>? Surfaces)? Read(string known = "") => session.Optional(() => ReadCore(known), "NavMesh");

    private (string, List<NavSurface>?)? ReadCore(string known)
    {
        var klass = session.Class(SurfaceClass);
        var holder = klass != 0 ? session.Il2Cpp.StaticFieldPtr(klass, "s_NavMeshSurfaces") : 0;
        var surfaces = holder != 0 ? session.Managed.ListItems((long)session.Memory.U64(holder)) : [];
        if (surfaces.Count == 0) return null;
        int dataAt = session.Il2Cpp.Field(klass, "m_NavMeshData"), posAt = session.Il2Cpp.Field(klass, "m_LastPosition"), rotAt = session.Il2Cpp.Field(klass, "m_LastRotation");
        var found = new List<(Vec3, Quat, List<(long Ptr, int Size)>)>();
        var digest = 0u;
        foreach (var surface in surfaces)
        {
            var raw = session.Memory.Read(surface, new[] { dataAt + 8, posAt + 12, rotAt + 16 }.Max());
            var data = raw != null ? Bin.U64(raw, dataAt) : 0;
            var native = data != 0 ? (long)session.Memory.U64(data + 0x10) : 0;
            if (native == 0 || TileTable(native) is not { } table) continue;
            var entries = Enumerable.Range(0, table.Length / Entry).Select(k => (Bin.U64(table, k * Entry), (int)Bin.U64(table, k * Entry + 16))).ToList();
            if (entries.Any(e => e.Item1 == 0 || e.Item2 is < NavMesh.Header or > MaxTileSize)) return null;
            var placement = raw![posAt..(posAt + 12)].Concat(raw[rotAt..(rotAt + 16)]).ToArray();
            digest = Crc32.Compute(placement, digest);
            for (var k = 0; k < table.Length; k += Entry) digest = Crc32.Compute(table.AsSpan(k + 16, Entry - 16), digest);
            found.Add((new Vec3(Bin.F32(raw, posAt), Bin.F32(raw, posAt + 4), Bin.F32(raw, posAt + 8)),
                new Quat(Bin.F32(raw, rotAt), Bin.F32(raw, rotAt + 4), Bin.F32(raw, rotAt + 8), Bin.F32(raw, rotAt + 12)), entries));
        }
        if (found.Count == 0) return null;
        var mark = $"{digest:x8}-{found.Sum(f => f.Item3.Count)}";
        if (mark == known) return (mark, null);
        var result = new List<NavSurface>();
        foreach (var (position, rotation, entries) in found)
        {
            var tiles = entries.Chunk(64).SelectMany(chunk => session.Memory.ReadMany(chunk.Select(e => (e.Ptr, e.Size)).ToList())).ToList();
            if (tiles.Any(t => t == null)) return null;
            result.Add(new NavSurface(position, rotation, tiles!));
        }
        L.Info($"Đọc NavMesh {mark}: {result.Sum(s => s.Tiles.Count)} ô");
        return (mark, result);
    }

    /// <summary>
    /// Bảng ô dữ liệu trong NavMeshData của engine (cặp con trỏ đầu / cuối bảng). Offset tuỳ bản Unity: thử từng chỗ, chỉ nhận chỗ
    /// chia hết cỡ mục và mục đầu trỏ đúng ô "VAND"; nhớ theo phiên bản game.
    /// </summary>
    private byte[]? TileTable(long native)
    {
        var head = session.Memory.Read(native, 0x140);
        if (head == null) return null;
        var saved = session.Cache.Get<int[]>("nav_tiles") is [var s] ? s : -1;
        var offsets = (saved >= 0 && saved <= head.Length - 16 ? [saved] : Array.Empty<int>()).Concat(Enumerable.Range(0, (head.Length - 0x20 - 15 + 7) / 8).Select(i => 0x20 + 8 * i));
        foreach (var offset in offsets)
        {
            long begin = Bin.U64(head, offset), end = Bin.U64(head, offset + 8);
            if (begin == 0 || end - begin is <= 0 or > Entry * 100000 || (end - begin) % Entry != 0) continue;
            var first = session.Memory.Read(begin, Entry);
            var pointer = first != null ? Bin.U64(first, 0) : 0;
            if (pointer == 0 || Bin.U64(first!, 16) < NavMesh.Header || session.Memory.Read(pointer, 4) is not { } magic || !magic.AsSpan().SequenceEqual(NavMesh.Magic)) continue;
            if (offset != saved) session.Cache.Put("nav_tiles", new[] { offset });
            return session.Memory.Read(begin, (int)(end - begin));
        }
        return null;
    }

    /// <summary>Chỉ mục tam giác mặt đất theo ô 4 m của bản đồ hiện tại; dựng lại khi lưới đổi. Kiểm lưới tối đa 1 lần / 2 giây.</summary>
    public Dictionary<(int, int), List<Vec3[]>>? Index()
    {
        if (_grid != null && DateTime.UtcNow - _checked < Recheck) return _grid;
        var read = Read(_grid != null ? _mark : "");
        _checked = DateTime.UtcNow;
        if (read is not var (mark, surfaces)) return _grid;
        if (surfaces == null) return _grid;
        var grid = new Dictionary<(int, int), List<Vec3[]>>();
        foreach (var tri in NavMesh.SurfaceTris(surfaces))
        {
            for (var ix = (int)Math.Floor(tri.Min(v => v.X) / GridCell); ix <= (int)Math.Floor(tri.Max(v => v.X) / GridCell); ix++)
            for (var iz = (int)Math.Floor(tri.Min(v => v.Z) / GridCell); iz <= (int)Math.Floor(tri.Max(v => v.Z) / GridCell); iz++)
                (grid.TryGetValue((ix, iz), out var list) ? list : grid[(ix, iz)] = []).Add(tri);
        }
        if (grid.Count > 0) (_grid, _mark) = (grid, mark);
        return _grid;
    }

    /// <summary>Độ cao mọi tầng mặt đất (cầu, sàn, mái...) tại (x, z).</summary>
    public static List<float> Heights(Dictionary<(int, int), List<Vec3[]>> grid, float x, float z) =>
        grid.TryGetValue(((int)Math.Floor(x / GridCell), (int)Math.Floor(z / GridCell)), out var cell)
            ? cell.Where(t => NavMesh.Inside(t, x, z)).Select(t => NavMesh.Height(t, x, z)).ToList()
            : [];

    /// <summary>
    /// Độ cao MẶT ĐẤT tại (x, z) trước khi dịch chuyển. Thuyền giữ mặt nước. Nhiều tầng thì chọn tầng gần <paramref name="targetY"/>.
    /// Quét cả dấu chân nhân vật (tâm + 2 vòng 8 điểm) trên cùng tầng, lấy điểm CAO NHẤT (mép dốc không làm kẹt). Đích ngoài lưới
    /// (sát tường, mép nước) thì lấy tầng của điểm có lưới gần nhất, dò vòng tròn mỗi 0,25 m tới <paramref name="maxRadius"/>.
    /// </summary>
    public Surface Resolve(float x, float z, float? targetY = null, bool boat = false, float maxRadius = 3.0f)
    {
        if (boat) return new Surface(targetY ?? 0, "water_surface", 0);
        if (Index() is not { } grid) return new Surface(null, "surface_not_found", 0);
        var heights = Heights(grid, x, z);
        var kind = "navmesh_detail";
        for (var r = SearchStep; heights.Count == 0 && r <= maxRadius; r += SearchStep)
        {
            kind = "navmesh_nearest";
            for (var k = 0; k < 16; k++) heights.AddRange(Heights(grid, x + r * MathF.Cos(k * MathF.PI / 8), z + r * MathF.Sin(k * MathF.PI / 8)));
        }
        if (heights.Count == 0) return new Surface(null, "surface_not_found", 0);
        var layer = targetY is { } ty ? heights.MinBy(h => Math.Abs(h - ty)) : heights.Max();
        var ground = layer;
        foreach (var radius in new[] { Footprint / 2, Footprint })
            for (var k = 0; k < 8; k++)
                foreach (var h in Heights(grid, x + radius * MathF.Cos(k * MathF.PI / 4), z + radius * MathF.Sin(k * MathF.PI / 4)))
                    if (Math.Abs(h - layer) <= LayerGap && h > ground) ground = h;
        return new Surface(ground, kind, targetY is { } t ? Math.Abs(ground - t) : 0);
    }
}
