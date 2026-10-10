namespace DTA.Runtime.Core;

/// <summary>CRC-32 (IEEE, như zlib.crc32) - dấu nhận dạng dữ liệu (lưới bản đồ...).</summary>
public static class Crc32
{
    private static readonly uint[] Table = Enumerable.Range(0, 256).Select(n =>
    {
        var c = (uint)n;
        for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
        return c;
    }).ToArray();

    /// <summary>CRC nối tiếp từ <paramref name="seed"/> (0 = bắt đầu).</summary>
    public static uint Compute(ReadOnlySpan<byte> data, uint seed = 0)
    {
        var crc = ~seed;
        foreach (var b in data) crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return ~crc;
    }
}
