using System.Runtime.InteropServices;
using System.Text;
using DTA.Game.Session;
using DTA.Runtime.Core;
using DTA.Runtime.Memory;

namespace DTA.Game.World;

/// <summary>
/// Tìm class / object không có đường tham chiếu nào tới được (vd object vùng câu chỉ được game tìm bằng truy vấn vật lý): chép vùng nhớ về
/// máy (gzip trên giả lập + adb pull, ~37 MB/s - nhanh hơn hẳn xử lý từng byte trên giả lập) rồi quét bằng C#.
/// </summary>
public static class HeapScan
{
    private const long Batch = 128L << 20;
    private static readonly Logger L = Log.For("heap");

    /// <summary>
    /// Class theo tên (kể cả class lồng / code game không tham chiếu thẳng): chuỗi "\0tên\0" trong khối metadata -> Il2CppClass có ô tên
    /// (+0x10) trỏ đúng chuỗi đó, nằm cùng vùng cấp phát với các class đã biết. Chỉ class game đã khởi tạo mới có. 0 nếu không thấy.
    /// </summary>
    public static long FindClass(GameSession session, string name)
    {
        if (session.Il2Cpp.Metadata() is not { } metadata || ClassRegion(session) is not { } classes) return 0;
        var needle = Encoding.UTF8.GetBytes("\0" + name + "\0");
        var meta = session.Memory.Dump(session.Device, metadata.Start, metadata.End);
        var texts = new List<long>();
        for (var k = meta.AsSpan().IndexOf(needle); k >= 0 && texts.Count < 16; k = Next(meta, needle, k)) texts.Add(metadata.Start + k + 1);
        if (texts.Count == 0) return 0;
        var data = MemoryMarshal.Cast<byte, long>(session.Memory.Dump(session.Device, classes.Start, classes.End));
        for (var i = 0; i < data.Length; i++)
        {
            if (!texts.Contains(data[i])) continue;
            var klass = classes.Start + 8L * i - 0x10;
            session.Il2Cpp.LoadClasses([klass]);
            if (session.Il2Cpp.FullName(klass) is var full && (full == name || full.EndsWith("." + name, StringComparison.Ordinal))) return klass;
        }
        return 0;
    }

    /// <summary>
    /// Mọi object của các class <paramref name="classes"/> (con trỏ class ở đầu object, căn 8 byte) trong vùng cấp phát động không tên của
    /// game (trừ vùng chứa class), chép về theo lô ~128 MB: {class: [địa chỉ object]}.
    /// </summary>
    public static Dictionary<long, List<long>> FindObjects(GameSession session, IReadOnlyCollection<long> classes, Action<string>? progress = null)
    {
        var result = classes.Distinct().ToDictionary(c => c, _ => new List<long>());
        if (result.Count == 0) return result;
        var skip = ClassRegion(session);
        var regions = session.Memory.Regions().Where(r => r.Perms.StartsWith("rw") && r.Name.Length == 0 && (r.Start, r.End) != skip)
            .Select(r => (r.Start, r.End)).ToList();
        long total = regions.Sum(r => r.End - r.Start), done = 0;
        foreach (var group in Groups(regions))
        {
            List<byte[]> chunks;
            try
            {
                chunks = session.Memory.DumpRanges(session.Device, group);
            }
            catch (GameError e)
            {
                L.Debug($"Bỏ lô {group.Count} vùng không chép được: {e.Message}");
                continue;
            }
            for (var g = 0; g < group.Count; g++)
            {
                var words = MemoryMarshal.Cast<byte, long>(chunks[g]);
                for (var i = 0; i < words.Length; i++)
                    if (result.TryGetValue(words[i], out var list)) list.Add(group[g].Start + 8L * i);
            }
            done += group.Sum(r => r.End - r.Start);
            progress?.Invoke($"{done * 100 / Math.Max(1, total)}%");
        }
        L.Debug($"Quét {total >> 20} MB: {string.Join(", ", result.Select(p => $"0x{p.Key:X} x{p.Value.Count}"))}");
        return result;
    }

    /// <summary>Vùng cấp phát chứa các class đã biết (class điều khiển nhân vật); null nếu chưa có.</summary>
    private static (long Start, long End)? ClassRegion(GameSession session)
    {
        var klass = session.ControlClass;
        return session.Il2Cpp.Anon.FirstOrDefault(a => klass >= a.Start && klass < a.End) is { End: > 0 } r ? r : null;
    }

    /// <summary>Chia vùng thành lô ≤ 128 MB (vùng lớn hơn đứng riêng 1 lô).</summary>
    private static IEnumerable<List<(long Start, long End)>> Groups(List<(long Start, long End)> regions)
    {
        var group = new List<(long, long)>();
        long size = 0;
        foreach (var r in regions)
        {
            if (group.Count > 0 && size + (r.End - r.Start) > Batch)
            {
                yield return group;
                (group, size) = ([], 0);
            }
            group.Add(r);
            size += r.End - r.Start;
        }
        if (group.Count > 0) yield return group;
    }

    private static int Next(byte[] data, byte[] needle, int from)
    {
        var next = data.AsSpan(from + 1).IndexOf(needle);
        return next < 0 ? -1 : from + 1 + next;
    }
}
