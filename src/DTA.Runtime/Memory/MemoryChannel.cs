using System.Buffers.Binary;
using System.Text;
using DTA.Runtime.Core;

namespace DTA.Runtime.Memory;

/// <summary>
/// Bộ nhớ tiến trình game qua /proc/(pid)/mem. Mỗi lượt đọc = 1 lệnh shell; chi phí chính là nạp chương trình đọc
/// (xxd của Android ~4,5 ms, busybox tĩnh của bộ root ~0,7 ms) nên luôn đọc theo lô và tự gộp vùng sát nhau.
/// </summary>
public sealed class MemoryChannel(RootShell shell, int pid)
{
    private static readonly string[] BusyboxPaths =
    [
        "/system/xbin/busybox", "/system/bin/busybox", "/sbin/busybox", "/data/adb/magisk/busybox",
        "/data/adb/ksu/bin/busybox", "/data/adb/ap/bin/busybox",
    ];
    /// <summary>2 vùng cách nhau không quá ngần này byte thì đọc gộp 1 lần.</summary>
    private const long Gap = 0x1000;
    /// <summary>ReadObjects gộp các địa chỉ cách nhau không quá 32 KB.</summary>
    private const long ObjectGap = 0x8000;
    /// <summary>1 dải gộp tối đa 512 KB.</summary>
    private const long MaxSpan = 0x80000;
    private static readonly Logger L = Log.For("memory");

    /// <summary>Busybox đọc nhanh: null = chưa thử, "" = không dùng được.</summary>
    private string? _busybox;

    public RootShell Shell { get; } = shell;
    public int Pid { get; } = pid;
    private string MemPath => $"/proc/{Pid}/mem";
    private string Xxd => string.IsNullOrEmpty(_busybox) ? "/system/bin/xxd" : $"{_busybox} xxd";

    public bool Alive() => Shell.Run($"[ -d /proc/{Pid} ] && echo 1") is ["1"];

    public byte[]? Read(long addr, int size) => ReadMany([(addr, size)])[0];

    public ulong U64(long addr) => addr != 0 && Read(addr, 8) is { } d ? BinaryPrimitives.ReadUInt64LittleEndian(d) : 0;

    /// <summary>Đọc nhiều vùng trong 1 lượt; vùng không đọc được -> null. Dải gộp vắt qua vùng chưa cấp phát thì đọc lại riêng.</summary>
    public byte[]?[] ReadMany(IReadOnlyList<(long Addr, int Size)> requests)
    {
        var result = new byte[]?[requests.Count];
        if (requests.Count == 0) return result;
        var spans = new List<(long Start, long End, List<int> Members)>();
        foreach (var i in Enumerable.Range(0, requests.Count).OrderBy(i => requests[i].Addr))
        {
            var (addr, size) = requests[i];
            if (spans.Count > 0 && addr <= spans[^1].End + Gap && addr + size - spans[^1].Start <= MaxSpan)
            {
                var last = spans[^1];
                spans[^1] = (last.Start, Math.Max(last.End, addr + size), last.Members);
                last.Members.Add(i);
            }
            else spans.Add((addr, addr + size, [i]));
        }
        var data = ReadSpans(spans.Select(s => (s.Start, (int)(s.End - s.Start))).ToList());
        var alone = new List<int>();
        for (var k = 0; k < spans.Count; k++)
        {
            foreach (var i in spans[k].Members)
            {
                if (data[k] is { } blob) result[i] = blob.AsSpan((int)(requests[i].Addr - spans[k].Start), requests[i].Size).ToArray();
                else if (spans[k].Members.Count > 1) alone.Add(i);
            }
        }
        if (alone.Count > 0)
        {
            var again = ReadSpans(alone.Select(i => requests[i]).ToList());
            for (var k = 0; k < alone.Count; k++) result[alone[k]] = again[k];
        }
        return result;
    }

