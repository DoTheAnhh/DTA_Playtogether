using DTA.Game.Geometry;
using DTA.Runtime.Core;

namespace DTA.Game.World;

/// <summary>1 mặt lưới NavMesh đang bật: chỗ đặt (vị trí + xoay) + dữ liệu từng ô.</summary>
public sealed record NavSurface(Vec3 Position, Quat Rotation, List<byte[]> Tiles);

/// <summary>Mép lưới (tường / vực) trên mặt đất: đoạn (x0,z0)-(x1,z1) ở độ cao Y.</summary>
public readonly record struct Wall(float X0, float Z0, float X1, float Z1, float Y);

/// <summary>
/// Giải mã ô dữ liệu NavMesh của Unity (Detour). Đầu ô 72 byte: "VAND", phiên bản 16, toạ độ ô, số đa giác / đỉnh / mesh chi tiết /
/// đỉnh chi tiết / tam giác chi tiết / nút BV. Đa giác 32 byte: 6 đỉnh + 6 hàng xóm (u16), cờ, số đỉnh, loại vùng. Hàng xóm 0 = mép
/// lưới, có bit 0x8000 = cạnh trên biên ô (4 bit thấp là biên nào). Ô sai định dạng thì báo lỗi, không đoán.
/// </summary>
public static class NavMesh
{
    public static readonly byte[] Magic = "VAND"u8.ToArray();
    public const int Version = 16, Header = 72;
    private const int Poly = 32, Vert = 12, DetailMesh = 12, DetailVert = 12, DetailTri = 8, BvNode = 16, Border = 0x8000;
    private const float Join = 0.6f;
    private static readonly Dictionary<int, (int X, int Y)> Sides = new() { [0] = (1, 0), [2] = (0, 1), [4] = (-1, 0), [6] = (0, -1) };

    /// <summary>Thông số đầu ô: (phiên bản, ô x, ô y, số đa giác, số đỉnh, mesh chi tiết, đỉnh chi tiết, tam giác chi tiết, nút BV).</summary>
    private static (uint Version, int X, int Y, int Polys, int Verts, int Meshes, int DVerts, int DTris, int Nodes) Head(byte[] t) =>
        (Bin.U32(t, 4), Bin.I32(t, 8), Bin.I32(t, 12), Bin.I32(t, 20), Bin.I32(t, 24), Bin.I32(t, 28), Bin.I32(t, 32), Bin.I32(t, 36), Bin.I32(t, 40));

    /// <summary>Ô có đúng dấu nhận dạng.</summary>
    public static bool IsTile(byte[] tile) => tile.Length >= Header && tile.AsSpan(0, 4).SequenceEqual(Magic);

    /// <summary>Toạ độ lưới -> bản đồ (xoay + dời theo chỗ đặt mặt lưới; mặt đặt ở gốc thì giữ nguyên).</summary>
    private static Func<Vec3, Vec3> Placement(NavSurface s)
    {
        var placed = Math.Abs(s.Rotation.W) < 0.9999f || Math.Max(Math.Abs(s.Position.X), Math.Max(Math.Abs(s.Position.Y), Math.Abs(s.Position.Z))) > 1e-3f;
        return placed ? v => s.Rotation.Rotate(v) is var r ? new Vec3(r.X + s.Position.X, r.Y + s.Position.Y, r.Z + s.Position.Z) : v : v => v;
    }

    private static Vec3 VertAt(byte[] tile, int start, int index) =>
        new(Bin.F32(tile, start + 12 * index), Bin.F32(tile, start + 12 * index + 4), Bin.F32(tile, start + 12 * index + 8));

