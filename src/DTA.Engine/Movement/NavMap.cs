using DTA.Game.World;
using DTA.Runtime.Storage;

namespace DTA.Engine.Movement;

/// <summary>Cạnh chung 2 ô kề: ô phía âm + trục vuông góc (0 = x, 1 = z).</summary>
public readonly record struct Edge(int I, int J, int Axis);

/// <summary>
/// Hiểu biết về đường đi của 1 bản đồ: địa hình của game (NavMesh) + phần tự học khi đi (ô đã qua, cạnh bị chặn bởi thứ ngoài địa hình,
/// cạnh phải nhảy, bậc lưới vẽ dư vẫn qua được, chỗ lỡ đi vào cổng). Lưu data/nav-(mã bản đồ).json (cùng định dạng bản Python).
/// </summary>
public sealed partial class NavMap
{
    public const float CellSize = 1.0f;
    /// <summary>Đã gặp 2 lần mà vẫn bị chặn thì giá gấp đôi, trần WallMax; cạnh vừa đụng trong lần đi này (shut) cấm hẳn.</summary>
    private const float Unknown = 1.15f, Wall = 60f, WallMax = 2000f, Barrier = 500f, Return = 8f, Rise = 1.3f, Off = 2.5f, Same = 1.5f;
    private const float Climb = 3f, Snug = 0.6f, Avoid = 8f, Door = 4f;
    private const int ForgetDays = 2, EnclosedCells = 300;
    private static readonly float[] Suspect = [14f, 9f, 5f, 3f];
    private static readonly float[] Line = [.. Enumerable.Repeat(60f, 4), .. Enumerable.Repeat(30f, 4), .. Enumerable.Repeat(15f, 4)];

    private readonly string _path;
    public Terrain? Terrain { get; }
    private bool _changed;
    private readonly HashSet<(int, int)> _free = [];
    private readonly Dictionary<Edge, int> _walls = [];
    private readonly Dictionary<Edge, int> _days = [];
    private readonly HashSet<Edge> _shut = [], _jumps = [], _opened = [];
    private readonly HashSet<StepKey> _passed = [];
    private readonly Dictionary<Edge, float> _suspects = [];
    private readonly List<(float X, float Z)> _traps = [];

    public NavMap(string path, Terrain? terrain)
    {
        (_path, Terrain) = (path, terrain);
        Merge(JsonStore.Load<Stored?>(path, null));
    }

    public IReadOnlyDictionary<Edge, int> Walls => _walls;
    public IReadOnlyDictionary<Edge, float> Suspects => _suspects;
    public IReadOnlySet<Edge> Jumps => _jumps;

    private static int Today => (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 86400);

    /// <summary>Dạng file (giống bản Python): free [i,j], jumps [i,j,trục], passed [i,j,di,dj], walls [i,j,trục,lần,ngày], traps [x,z], suspects [i,j,trục,thêm].</summary>
    private sealed record Stored(float Cell, bool Levels, List<int[]> Free, List<int[]> Jumps, List<int[]> Passed, List<double[]> Walls, List<double[]> Traps, List<double[]> Suspects);

