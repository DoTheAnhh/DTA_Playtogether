using System.IO.Compression;
using DTA.Runtime.Core;
using DTA.Runtime.Device;

namespace DTA.Runtime.Memory;

/// <summary>Chép vùng nhớ lớn về máy: nén gzip trên giả lập rồi `adb pull` (nhanh ~10 lần kéo qua shell).</summary>
public static class MemoryDump
{
    private static readonly Logger L = Log.For("memory");
    private const string DeviceTemp = "/data/local/tmp/.dta_scan";

    /// <summary>Đầu / cuối từng vùng phải chẵn trang nhớ (4096). Trả nội dung từng vùng đúng thứ tự.</summary>
    public static List<byte[]> DumpRanges(this MemoryChannel memory, EmulatorDevice device, IReadOnlyList<(long Start, long End)> ranges)
    {
        var path = $"/proc/{memory.Pid}/mem";
        var reads = string.Join("; ", ranges.Select(r => $"dd if={path} ibs=4096 obs=1048576 skip={r.Start / 4096} count={(r.End - r.Start) / 4096} 2>/dev/null"));
        var host = Path.GetTempFileName();
        var remote = $"{DeviceTemp}.{Environment.ProcessId}.{Path.GetFileName(host)}";
        byte[] data;
        try
        {
            memory.Shell.Run($"({reads}) | gzip -1 > {remote}", 120);
            device.Adb(["pull", remote, host], 120);
            using var gz = new GZipStream(File.OpenRead(host), CompressionMode.Decompress);
            using var ms = new MemoryStream();
            gz.CopyTo(ms);
            data = ms.ToArray();
        }
        catch (Exception e) when (e is IOException or InvalidDataException)
        {
            L.Warn($"Chép vùng nhớ lỗi: {e.Message}");
            data = [];
        }
        finally
        {
            memory.Shell.Run($"rm -f {remote}");
            try { File.Delete(host); } catch (Exception e) { L.Swallowed("xoá file tạm", e); }
        }
        if (data.Length != ranges.Sum(r => r.End - r.Start)) throw new GameError("Không chép được vùng nhớ của game", true);
        var chunks = new List<byte[]>(ranges.Count);
        var offset = 0;
        foreach (var (start, end) in ranges)
        {
            chunks.Add(data.AsSpan(offset, (int)(end - start)).ToArray());
            offset += (int)(end - start);
        }
        return chunks;
    }

    public static byte[] Dump(this MemoryChannel memory, EmulatorDevice device, long start, long end) =>
        memory.DumpRanges(device, [(start, end)])[0];
}
