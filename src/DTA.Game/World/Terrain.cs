using DTA.Game.Geometry;
using DTA.Runtime.Storage;

namespace DTA.Game.World;

/// <summary>Khoá 1 bước giữa 2 ô kề (kể cả chéo), không phân biệt chiều.</summary>
public readonly record struct StepKey(int I, int J, int Di, int Dj);

/// <summary>
/// Mặt đất + tường của 1 bản đồ theo lưới ô vuông (dựng từ NavMesh của game): ô nào có đất (các tầng độ cao), bước nào cắt ngang mép
/// lưới (tường / vực), ô nào sát mép. Walker dùng để vòng qua nhà cửa từ đầu. Dựng ~0,6 s / bản đồ nên lưu file theo dấu lưới.
/// </summary>
public sealed class Terrain
{
    private const float Merge = 0.3f, Level = 1.5f, Tight = 0.4f;
    private static readonly (int Di, int Dj)[] Steps = [(1, 0), (0, 1), (1, 1), (1, -1)];

    public float Cell { get; }
    public IReadOnlyDictionary<(int I, int J), float[]> Ground { get; }
    public IReadOnlyDictionary<StepKey, float[]> Barred { get; }
    public IReadOnlySet<(int I, int J)> TightCells { get; }
    public (int I, int J) Low { get; }
    public (int I, int J) High { get; }

    public Terrain(float cell, Dictionary<(int, int), float[]> ground, Dictionary<StepKey, float[]> barred, HashSet<(int, int)> tight)
    {
        (Cell, Ground, Barred, TightCells) = (cell, ground, barred, tight);
        if (ground.Count == 0) return;
        Low = (ground.Keys.Min(k => k.Item1), ground.Keys.Min(k => k.Item2));
        High = (ground.Keys.Max(k => k.Item1), ground.Keys.Max(k => k.Item2));
    }

    /// <summary>Khoá bước a -> b (4 kiểu ghi trong Barred; kiểu ngược tra từ ô bên kia).</summary>
    public static StepKey Key((int I, int J) a, (int I, int J) b)
    {
        var move = (b.I - a.I, b.J - a.J);
        return Steps.Contains(move) ? new StepKey(a.I, a.J, move.Item1, move.Item2) : new StepKey(b.I, b.J, -move.Item1, -move.Item2);
    }

    /// <summary>Bước a -> b có cắt mép lưới ở tầng <paramref name="height"/> (null = tầng nào cũng tính) không.</summary>
    public bool Bars((int I, int J) a, (int I, int J) b, float? height) =>
        Barred.TryGetValue(Key(a, b), out var levels) && (height == null || levels.Any(l => Math.Abs(l - height.Value) <= Level));

    /// <summary>Dựng từ đa giác + mép lưới (xem <see cref="NavMesh.Mesh"/>).</summary>
    public static Terrain Build(IEnumerable<NavSurface> surfaces, float cell)
    {
        var (polys, walls) = NavMesh.Mesh(surfaces);
        var ground = new Dictionary<(int, int), List<float>>();
        var barred = new Dictionary<StepKey, List<float>>();
        var tight = new HashSet<(int, int)>();
        foreach (var verts in polys) FillGround(verts, cell, ground);
        foreach (var wall in walls) MarkWall(wall, cell, barred, tight);
        tight.IntersectWith(ground.Keys);
        return new Terrain(cell, ground.ToDictionary(p => p.Key, p => p.Value.Order().ToArray()), barred.ToDictionary(p => p.Key, p => p.Value.ToArray()), tight);
    }

    /// <summary>Ghi độ cao tâm mọi ô nằm trong đa giác (tầng lệch ≤ 0,3 m coi là 1).</summary>
    private static void FillGround(Vec3[] verts, float cell, Dictionary<(int, int), List<float>> ground)
    {
        float minX = verts.Min(v => v.X), maxX = verts.Max(v => v.X), minZ = verts.Min(v => v.Z), maxZ = verts.Max(v => v.Z);
        for (var i = (int)Math.Ceiling(minX / cell - 0.501); i <= (int)Math.Floor(maxX / cell - 0.499); i++)
        for (var j = (int)Math.Ceiling(minZ / cell - 0.501); j <= (int)Math.Floor(maxZ / cell - 0.499); j++)
        {
            float x = (i + 0.5f) * cell, z = (j + 0.5f) * cell;
            if (!NavMesh.Inside(verts, x, z)) continue;
            var height = NavMesh.Height(verts, x, z);
            var levels = ground.TryGetValue((i, j), out var l) ? l : ground[(i, j)] = [];
            if (levels.All(level => Math.Abs(height - level) > Merge)) levels.Add(height);
        }
    }