    /// <summary>Gộp kiến thức đã lưu (lần chạy trước / tab khác cùng bản đồ). Tường mới đụng 1 lần đã quá 2 ngày thì quên (vật chặn tạm).</summary>
    private void Merge(Stored? data)
    {
        if (data == null || data.Cell != CellSize) return;
        foreach (var c in data.Free ?? []) _free.Add((c[0], c[1]));
        foreach (var e in data.Jumps ?? []) if (!_walls.ContainsKey(new Edge(e[0], e[1], e[2]))) _jumps.Add(new Edge(e[0], e[1], e[2]));
        if (data.Levels) foreach (var s in (data.Passed ?? []).Where(s => s.Length == 4)) _passed.Add(new StepKey(s[0], s[1], s[2], s[3]));
        foreach (var t in data.Traps ?? []) if (_traps.All(p => Dist(p.X, p.Z, (float)t[0], (float)t[1]) > 1)) _traps.Add(((float)t[0], (float)t[1]));
        foreach (var s in data.Suspects ?? [])
        {
            var edge = new Edge((int)s[0], (int)s[1], (int)s[2]);
            _suspects[edge] = Math.Max(_suspects.GetValueOrDefault(edge), (float)s[3]);
        }
        foreach (var w in data.Walls ?? [])
        {
            var edge = new Edge((int)w[0], (int)w[1], (int)w[2]);
            int hits = (int)w[3], day = w.Length > 4 ? (int)w[4] : Today;
            if (hits <= 1 && Today - day > ForgetDays && !_walls.ContainsKey(edge)) continue;
            if (_opened.Contains(edge) || _jumps.Contains(edge)) continue;
            _walls[edge] = Math.Max(_walls.GetValueOrDefault(edge), hits);
            _days[edge] = Math.Max(_days.GetValueOrDefault(edge), day);
        }
        _links.Clear();
    }

    /// <summary>Lưu nếu có học thêm (gộp file trước để không đè kiến thức tab khác vừa ghi).</summary>
    public void Save()
    {
        if (!_changed) return;
        _changed = false;
        Merge(JsonStore.Load<Stored?>(_path, null));
        JsonStore.Save(_path, new Stored(CellSize, true,
            _free.Order().Select(c => new[] { c.Item1, c.Item2 }).ToList(),
            _jumps.Select(e => new[] { e.I, e.J, e.Axis }).ToList(),
            _passed.Select(s => new[] { s.I, s.J, s.Di, s.Dj }).ToList(),
            _walls.Select(p => new double[] { p.Key.I, p.Key.J, p.Key.Axis, p.Value, _days.GetValueOrDefault(p.Key) }).ToList(),
            _traps.Select(t => new double[] { t.X, t.Z }).ToList(),
            _suspects.Where(p => !_walls.ContainsKey(p.Key) && !_opened.Contains(p.Key)).Select(p => new double[] { p.Key.I, p.Key.J, p.Key.Axis, p.Value }).ToList()));
    }

    public static (int I, int J) Cell(float x, float z) => ((int)MathF.Floor(x / CellSize), (int)MathF.Floor(z / CellSize));
    public static (float X, float Z) Center((int I, int J) c) => ((c.I + 0.5f) * CellSize, (c.J + 0.5f) * CellSize);
    public static float Dist(float x0, float z0, float x1, float z1) => MathF.Sqrt((x1 - x0) * (x1 - x0) + (z1 - z0) * (z1 - z0));

    /// <summary>Cạnh chung 2 ô kề cạnh.</summary>
    public static Edge EdgeOf((int I, int J) a, (int I, int J) b) => new(Math.Min(a.I, b.I), Math.Min(a.J, b.J), a.I != b.I ? 0 : 1);

    /// <summary>Các cạnh phải thông để đi thẳng a -> b kề; đi chéo = cả 4 cạnh quanh góc.</summary>
    public static Edge[] Crossing((int I, int J) a, (int I, int J) b)
    {
        if (a.I == b.I || a.J == b.J) return [EdgeOf(a, b)];
        (int, int) c = (b.I, a.J), d = (a.I, b.J);
        return [EdgeOf(a, c), EdgeOf(c, b), EdgeOf(a, d), EdgeOf(d, b)];
    }

    /// <summary>Tầng mặt đất ô <paramref name="cell"/> đứng lên được từ độ cao <paramref name="height"/> (lệch ≤ reach); -1 nếu không có.</summary>
    public int Layer((int, int) cell, float? height, float reach)
    {
        if (Terrain == null || !Terrain.Ground.TryGetValue(cell, out var levels) || levels.Length == 0) return -1;
        if (height is not { } h) return 0;
        var near = Enumerable.Range(0, levels.Length).MinBy(k => Math.Abs(levels[k] - h));
        return Math.Abs(levels[near] - h) <= reach ? near : -1;
    }

