using DTA.Game.World;

namespace DTA.Engine.Movement;

/// <summary>Phần tự học khi đi của <see cref="NavMap"/>.</summary>
public sealed partial class NavMap
{
    /// <summary>Vừa biết thêm quanh ô (i, j): bỏ giá các bước đã tính ở vùng đó.</summary>
    private void Forget(int i, int j)
    {
        if (_links.Count == 0) return;
        for (var a = i - 2; a <= i + 2; a++)
        for (var b = j - 2; b <= j + 2; b++)
        {
            var layers = Terrain != null && Terrain.Ground.TryGetValue((a, b), out var levels) ? levels.Length : 1;
            for (var layer = -1; layer < layers; layer++) _links.Remove((a, b, layer));
        }
    }

    /// <summary>
    /// Nhân vật (độ cao <paramref name="height"/>) vừa sang ô <paramref name="cell"/> từ <paramref name="previous"/>: đứng trên đất thì ô đi
    /// được; cạnh từng ghi chặn mà đi bộ qua được thì gỡ, nhảy qua được thì thành "phải nhảy"; bước cắt mép địa hình vẫn qua được thì thông.
    /// </summary>
    public void Visit((int I, int J)? previous, (int I, int J) cell, bool airborne = false, float? height = null)
    {
        if (!airborne && _free.Add(cell)) _changed = true;
        if (previous is not { } p || Math.Max(Math.Abs(p.I - cell.I), Math.Abs(p.J - cell.J)) != 1) return;
        var straight = p.I == cell.I || p.J == cell.J;
        if (straight)
        {
            var edge = EdgeOf(p, cell);
            if (_suspects.Remove(edge))
            {
                _changed = true;
                Forget(cell.I, cell.J);
            }
            if (_walls.Remove(edge))
            {
                (airborne ? _jumps : _opened).Add(edge);
                _changed = true;
                Forget(cell.I, cell.J);
            }
        }
        var key = Terrain.Key(p, cell);
        if (Terrain == null || height == null || _passed.Contains(key) || !Terrain.Bars(p, cell, height)) return;
        _passed.Add(key);
        if (airborne && straight) _jumps.Add(EdgeOf(p, cell));
        _changed = true;
        Forget(cell.I, cell.J);
    }

    /// <summary>Ghi nhớ: ô a không sang được ô b kề cạnh. Cạnh thẳng hàng 2 bên bị nghi cùng bức tường (đã có tường gần thì nghi xa + nặng hơn).</summary>
    public void Block((int I, int J) a, (int I, int J) b)
    {
        var edge = EdgeOf(a, b);
        var along = edge.Axis == 0 ? (0, 1) : (1, 0);
        var lined = new[] { -3, -2, -1, 1, 2, 3 }.Any(side => _walls.ContainsKey(new Edge(edge.I + along.Item1 * side, edge.J + along.Item2 * side, edge.Axis)));
        _walls[edge] = _walls.GetValueOrDefault(edge) + 1;
        _days[edge] = Today;
        _shut.Add(edge);
        _jumps.Remove(edge);
        _opened.Remove(edge);
        _passed.Remove(Terrain.Key(a, b));
        _changed = true;
        var weights = lined ? Line : Suspect;
        for (var offset = 1; offset <= weights.Length; offset++)
            foreach (var side in new[] { offset, -offset })
            {
                var near = new Edge(edge.I + along.Item1 * side, edge.J + along.Item2 * side, edge.Axis);
                _suspects[near] = Math.Max(_suspects.GetValueOrDefault(near), weights[offset - 1]);
            }
        Forget(a.I, a.J);
        Forget(b.I, b.J);
    }

    /// <summary>Bắt đầu lần đi mới: cạnh cấm hẳn trong lần trước trở lại là tường thường.</summary>
    public void Reopen()
    {
        foreach (var e in _shut) Forget(e.I, e.J);
        _shut.Clear();
    }

    /// <summary>Đường có cắt cạnh vừa đụng trong lần đi này không (= không còn lối khác).</summary>
    public bool Shuts((int I, int J) start, IReadOnlyList<(float X, float Z)> path)
    {
        var cells = path.Select(p => Cell(p.X, p.Z)).Prepend(start).ToList();
        for (var k = 1; k < cells.Count; k++)
            if (Math.Max(Math.Abs(cells[k - 1].I - cells[k].I), Math.Abs(cells[k - 1].J - cells[k].J)) == 1 && Crossing(cells[k - 1], cells[k]).Any(_shut.Contains)) return true;
        return false;
    }

    /// <summary>Ghi nhớ: cạnh này phải nhảy mới qua.</summary>
    public void Jumped((int, int) a, (int, int) b)
    {
        if (_jumps.Add(EdgeOf(a, b))) _changed = true;
    }

    /// <summary>Nhân vật bị vây kín: vùng đi tới được không qua chỗ chặn đã biết nhỏ hơn 300 ô.</summary>
    public bool Enclosed((int I, int J) cell, float? height)
    {
        var seen = new HashSet<(int, int)> { cell };
        var queue = new Stack<(int I, int J)>([cell]);
        while (queue.Count > 0)
        {
            if (seen.Count >= EnclosedCells) return false;
            var here = queue.Pop();
            foreach (var (di, dj) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var next = (here.I + di, here.J + dj);
                if (seen.Contains(next) || _walls.ContainsKey(EdgeOf(here, next))) continue;
                if (Terrain != null && !_passed.Contains(Terrain.Key(here, next)) && Terrain.Bars(here, next, height)) continue;
                seen.Add(next);
                queue.Push(next);
            }
        }
        return true;
    }
}