    /// <summary>Đọc <paramref name="size"/> byte tại nhiều object, gộp địa chỉ gần nhau. Object không đọc được bị bỏ.</summary>
    public Dictionary<long, byte[]> ReadObjects(IEnumerable<long> addresses, int size)
    {
        var sorted = addresses.Where(a => a != 0).Distinct().Order().ToArray();
        var spans = new List<(long Start, long End)>();
        foreach (var a in sorted)
        {
            if (spans.Count > 0 && a - spans[^1].End <= ObjectGap && a + size - spans[^1].Start <= MaxSpan)
                spans[^1] = (spans[^1].Start, Math.Max(spans[^1].End, a + size));
            else spans.Add((a, a + size));
        }
        var result = new Dictionary<long, byte[]>(sorted.Length);
        foreach (var chunk in spans.Chunk(64))
        {
            var data = ReadMany(chunk.Select(s => (s.Start, (int)(s.End - s.Start))).ToList());
            for (var k = 0; k < chunk.Length; k++)
            {
                if (data[k] is not { } blob) continue;
                var low = Array.BinarySearch(sorted, chunk[k].Start);
                for (var j = low < 0 ? ~low : low; j < sorted.Length && sorted[j] < chunk[k].End; j++)
                    result[sorted[j]] = blob.AsSpan((int)(sorted[j] - chunk[k].Start), size).ToArray();
            }
        }
        var left = sorted.Where(a => !result.ContainsKey(a)).ToArray();
        foreach (var chunk in left.Chunk(64))
        {
            var data = ReadMany(chunk.Select(a => (a, size)).ToList());
            for (var k = 0; k < chunk.Length; k++) if (data[k] is { } d) result[chunk[k]] = d;
        }
        return result;
    }

