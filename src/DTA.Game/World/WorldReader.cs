using System.Collections.Concurrent;
using DTA.Game.Geometry;
using DTA.Game.Session;
using DTA.Game.Ui;
using DTA.Runtime.Core;

namespace DTA.Game.World;

/// <summary>
/// Vị trí 3D của object game (nhân vật, bọ, quặng, nút không nền...) qua cây Transform native; theo dõi object đọc lặp để mỗi lần
/// chỉ tốn 1 lượt lệnh. Dùng chung theo phiên.
/// </summary>
public sealed partial class WorldReader(GameSession session)
{
    private const long TrackCheckMs = 500;
    private const int MaxTracks = 512;
    private static readonly Logger L = Log.For("world");

    /// <summary>Đường dữ liệu của 1 object đang theo dõi; Checked = lúc kiểm lại gần nhất (ms).</summary>
    private sealed class Tracked(byte[] native, long transform, TransformTrack track)
    {
        public byte[] Native { get; } = native;
        public long Transform { get; } = transform;
        public TransformTrack Track { get; } = track;
        public long Checked { get; set; } = Environment.TickCount64;
    }

    private readonly ConcurrentDictionary<long, Tracked> _tracks = new();

    /// <summary>
    /// Tư thế thế giới của GameObject chứa <paramref name="component"/>. <paramref name="track"/> = object đọc lặp (nhớ đường);
    /// <paramref name="guard"/> = (địa chỉ, 8 byte phải có) kiểm kèm cùng lượt - ô đổi thì trả null để nơi gọi xác định lại object.
    /// </summary>
    public Pose? Pose(long component, bool track = false, (long Addr, byte[] Expect)? guard = null)
    {
        if (component == 0 || Layout() is not { Supported: true } layout) return null;
        var memory = session.Memory;
        if (_tracks.TryGetValue(component, out var known))
        {
            var (requests, verify) = Plan(component, known, layout, TrackCheckMs);
            if (guard is { } g) requests.Insert(0, (g.Addr, 8));
            var data = memory.ReadMany(requests).ToList();
            if (guard is { } g2)
            {
                if (data[0] is not { } got || !got.AsSpan().SequenceEqual(g2.Expect)) return null;
                data.RemoveAt(0);
            }
            if (Resolve(known, data, verify) is { } pose) return pose;
            _tracks.TryRemove(component, out _);
        }
        var native = memory.Read(component + layout.Cached, 8);
        if (native == null) return null;
        var ptr = Bin.U64(native, 0);
        var gameObject = session.Managed.Names(component).Contains(UiClasses.GameObject) ? ptr : ptr != 0 ? (long)memory.U64(ptr + layout.ComponentGo) : 0;
        var transform = gameObject != 0 ? (long)memory.U64((long)memory.U64(gameObject + layout.GoComponents) + 8) : 0;
        var found = transform != 0 ? TransformMath.Track(memory, transform, layout.Hierarchy) : null;
        var result = found != null ? TransformMath.Pose(memory, found) : null;
        if (result == null || !track) return result;
        if (_tracks.Count >= MaxTracks)
        {
            L.Debug($"Quá {MaxTracks} object theo dõi - xoá hết");
            _tracks.Clear();
        }
        _tracks[component] = new Tracked(native, transform, found!);
        return result;
    }

    /// <summary>Vị trí thế giới của GameObject chứa <paramref name="component"/> (xem <see cref="Pose"/>).</summary>
    public Vec3? Position(long component, bool track = false) => Pose(component, track)?.Position;

    /// <summary>Tư thế của nhiều object trong 1 lượt lệnh; object chưa theo dõi / đổi đường thì dò lại riêng. <paramref name="checkMs"/> = chu kỳ kiểm đường.</summary>
    public Dictionary<long, Pose> Poses(IEnumerable<long> components, long checkMs = 3000)
    {
        var result = new Dictionary<long, Pose>();
        if (Layout() is not { Supported: true } layout) return result;
        var list = components.Where(c => c != 0).Distinct().ToList();
        var plans = list.Select(c => _tracks.TryGetValue(c, out var k) ? (c, k, Plan(c, k, layout, checkMs)) : default)
            .Where(p => p.k != null).ToList();
        var data = plans.Count > 0 ? session.Memory.ReadMany(plans.SelectMany(p => p.Item3.Requests).ToList()) : [];
        var start = 0;
        foreach (var (component, known, (requests, verify)) in plans)
        {
            var slice = data.Skip(start).Take(requests.Count).ToList();
            start += requests.Count;
            if (Resolve(known, slice, verify) is { } pose) result[component] = pose;
            else _tracks.TryRemove(component, out _);
        }
        foreach (var component in list.Where(c => !result.ContainsKey(c)))
            if (Pose(component, true) is { } pose) result[component] = pose;
        return result;
    }

    /// <summary>Vùng nhớ cần đọc cho 1 object đang theo dõi; quá chu kỳ thì đọc kèm các ô xác định đường đi để kiểm.</summary>
    private static (List<(long Addr, int Size)> Requests, bool Verify) Plan(long component, Tracked known, NativeLayout layout, long checkMs)
    {
        var t = known.Track;
        var verify = Environment.TickCount64 - known.Checked > checkMs;
        var requests = new List<(long, int)>();
        if (verify)
        {
            requests.Add((component + layout.Cached, 8));
            requests.Add((known.Transform + layout.Hierarchy, 12));
            requests.Add((t.Hierarchy + 0x10, 0x18));
            requests.AddRange(t.Chain.Select(i => (t.Parents + 4L * i, 4)));
        }
        requests.AddRange(t.Chain.Select(i => (t.Nodes + TransformMath.NodeSize * (long)i, TransformMath.NodeSize)));
        return (requests, verify);
    }

    /// <summary>
    /// Tư thế từ dữ liệu theo <see cref="Plan"/>; null nếu đường đã đổi. Bảng cây so trừ ô bộ đếm (byte 4-8, Unity đổi liên tục) -
    /// so cả ô đó thì lần kiểm nào cũng "đổi" và mọi object bị dò lại.
    /// </summary>
    private static Pose? Resolve(Tracked known, IReadOnlyList<byte[]?> data, bool verify)
    {
        if (data.Any(d => d == null)) return null;
        var t = known.Track;
        if (verify)
        {
            var table = data[2]!;
            var parents = data.Skip(3).Take(t.Chain.Length).Select(d => Bin.I32(d!, 0)).ToArray();
            var sameTable = table.AsSpan(0, 4).SequenceEqual(t.Table.AsSpan(0, 4)) && table.AsSpan(8).SequenceEqual(t.Table.AsSpan(8));
            var sameChain = parents[..^1].SequenceEqual(t.Chain[1..]);
            var rootEnds = t.Chain.Length >= 64 || parents[^1] < 0 || parents[^1] >= t.Capacity;
            if (!data[0]!.AsSpan().SequenceEqual(known.Native) || !data[1]!.AsSpan().SequenceEqual(t.Head) || !sameTable || !sameChain || !rootEnds) return null;
            known.Checked = Environment.TickCount64;
        }
        return TransformMath.Compose(data.Skip(data.Count - t.Chain.Length).ToList()!);
    }
}
