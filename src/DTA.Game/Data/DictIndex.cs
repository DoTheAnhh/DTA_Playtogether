using System.Collections.Concurrent;
using DTA.Game.Session;
using DTA.Runtime.Core;

namespace DTA.Game.Data;

/// <summary>Tra thẳng 1 khoá trong Dictionary của game theo mã băm (không đọc cả bảng) - dùng chung theo phiên.</summary>
public sealed class DictIndex(GameSession session)
{
    private const int MaxChain = 64;
    private readonly ConcurrentDictionary<long, (long Buckets, long Count, long Entries, int Base)> _layouts = new();

    /// <summary>Mã băm chuỗi đúng như .NET trong game (đã đối chiếu mã game lưu sẵn).</summary>
    public static int StringHash(string text)
    {
        uint hash1 = 5381, hash2 = 5381;
        for (var i = 0; i < text.Length; i += 2)
        {
            hash1 = ((hash1 << 5) + hash1) ^ text[i];
            if (i + 1 < text.Length) hash2 = ((hash2 << 5) + hash2) ^ text[i + 1];
        }
        return (int)((hash1 + hash2 * 1566083941u) & 0x7FFFFFFF);
    }

    /// <summary>Mã băm khoá long: nửa thấp XOR nửa cao.</summary>
    public static int LongHash(long key) => (int)((key ^ (key >> 32)) & 0x7FFFFFFF);

    /// <summary>
    /// Giá trị (object*) của mục có mã băm <paramref name="hash"/> và <paramref name="match"/>(8 byte khoá) đúng; 0 nếu không có.
    /// Ô băm: thư viện cũ đếm từ 0 (ô trống -1), bản mới đếm từ 1 (ô trống 0) - nhận ra theo mẫu ô băm.
    /// </summary>
    public long Find(long dict, int hash, Func<byte[], bool> match)
    {
        var memory = session.Memory;
        if (!_layouts.TryGetValue(dict, out var layout))
        {
            var head = dict != 0 ? memory.Read(dict, 0x30) : null;
            if (head == null) return 0;
            var fields = session.Il2Cpp.DeclaredFields(Bin.U64(head, 0));
            var buckets = Bin.U64(head, fields.GetValueOrDefault("_buckets", 0x10));
            var entries = Bin.U64(head, fields.GetValueOrDefault("_entries", 0x18));
            var sample = buckets != 0 ? memory.Read(buckets + 0x18, 8 + 4 * 64) : null;
            var count = sample != null ? Bin.U64(sample, 0) : 0;
            if (entries == 0 || count is <= 0 or > 50_000_000) return 0;
            var empty = Enumerable.Range(0, (int)Math.Min(count, 64)).Any(i => Bin.I32(sample!, 8 + 4 * i) == -1);
            _layouts[dict] = layout = (buckets, count, entries, empty ? 0 : 1);
        }
        var raw = memory.Read(layout.Buckets + 0x20 + 4 * (hash % layout.Count), 4);
        var index = raw != null ? Bin.I32(raw, 0) - layout.Base : -1;
        for (var step = 0; step < MaxChain && index >= 0; step++)
        {
            var entry = memory.Read(layout.Entries + 0x20 + 24L * index, 24);
            if (entry == null) return 0;
            if (Bin.I32(entry, 0) == hash && match(entry[8..16])) return Bin.U64(entry, 16);
            index = Bin.I32(entry, 4);
        }
        return 0;
    }
}
