using DTA.Game.Geometry;
using DTA.Runtime.Core;
using DTA.Runtime.Memory;

namespace DTA.Game.World;

/// <summary>
/// Đường tới dữ liệu vị trí của 1 Transform. Engine lưu cả cây Transform thành 2 mảng: TRS cục bộ của từng nút (0x30 byte) và
/// chỉ số nút cha. Head = {cây*, chỉ số nút} (12 byte); Table = {sức chứa i32, bộ đếm i32, mảng TRS*, mảng cha*}; Chain = nút -> gốc.
/// </summary>
public sealed record TransformTrack(byte[] Head, byte[] Table, int[] Chain)
{
    public long Hierarchy => Bin.U64(Head, 0);
    public int Capacity => Bin.I32(Table, 0);
    public long Nodes => Bin.U64(Table, 8);
    public long Parents => Bin.U64(Table, 16);
}

/// <summary>Đọc và ghép vị trí thế giới từ cây Transform native - hàm thuần, dùng chung cho mọi reader.</summary>
public static class TransformMath
{
    public const int NodeSize = 0x30;
    private const int MaxDepth = 64;

    /// <summary>Dò đường tới dữ liệu vị trí của <paramref name="transform"/>; null nếu đọc không được.</summary>
    public static TransformTrack? Track(MemoryChannel memory, long transform, int hierarchyOffset)
    {
        var head = transform != 0 ? memory.Read(transform + hierarchyOffset, 12) : null;
        var hierarchy = head != null ? Bin.U64(head, 0) : 0;
        var table = hierarchy != 0 ? memory.Read(hierarchy + 0x10, 0x18) : null;
        if (table == null) return null;
        int index = Bin.I32(head!, 8), capacity = Bin.I32(table, 0);
        var parents = index >= 0 && index < capacity && capacity <= 1_000_000 ? memory.Read(Bin.U64(table, 16), 4 * capacity) : null;
        return parents != null ? new TransformTrack(head!, table, Chain(parents, index)) : null;
    }

    /// <summary>Dãy chỉ số nút từ <paramref name="index"/> lên tới gốc cây theo mảng chỉ số cha.</summary>
    public static int[] Chain(byte[] parents, int index)
    {
        var capacity = parents.Length / 4;
        var chain = new List<int> { index };
        while (chain.Count < MaxDepth)
        {
            var parent = Bin.I32(parents, 4 * chain[^1]);
            if (parent < 0 || parent >= capacity) break;
            chain.Add(parent);
        }
        return [.. chain];
    }

    /// <summary>
    /// Tư thế thế giới của nút đầu dãy <paramref name="nodes"/> (nút -> gốc). Mỗi nút: vị trí (x,y,z,-), quaternion, tỉ lệ (x,y,z,-);
    /// lần lượt đổi sang hệ nút cha: nhân tỉ lệ, xoay, cộng vị trí; hướng = xoay cha * xoay đang có.
    /// </summary>
    public static Pose Compose(IReadOnlyList<byte[]> nodes)
    {
        Vec3 position = default;
        Quat rotation = Quat.Identity;
        for (var i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            var t = new Vec3(Bin.F32(n, 0), Bin.F32(n, 4), Bin.F32(n, 8));
            var q = new Quat(Bin.F32(n, 16), Bin.F32(n, 20), Bin.F32(n, 24), Bin.F32(n, 28));
            if (i == 0)
            {
                (position, rotation) = (t, q);
                continue;
            }
            var scaled = new Vec3(position.X * Bin.F32(n, 32), position.Y * Bin.F32(n, 36), position.Z * Bin.F32(n, 40));
            var r = q.Rotate(scaled);
            position = new Vec3(r.X + t.X, r.Y + t.Y, r.Z + t.Z);
            rotation = q.Multiply(rotation);
        }
        return new Pose(position, rotation);
    }

    /// <summary>Tư thế thế giới theo đường đã dò (1 lượt đọc các nút); null nếu thiếu nút.</summary>
    public static Pose? Pose(MemoryChannel memory, TransformTrack track)
    {
        var data = memory.ReadMany(track.Chain.Select(i => (track.Nodes + NodeSize * (long)i, NodeSize)).ToList());
        return data.Any(d => d == null) ? null : Compose(data!);
    }

    /// <summary>Vị trí thế giới của 1 Transform (dò đường + đọc); null nếu không đọc được.</summary>
    public static Vec3? Position(MemoryChannel memory, long transform, int hierarchyOffset) =>
        Track(memory, transform, hierarchyOffset) is { } track ? Pose(memory, track)?.Position : null;
}