    /// <summary>Nội dung các System.String: độ dài ở +0x10, ký tự UTF-16 từ +0x14.</summary>
    public Dictionary<long, string> Strings(IEnumerable<long> pointers, int maxChars = 48)
    {
        var result = new Dictionary<long, string>();
        foreach (var (ptr, data) in ReadObjects(pointers, 0x14 + 2 * maxChars))
        {
            var length = Math.Clamp(BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(0x10)), 0, maxChars);
            result[ptr] = Encoding.Unicode.GetString(data, 0x14, 2 * length);
        }
        return result;
    }

    /// <summary>Ghi nhiều chỗ trong 1 lệnh (bọc "( ... )" có dấu cách: mksh hiểu "((" là phép tính số học, cả lô sẽ hỏng).</summary>
    public bool WriteMany(IEnumerable<(long Addr, byte[] Data)> items)
    {
        var parts = items.Where(i => i.Addr != 0 && i.Data.Length > 0).Select(i => $"({WriteCommand(i.Addr, i.Data)} 2>/dev/null)").ToList();
        if (parts.Count == 0) return false;
        Shell.Run($"( {string.Join("; ", parts)} )");
        return true;
    }

    public bool Write(long addr, byte[] data) => WriteMany([(addr, data)]);

    /// <summary>
    /// Cài hook 1 lần + chờ game chạy, cả trong 1 lệnh shell: ghi <paramref name="writes"/> (payload trước, ô slot cuối) -> đọc ô
    /// slot tới khi trampoline tự trả về <paramref name="orig"/> -> quá <paramref name="polls"/> lượt thì tự trả. True = game đã chạy.
    /// </summary>
    public bool SwapAndWait(long slot, ulong orig, IEnumerable<(long Addr, byte[] Data)> writes, int polls)
    {
        var bb = _busybox;
        var read = string.IsNullOrEmpty(bb)
            ? $"/system/bin/xxd -p -s {slot} -l 8 {MemPath} 2>/dev/null"
            : $"{bb} dd if={MemPath} bs=8 iflag=skip_bytes count=1 skip={slot} 2>/dev/null | {bb} xxd -p";
        var nap = string.IsNullOrEmpty(bb) ? "sleep 0.0015" : $"{bb} usleep 1500";
        var origBytes = BitConverter.GetBytes(orig);
        var origHex = Convert.ToHexString(origBytes).ToLowerInvariant();
        var script = string.Join("; ", writes.Where(w => w.Addr != 0).Select(w => WriteCommand(w.Addr, w.Data) + " 2>/dev/null"))
            + $"; r=T; i=0; while [ $i -lt {polls} ]; do [ \"$({read})\" = {origHex} ] && {{ r=OK; break; }}; {nap}; i=$((i+1)); done"
            + $"; if [ $r != OK ]; then if [ \"$({read})\" = {origHex} ]; then r=OK; else {WriteCommand(slot, origBytes)} 2>/dev/null; fi; fi; echo $r";
        var output = Shell.Run(script);
        return output.Count > 0 && output[^1].Trim() == "OK";
    }

    /// <summary>Bản đồ bộ nhớ: (đầu, cuối, quyền, tên vùng).</summary>
    public List<(long Start, long End, string Perms, string Name)> Regions()
    {
        var result = new List<(long, long, string, string)>();
        foreach (var line in Shell.Run($"cat /proc/{Pid}/maps", 15))
        {
            var parts = line.Split(' ', 6, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5 || !parts[0].Contains('-')) continue;
            var range = parts[0].Split('-');
            result.Add((Convert.ToInt64(range[0], 16), Convert.ToInt64(range[1], 16), parts[1], parts.Length > 5 ? parts[5].Trim() : ""));
        }
        return result;
    }

    private string WriteCommand(long addr, byte[] data) =>
        $"echo {Convert.ToHexString(data).ToLowerInvariant()} | {Xxd} -r -p | /system/bin/dd of={MemPath} bs=1 seek={addr} count={data.Length}";

    /// <summary>
    /// Đọc đúng từng vùng (không gộp). Lần đầu: tìm busybox có applet xxd, chỉ dùng nếu nó đọc ra y hệt xxd của Android.
    /// Dòng hex của xxd có dấu cách cuối nên mọi dòng đều được cắt khoảng trắng trước khi đổi.
    /// </summary>
    private byte[]?[] ReadSpans(IReadOnlyList<(long Addr, int Size)> requests)
    {
        if (!string.IsNullOrEmpty(_busybox)) return ReadFast(requests);
        var result = ReadParts(string.Join("; echo ==; ", requests.Select(r => $"xxd -p -s {r.Addr} -l {r.Size} {MemPath} 2>/dev/null")), requests);
        if (_busybox == null)
        {
            var found = Shell.Run("for b in " + string.Join(' ', BusyboxPaths) + "; do [ -x $b ] && $b xxd -p /dev/null >/dev/null 2>&1 && echo $b; done");
            _busybox = found.Count > 0 ? found[0] : "";
            if (_busybox != "")
            {
                if (result.Contains(null)) _busybox = null;
                else if (!ReadFast(requests).Zip(result).All(p => p.First != null && p.First.AsSpan().SequenceEqual(p.Second))) _busybox = "";
            }
            L.Info(_busybox is { Length: > 0 } ? $"Đọc nhanh bằng {_busybox}" : "Đọc bằng xxd của Android");
        }
        return result;
    }

    private byte[]?[] ReadFast(IReadOnlyList<(long Addr, int Size)> requests)
    {
        var cuts = requests.Select(r => $"{_busybox} dd if={MemPath} bs=4096 iflag=skip_bytes,count_bytes skip={r.Addr} count={r.Size}").ToList();
        var text = string.Concat(Shell.Run($"({string.Join("; ", cuts)}) 2>/dev/null | {_busybox} xxd -p", 15).Select(l => l.Trim()));
        if (text.Length == 2 * requests.Sum(r => r.Size) && TryHex(text, out var all))
        {
            var result = new byte[]?[requests.Count];
            var start = 0;
            for (var i = 0; i < requests.Count; i++)
            {
                result[i] = all.AsSpan(start, requests[i].Size).ToArray();
                start += requests[i].Size;
            }
            return result;
        }
        return ReadParts(string.Join("; echo ==; ", cuts.Select(c => $"{c} 2>/dev/null | {_busybox} xxd -p")), requests);
    }

    private byte[]?[] ReadParts(string command, IReadOnlyList<(long Addr, int Size)> requests)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        foreach (var line in Shell.Run(command, 15))
        {
            if (line == "==") { parts.Add(current.ToString()); current.Clear(); }
            else current.Append(line.Trim());
        }
        parts.Add(current.ToString());
        var result = new byte[]?[requests.Count];
        for (var i = 0; i < requests.Count && i < parts.Count; i++)
            result[i] = TryHex(parts[i], out var data) && data.Length == requests[i].Size ? data : null;
        return result;
    }

    private static bool TryHex(string text, out byte[] data)
    {
        try { data = Convert.FromHexString(text); return true; }
        catch (FormatException) { data = []; return false; }
    }
}