    /// <summary>Đánh dấu ô sát mép (tâm cách mép &lt; 0,4 m) và các bước cắt ngang mép này.</summary>
    private static void MarkWall(Wall w, float cell, Dictionary<StepKey, List<float>> barred, HashSet<(int, int)> tight)
    {
        float wx = w.X1 - w.X0, wz = w.Z1 - w.Z0, length = wx * wx + wz * wz;
        for (var i = (int)Math.Floor(Math.Min(w.X0, w.X1) / cell - 0.5) - 1; i <= (int)Math.Floor(Math.Max(w.X0, w.X1) / cell - 0.5) + 1; i++)
        for (var j = (int)Math.Floor(Math.Min(w.Z0, w.Z1) / cell - 0.5) - 1; j <= (int)Math.Floor(Math.Max(w.Z0, w.Z1) / cell - 0.5) + 1; j++)
        {
            float x = (i + 0.5f) * cell, z = (j + 0.5f) * cell;
            var t = length > 1e-12f ? Math.Clamp(((x - w.X0) * wx + (z - w.Z0) * wz) / length, 0, 1) : 0;
            if (MathF.Sqrt(MathF.Pow(w.X0 + wx * t - x, 2) + MathF.Pow(w.Z0 + wz * t - z, 2)) < Tight) tight.Add((i, j));
            foreach (var (di, dj) in Steps)
            {
                float sx = di * cell, sz = dj * cell, cross = sx * wz - sz * wx;
                if (Math.Abs(cross) <= 1e-12f) continue;
                var s = ((w.X0 - x) * wz - (w.Z0 - z) * wx) / cross;
                var u = ((w.X0 - x) * sz - (w.Z0 - z) * sx) / cross;
                if (s is < 0 or > 1 || u is < 0 or > 1) continue;
                var key = new StepKey(i, j, di, dj);
                var levels = barred.TryGetValue(key, out var l) ? l : barred[key] = [];
                if (levels.All(level => Math.Abs(w.Y - level) > Merge)) levels.Add(w.Y);
            }
        }
    }

    /// <summary>Dạng lưu file: dấu lưới + cỡ ô + các dòng [i, j, độ cao...] / [i, j, di, dj, độ cao...].</summary>
    private sealed record Stored(string Mark, float Cell, List<float[]> Ground, List<float[]> Barred, List<int[]> Tight);

    /// <summary>Lưu kèm dấu nhận dạng lưới (game đổi lưới thì dựng lại).</summary>
    public void Save(string path, string mark) => JsonStore.Save(path, new Stored(mark, Cell,
        Ground.Select(p => new float[] { p.Key.I, p.Key.J }.Concat(p.Value.Select(v => MathF.Round(v, 2))).ToArray()).ToList(),
        Barred.Select(p => new float[] { p.Key.I, p.Key.J, p.Key.Di, p.Key.Dj }.Concat(p.Value.Select(v => MathF.Round(v, 2))).ToArray()).ToList(),
        TightCells.Select(c => new[] { c.I, c.J }).ToList()));

    /// <summary>(dấu lưới, địa hình) từ file; chưa có / hỏng / khác cỡ ô thì ("", null).</summary>
    public static (string Mark, Terrain? Terrain) Load(string path, float cell)
    {
        var data = JsonStore.Load<Stored?>(path, null);
        if (data == null || data.Cell != cell || string.IsNullOrEmpty(data.Mark)) return ("", null);
        return (data.Mark, new Terrain(cell,
            data.Ground.ToDictionary(r => ((int)r[0], (int)r[1]), r => r[2..]),
            data.Barred.ToDictionary(r => new StepKey((int)r[0], (int)r[1], (int)r[2], (int)r[3]), r => r[4..]),
            data.Tight.Select(r => (r[0], r[1])).ToHashSet()));
    }
}
