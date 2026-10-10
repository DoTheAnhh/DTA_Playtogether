using System.Buffers.Binary;
using DTA.Runtime.Core;
using DTA.Runtime.Device;
using DTA.Runtime.Memory;

namespace DTA.Runtime.Il2Cpp;

/// <summary>
/// Tìm Il2CppClass theo tên đầy đủ. Mã máy game giữ con trỏ tới từng class trong các ô cố định thuộc vùng dữ liệu libil2cpp.so;
/// ô đã nhớ (<see cref="Il2CppLayouts.Slots"/>) thì chỉ đọc 8 byte. Tên chưa có ô thì dò: chép vùng dữ liệu, gom con trỏ trỏ vào
/// vùng cấp phát động, chép các khối nhiều con trỏ nhất trước, đọc tên class trong khối metadata, dừng khi đủ.
/// </summary>
public static class ClassScanner
{
    private static readonly Logger L = Log.For("il2cpp");
    private const int Block = 1 << 20;
    private static readonly int[] Batches = [32, 32, 64, 128];

    /// <summary>
    /// Tìm class theo tên: đường nhanh đọc thẳng ô đã nhớ rồi kiểm bằng tên; tên chưa có ô thì dò. Bảng chuỗi tên class nằm gọn
    /// trong 1 vùng (vùng nhiều con trỏ tên trỏ vào nhất) nên chỉ chép đúng đoạn cần.
    /// </summary>
    public static Dictionary<string, long> FindClasses(Il2CppResolver il2cpp, EmulatorDevice device, IReadOnlyCollection<string> names,
                                                       bool scan, Action<string> progress)
    {
        var memory = il2cpp.Memory;
        var regions = memory.Regions();
        var anon = regions.Where(r => r.Perms.StartsWith("rw") && !r.Name.StartsWith('/')).Select(r => (r.Start, r.End)).Order().ToList();
        il2cpp.Anon = anon;
        var data = regions.Where(r => r.Name.EndsWith("/libil2cpp.so") && r.Perms.StartsWith("rw")).MaxBy(r => r.End - r.Start);
        if (data.End == 0) throw new GameError("Không tìm thấy libil2cpp của game (game chưa mở xong?)", true);

        var slots = il2cpp.Store.Slots;
        var found = new Dictionary<string, long>();
        var known = names.Where(slots.ContainsKey).ToList();
        if (known.Count > 0)
        {
            var values = memory.ReadMany(known.Select(n => (data.Start + slots[n], 8)).ToList());
            var klasses = values.Select(v => v == null ? 0L : (long)BinaryPrimitives.ReadUInt64LittleEndian(v)).ToList();
            il2cpp.LoadClasses(klasses);
            for (var i = 0; i < known.Count; i++) if (il2cpp.FullName(klasses[i]) == known[i]) found[known[i]] = klasses[i];
        }
        var missing = names.Where(n => !slots.ContainsKey(n)).ToHashSet();
        if (missing.Count == 0 || !scan) return found;

        progress("Đang dò cấu trúc game (chỉ lần đầu của mỗi phiên bản game)...");
        L.Info($"Dò class: {string.Join(", ", missing)}");
        var words = memory.Dump(device, data.Start, data.End);
        var starts = anon.Select(a => a.Start).ToArray();
        long low = anon[0].Start, high = anon[^1].End;
        var slotOf = new Dictionary<long, long>();
        var blocks = new Dictionary<long, List<long>>();
        for (var i = 0; i + 8 <= words.Length; i += 8)
        {
            var value = (long)BinaryPrimitives.ReadUInt64LittleEndian(words.AsSpan(i));
            if (value < low || value >= high || (value & 7) != 0 || slotOf.ContainsKey(value)) continue;
            var j = Upper(starts, value) - 1;
            if (j < 0 || value >= anon[j].End) continue;
            slotOf[value] = i;
            var block = anon[j].Start + (value - anon[j].Start) / Block * Block;
            (blocks.TryGetValue(block, out var list) ? list : blocks[block] = []).Add(value);
        }

        var order = blocks.Keys.OrderByDescending(b => blocks[b].Count).ToList();
        long textStart = 0;
        byte[] text = [];
        var done = 0;
        for (var index = 0; index <= Batches.Length && missing.Count > 0; index++)
        {
            var batch = (index < Batches.Length ? order.Skip(done).Take(Batches[index]) : order.Skip(done)).Order().ToList();
            done += batch.Count;
            if (batch.Count == 0) break;
            var ranges = new List<(long Start, long End)>();
            foreach (var block in batch)
            {
                var end = Math.Min(block + Block + 4096, anon[Upper(starts, block) - 1].End);
                if (ranges.Count > 0 && block <= ranges[^1].End) ranges[^1] = (ranges[^1].Start, Math.Max(ranges[^1].End, end));
                else ranges.Add((block, end));
            }
            var chunks = memory.DumpRanges(device, ranges);
            var rangeStarts = ranges.Select(r => r.Start).ToArray();
            var classes = new List<(long Klass, long NamePtr, long NsPtr)>();
            foreach (var block in batch)
            {
                var k = Upper(rangeStarts, block) - 1;
                var blob = chunks[k];
                foreach (var value in blocks[block])
                {
                    var offset = (int)(value - ranges[k].Start);
                    if (offset + Il2CppResolver.HeaderSize > blob.Length) continue;
                    if ((long)Il2CppResolver.U64(blob, offset + Il2CppResolver.Self) != value) continue;
                    classes.Add((value, (long)Il2CppResolver.U64(blob, offset + Il2CppResolver.Name), (long)Il2CppResolver.U64(blob, offset + Il2CppResolver.Namespace)));
                }
            }
            if (classes.Count == 0) continue;
            var pointers = classes.SelectMany(c => new[] { c.NamePtr, c.NsPtr }).Where(p => p >= low && p < high).ToList();
            var votes = pointers.Take(8000).GroupBy(p => Upper(starts, p) - 1).Where(g => g.Key >= 0).OrderByDescending(g => g.Count()).FirstOrDefault();
            if (votes == null) continue;
            var (regionStart, regionStop) = anon[votes.Key];
            pointers = pointers.Where(p => p >= regionStart && p < regionStop).ToList();
            var first = Math.Max(pointers.Min() / 4096 * 4096 - Block, regionStart);
            var last = Math.Min((pointers.Max() + 256) / 4096 * 4096 + Block, regionStop);
            if (text.Length == 0 || first < textStart || last > textStart + text.Length)
            {
                if (text.Length > 0 && textStart >= regionStart && textStart < regionStop)
                {
                    first = Math.Min(first, textStart);
                    last = Math.Max(last, textStart + text.Length);
                }
                textStart = first;
                text = memory.Dump(device, first, last);
            }
            foreach (var name in missing.ToList())
            {
                var dot = name.LastIndexOf('.');
                string ns = dot < 0 ? "" : name[..dot], shortName = dot < 0 ? name : name[(dot + 1)..];
                foreach (var (klass, namePtr, nsPtr) in classes)
                {
                    if (TextAt(text, textStart, namePtr) != shortName || TextAt(text, textStart, nsPtr) != ns) continue;
                    found[name] = klass;
                    slots[name] = slotOf[klass];
                    il2cpp.Changed = true;
                    missing.Remove(name);
                    break;
                }
            }
        }
        return found;
    }

    private static string TextAt(byte[] text, long textStart, long addr)
    {
        var offset = addr - textStart;
        if (offset < 0 || offset >= text.Length) return "\u0000?";
        var end = Array.IndexOf(text, (byte)0, (int)offset);
        return System.Text.Encoding.UTF8.GetString(text, (int)offset, (end < 0 ? text.Length : end) - (int)offset);
    }

    /// <summary>Số phần tử nhỏ hơn hoặc bằng value (như bisect_right).</summary>
    private static int Upper(long[] sorted, long value)
    {
        int lo = 0, hi = sorted.Length;
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (sorted[mid] <= value) lo = mid + 1; else hi = mid;
        }
        return lo;
    }
}