    /// <summary>Các đa giác (dãy đỉnh) và mép lưới (tường) trong toạ độ bản đồ; sai định dạng thì ném <see cref="FormatException"/>.</summary>
    public static (List<Vec3[]> Polys, List<Wall> Walls) Mesh(IEnumerable<NavSurface> surfaces)
    {
        var polys = new List<Vec3[]>();
        var walls = new List<Wall>();
        foreach (var surface in surfaces)
        {
            var world = Placement(surface);
            var edges = new List<(Vec3 A, Vec3 B)>();
            var borders = new Dictionary<((int, int) Tile, int Side), List<(Vec3 A, Vec3 B)>>();
            foreach (var tile in surface.Tiles)
            {
                if (!IsTile(tile)) throw new FormatException("ô NavMesh sai dấu nhận dạng");
                var h = Head(tile);
                var size = Header + Vert * h.Verts + Poly * h.Polys + DetailMesh * h.Meshes + DetailVert * h.DVerts + DetailTri * h.DTris + BvNode * h.Nodes;
                if (h.Version != Version || size != tile.Length) throw new FormatException("ô NavMesh khác định dạng đã biết");
                var polyStart = Header + Vert * h.Verts;
                for (var p = 0; p < h.Polys; p++)
                {
                    var at = polyStart + Poly * p;
                    int count = tile[at + 28];
                    var idx = Enumerable.Range(0, count).Select(k => Bin.U16(tile, at + 2 * k)).ToArray();
                    if (count is < 3 or > 6 || idx.Max() >= h.Verts) throw new FormatException("đa giác NavMesh không hợp lệ");
                    var corners = idx.Select(v => VertAt(tile, Header, v)).ToArray();
                    polys.Add(corners.Select(world).ToArray());
                    for (var k = 0; k < count; k++)
                    {
                        var neighbour = Bin.U16(tile, at + 12 + 2 * k);
                        var edge = (corners[k], corners[(k + 1) % count]);
                        if (neighbour == 0) edges.Add(edge);
                        else if ((neighbour & Border) != 0)
                        {
                            var key = ((h.X, h.Y), (int)(neighbour & 0xF));
                            (borders.TryGetValue(key, out var list) ? list : borders[key] = []).Add(edge);
                        }
                    }
                }
            }
            edges.AddRange(UnjoinedBorders(borders));
            walls.AddRange(edges.Select(e => (world(e.A), world(e.B)) is var (a, b) ? new Wall(a.X, a.Z, b.X, b.Z, (a.Y + b.Y) / 2) : default));
        }
        return (polys, walls);
    }

    /// <summary>Cạnh trên biên ô không có cạnh ô kề chồng lên ở cùng độ cao (lệch ≤ 0,6 m) = mép lưới.</summary>
    private static IEnumerable<(Vec3, Vec3)> UnjoinedBorders(Dictionary<((int, int) Tile, int Side), List<(Vec3 A, Vec3 B)>> borders)
    {
        foreach (var ((tile, side), mine) in borders)
        {
            var hasShift = Sides.TryGetValue(side, out var shift);
            var others = hasShift && borders.TryGetValue(((tile.Item1 + shift.X, tile.Item2 + shift.Y), (side + 4) % 8), out var o) ? o : [];
            Func<Vec3, float> axis = hasShift && shift.X != 0 ? v => v.Z : v => v.X;
            foreach (var (a, b) in mine)
            {
                float low = Math.Min(axis(a), axis(b)), high = Math.Max(axis(a), axis(b));
                var joined = others.Any(other =>
                {
                    var (c, d) = other;
                    float start = Math.Max(low, Math.Min(axis(c), axis(d))), end = Math.Min(high, Math.Max(axis(c), axis(d)));
                    if (end - start <= 0.05f) return false;
                    var middle = (start + end) / 2;
                    var here = a.Y + (b.Y - a.Y) * (middle - axis(a)) / (axis(b) - axis(a));
                    var there = c.Y + (d.Y - c.Y) * (middle - axis(c)) / (axis(d) - axis(c));
                    return Math.Abs(here - there) <= Join;
                });
                if (!joined) yield return (a, b);
            }
        }
    }

    /// <summary>
    /// Mặt đất CHÍNH XÁC (tam giác lưới chi tiết, sai số vài cm) để tính độ cao trước khi dịch chuyển - đa giác thô chỉ nội suy phẳng
    /// nên lệch ở đồi / bậc thềm -> độn thổ. Tam giác chi tiết 8 byte: thử 4×u16 rồi 4×u8; ô không đọc được thì chia quạt đa giác thô.
    /// </summary>
    public static List<Vec3[]> SurfaceTris(IEnumerable<NavSurface> surfaces)
    {
        var tris = new List<Vec3[]>();
        foreach (var surface in surfaces)
        {
            var world = Placement(surface);
            foreach (var tile in surface.Tiles.Where(IsTile))
            {
                var h = Head(tile);
                var polyStart = Header + Vert * h.Verts;
                var polys = Enumerable.Range(0, h.Polys).Select(p =>
                {
                    var at = polyStart + Poly * p;
                    return Enumerable.Range(0, tile[at + 28]).Select(k => Bin.U16(tile, at + 2 * k)).ToArray();
                }).ToList();
                var meshStart = polyStart + Poly * h.Polys;
                var dvertStart = meshStart + DetailMesh * h.Meshes;
                var dtriStart = dvertStart + DetailVert * h.DVerts;
                var details = Enumerable.Range(0, Math.Min(h.Meshes, polys.Count))
                    .Select(k => (VBase: (int)Bin.U32(tile, meshStart + 12 * k), TBase: (int)Bin.U32(tile, meshStart + 12 * k + 4), VCount: (int)tile[meshStart + 12 * k + 8], TCount: (int)tile[meshStart + 12 * k + 9]))
                    .ToList();
                var tileTris = DetailTris(tile, h.Verts, h.DVerts, h.DTris, dvertStart, dtriStart, polys, details, world, true)
                               ?? DetailTris(tile, h.Verts, h.DVerts, h.DTris, dvertStart, dtriStart, polys, details, world, false)
                               ?? polys.SelectMany(p => Fan(p.Select(v => world(VertAt(tile, Header, v))).ToArray())).ToList();
                tris.AddRange(tileTris);
            }
        }
        return tris;
    }

