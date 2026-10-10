using DTA.Game.World;

namespace DTA.Engine.Movement;

/// <summary>Kết quả tìm đường: dãy ô, giá, cả đường có chắc chắn đi được không.</summary>
public sealed record Route(List<(int I, int J)> Cells, float Cost, bool Sure);

/// <summary>Phần tìm đường A* của <see cref="NavMap"/>.</summary>
public sealed partial class NavMap
{
    public const float SafeRatio = 2.5f, SafeExtra = 15f;
    private const int Pad = 40, Limit = 90000;
    private static readonly (int Di, int Dj, float Length)[] Steps =
        [(1, 0, 1f), (-1, 0, 1f), (0, 1, 1f), (0, -1, 1f), (1, 1, 1.4142f), (1, -1, 1.4142f), (-1, 1, 1.4142f), (-1, -1, 1.4142f)];

    private readonly Dictionary<(int I, int J, int Layer), List<((int, int, int) Node, float Cost, bool Sure)>> _links = [];
    private float? _base;

    /// <summary>
    /// Bước đi được từ nút (ô, tầng; -1 = không có đất ở độ cao đang đi): giá = độ dài (× nếu ô tới lạ), + leo dốc, + sát tường, + cắt mép
    /// địa hình (về lại lưới từ chỗ lưới không phủ thì phạt nhẹ - lưới hay vẽ thiếu), + cạnh đã học chặn / đang nghi. Chắc chắn = ô tới đã
    /// biết đi được và không cắt thứ gì kể trên.
    /// </summary>
    private List<((int, int, int), float, bool)> LinksOf((int I, int J, int Layer) node)
    {
        var levels = Terrain != null && Terrain.Ground.TryGetValue((node.I, node.J), out var l) ? l : null;
        var height = levels != null && node.Layer >= 0 ? levels[node.Layer] : _base;
        var links = new List<((int, int, int), float, bool)>(8);
        foreach (var (di, dj, length) in Steps)
        {
            (int I, int J) cell = (node.I + di, node.J + dj);
            var sure = _free.Contains(cell);
            int near;
            float cost;
            if (Terrain == null) (near, cost) = (0, sure ? length : length * Unknown);
            else
            {
                near = Layer(cell, height, Rise * length);
                sure |= near >= 0;
                cost = sure ? length : length * Off;
                if (near >= 0 && height is { } h) cost += Math.Abs(Terrain.Ground[cell][near] - h) * Climb;
                if (Terrain.TightCells.Contains(cell)) cost += Snug;
                if (!_passed.Contains(Terrain.Key((node.I, node.J), cell)) && Terrain.Bars((node.I, node.J), cell, height))
                {
                    cost += node.Layer < 0 && near >= 0 ? Return : Barrier;
                    sure = false;
                }
            }
            foreach (var edge in Crossing((node.I, node.J), cell))
            {
                if (_walls.TryGetValue(edge, out var hits) && hits > 0)
                {
                    cost += _shut.Contains(edge) ? WallMax : Math.Min(Wall * MathF.Pow(2, hits - 1), WallMax);
                    sure = false;
                }
                else if (_suspects.TryGetValue(edge, out var extra))
                {
                    cost += extra;
                    sure = false;
                }
            }
            links.Add(((cell.I, cell.J, near), cost, sure));
        }
        return links;
    }

    /// <summary>
    /// Đường ngắn nhất (A*) từ ô <paramref name="start"/> tới ô đầu tiên cách đích ≤ <paramref name="accept"/> m mà với thẳng tới đích
    /// được. Chỗ chặn tính như quãng vòng rất dài chứ không cấm. <paramref name="safe"/> = chỉ bước chắc chắn. <paramref name="bound"/>:
    /// biết chắc không có đường rẻ hơn thì thôi tìm (null).
    /// </summary>
    public Route? Find((int I, int J) start, (float X, float Z) target, float accept, float? height = null,
                       IReadOnlyDictionary<(int, int), float>? extra = null, bool safe = false, float bound = float.PositiveInfinity)
    {
        var goal = Cell(target.X, target.Z);
        int lowI = Math.Min(start.I, goal.I), lowJ = Math.Min(start.J, goal.J), highI = Math.Max(start.I, goal.I), highJ = Math.Max(start.J, goal.J);
        if (Terrain != null) (lowI, lowJ, highI, highJ) = (Math.Min(lowI, Terrain.Low.I), Math.Min(lowJ, Terrain.Low.J), Math.Max(highI, Terrain.High.I), Math.Max(highJ, Terrain.High.J));
        (lowI, lowJ, highI, highJ) = (lowI - Pad, lowJ - Pad, highI + Pad, highJ + Pad);
        if (Terrain != null && (_base == null || height == null || Math.Abs(_base.Value - height.Value) > 0.5f))
            foreach (var node in _links.Keys.Where(n => n.Layer < 0).ToList()) _links.Remove(node);
        _base = height;
        (int, int, int) first = (start.I, start.J, Terrain != null ? Layer(start, height, Same) : 0);
        var best = new Dictionary<(int, int, int), float> { [first] = 0 };
        var came = new Dictionary<(int, int, int), (int, int, int)>();
        var risky = new HashSet<(int, int, int)>();
        var heap = new PriorityQueue<((int I, int J, int L) Node, float Cost), float>();
        heap.Enqueue((first, 0), 0);
        var seen = 0;
        while (heap.TryDequeue(out var item, out var least) && seen < Limit)
        {
            if (least >= bound + accept) return null;
            var (node, cost) = item;
            if (cost > best[node]) continue;
            seen++;
            if ((node.I, node.J) == goal || (Dist((node.I + 0.5f) * CellSize, (node.J + 0.5f) * CellSize, target.X, target.Z) <= accept && Reaches((node.I, node.J), target)))
            {
                var path = new List<(int, int, int)> { node };
                while (came.TryGetValue(path[^1], out var back)) path.Add(back);
                path.Reverse();
                return new Route(path.Select(n => (n.Item1, n.Item2)).ToList(), cost, !path.Any(risky.Contains));
            }
            if (!_links.TryGetValue(node, out var steps)) _links[node] = steps = LinksOf(node);
            foreach (var (next, price, sure) in steps)
            {
                if (next.Item1 < lowI || next.Item1 > highI || next.Item2 < lowJ || next.Item2 > highJ || (safe && !sure)) continue;
                var add = extra?.GetValueOrDefault((next.Item1, next.Item2)) ?? 0;
                if (float.IsPositiveInfinity(add)) add = float.IsPositiveInfinity(extra!.GetValueOrDefault((node.I, node.J))) ? 0 : add;
                if (float.IsPositiveInfinity(add)) continue;
                var step = cost + price + add;
                if (step >= best.GetValueOrDefault(next, float.PositiveInfinity)) continue;
                best[next] = step;
                came[next] = node;
                if (sure) risky.Remove(next); else risky.Add(next);
                int ax = Math.Abs(next.Item1 - goal.I), az = Math.Abs(next.Item2 - goal.J);
                heap.Enqueue((next, step), step + Math.Max(ax, az) + 0.4142f * Math.Min(ax, az));
            }
        }
        return null;
    }
}