    /// <summary>Đứng được ở ô này (có đất ở độ cao đó / từng đi qua / không có địa hình).</summary>
    public bool Standable((int, int) cell, float? height) => Terrain == null || _free.Contains(cell) || Layer(cell, height, Same) >= 0;

    /// <summary>
    /// Đi thẳng (x0,z0) -> (x1,z1) được: không cắt cạnh bị chặn, không cắt mép địa hình ở tầng đang đứng. <paramref name="strict"/> = cạnh
    /// đang nghi cũng tính chặn và không xuyên ô trong <paramref name="avoid"/>.
    /// </summary>
    public bool Clear(float x0, float z0, float x1, float z1, float? height = null, bool strict = true, IReadOnlyDictionary<(int, int), float>? avoid = null)
    {
        var steps = Math.Max(1, (int)(Dist(x0, z0, x1, z1) / (CellSize / 4)));
        var previous = Cell(x0, z0);
        for (var k = 1; k <= steps; k++)
        {
            var cell = Cell(x0 + (x1 - x0) * k / steps, z0 + (z1 - z0) * k / steps);
            if (cell == previous) continue;
            if (strict && ((avoid?.ContainsKey(cell) ?? false) || !Standable(cell, height))) return false;
            if (Crossing(previous, cell).Any(e => _walls.ContainsKey(e) || (strict && _suspects.ContainsKey(e)))) return false;
            if (Terrain != null && !_passed.Contains(Terrain.Key(previous, cell)) && Terrain.Bars(previous, cell, height)) return false;
            previous = cell;
        }
        return true;
    }

    /// <summary>Từ tâm ô với thẳng tới điểm được (không cắt cạnh đã học / đang nghi).</summary>
    public bool Reaches((int, int) cell, (float X, float Z) target)
    {
        var (x0, z0) = Center(cell);
        var steps = Math.Max(1, (int)(Dist(x0, z0, target.X, target.Z) / (CellSize / 4)));
        var previous = cell;
        for (var k = 1; k <= steps; k++)
        {
            var here = Cell(x0 + (target.X - x0) * k / steps, z0 + (target.Z - z0) * k / steps);
            if (here == previous) continue;
            if (Crossing(previous, here).Any(e => _walls.ContainsKey(e) || _suspects.ContainsKey(e))) return false;
            previous = here;
        }
        return true;
    }

    /// <summary>
    /// Ô cần né: vật chắn tạm (x, z, bán kính) +8 ô; cửa / cổng qua bản đồ + chỗ từng lỡ đi vào trong 4 m CẤM HẲN (vô cực - chỉ được đi
    /// ra nếu đang đứng trong đó; đích nằm trong vùng cấm thì không có đường -> bỏ đích).
    /// </summary>
    public Dictionary<(int, int), float> Around(IEnumerable<(float X, float Z, float R)> obstacles, IEnumerable<(float X, float Z)> doors)
    {
        var extra = new Dictionary<(int, int), float>();
        foreach (var (x, z) in doors.Concat(_traps)) Fill(x, z, Door, _ => float.PositiveInfinity);
        foreach (var (x, z, r) in obstacles) Fill(x, z, r, old => Math.Max(old, Avoid));
        return extra;

        void Fill(float x, float z, float radius, Func<float, float> value)
        {
            var (low, high) = (Cell(x - radius, z - radius), Cell(x + radius, z + radius));
            for (var i = low.I; i <= high.I; i++)
            for (var j = low.J; j <= high.J; j++)
                if (Dist((i + 0.5f) * CellSize, (j + 0.5f) * CellSize, x, z) <= radius) extra[(i, j)] = value(extra.GetValueOrDefault((i, j)));
        }
    }

    /// <summary>Ghi nhớ: đi tới đây thì game bật bảng (lỡ vào cổng) - lần sau né.</summary>
    public void Trap(float x, float z)
    {
        if (_traps.Any(p => Dist(p.X, p.Z, x, z) <= 1)) return;
        _traps.Add((MathF.Round(x, 2), MathF.Round(z, 2)));
        _changed = true;
    }
}