    /// <summary>Tam giác lưới chi tiết của 1 ô theo kiểu chỉ số (u16 / u8); null nếu kiểu đó không khớp dữ liệu.</summary>
    private static List<Vec3[]>? DetailTris(byte[] tile, int verts, int dverts, int dtris, int dvertStart, int dtriStart, List<int[]> polys,
                                            List<(int VBase, int TBase, int VCount, int TCount)> details, Func<Vec3, Vec3> world, bool wide)
    {
        var found = new List<Vec3[]>();
        for (var index = 0; index < details.Count; index++)
        {
            var (vbase, tbase, vcount, tcount) = details[index];
            var poly = polys[index];
            if (tbase + tcount > dtris || vbase + vcount > dverts) return null;
            for (var t = tbase; t < tbase + tcount; t++)
            {
                var at = dtriStart + DetailTri * t;
                var corners = Enumerable.Range(0, 3).Select(k => wide ? Bin.U16(tile, at + 2 * k) : tile[at + k]).ToArray();
                if (corners.Max() >= poly.Length + vcount) return null;
                found.Add(corners.Select(c => world(c < poly.Length ? VertAt(tile, Header, poly[c]) : VertAt(tile, dvertStart, vbase + c - poly.Length))).ToArray());
            }
        }
        return found.Count > 0 ? found : null;
    }

    /// <summary>Chia quạt đa giác lồi thành tam giác (đỉnh 0, k, k+1).</summary>
    public static IEnumerable<Vec3[]> Fan(Vec3[] corners) => Enumerable.Range(1, Math.Max(0, corners.Length - 2)).Select(k => new[] { corners[0], corners[k], corners[k + 1] });

    /// <summary>Điểm (x, z) nằm trong đa giác lồi (nhìn từ trên xuống), kể cả trên cạnh.</summary>
    public static bool Inside(Vec3[] verts, float x, float z)
    {
        var sign = 0f;
        for (var k = 0; k < verts.Length; k++)
        {
            Vec3 a = verts[k], b = verts[(k + 1) % verts.Length];
            var cross = (b.X - a.X) * (z - a.Z) - (b.Z - a.Z) * (x - a.X);
            if (cross is <= 1e-4f and >= -1e-4f) continue;
            if (sign != 0 && cross > 0 != sign > 0) return false;
            sign = cross;
        }
        return true;
    }

    /// <summary>Độ cao mặt đa giác tại (x, z): nội suy trong tam giác (0, k, k+1) chứa điểm.</summary>
    public static float Height(Vec3[] verts, float x, float z)
    {
        var a = verts[0];
        for (var k = 1; k < verts.Length - 1; k++)
        {
            Vec3 b = verts[k], c = verts[k + 1];
            var det = (b.Z - c.Z) * (a.X - c.X) + (c.X - b.X) * (a.Z - c.Z);
            if (Math.Abs(det) <= 1e-9f) continue;
            var u = ((b.Z - c.Z) * (x - c.X) + (c.X - b.X) * (z - c.Z)) / det;
            var v = ((c.Z - a.Z) * (x - c.X) + (a.X - c.X) * (z - c.Z)) / det;
            if (u >= -1e-3f && v >= -1e-3f && u + v <= 1.001f) return u * a.Y + v * b.Y + (1 - u - v) * c.Y;
        }
        return verts.Average(v => v.Y);
    }
}
