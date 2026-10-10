using DTA.Runtime.Core;
using DTA.Runtime.Memory;

namespace DTA.Runtime.Il2Cpp;

/// <summary>Đọc các kiểu dữ liệu C# chuẩn trong bộ nhớ game (List, Dictionary, mảng, tên class của object) - dùng chung mọi chức năng.</summary>
public sealed class ManagedReader(MemoryChannel memory, Il2CppResolver il2cpp)
{
    public MemoryChannel Memory { get; } = memory;
    public Il2CppResolver Il2Cpp { get; } = il2cpp;

    /// <summary>Class của object.</summary>
    public long ClassOf(long obj) => obj == 0 ? 0 : (long)Memory.U64(obj);

    /// <summary>Tên class của object kèm các class cha.</summary>
    public IReadOnlyList<string> Names(long obj) => obj == 0 ? [] : Il2Cpp.Names(ClassOf(obj));

    /// <summary>Con trỏ tại field <paramref name="field"/> của object.</summary>
    public long Ptr(long obj, string field) => obj == 0 ? 0 : (long)Memory.U64(obj + Il2Cpp.Field(ClassOf(obj), field));

    /// <summary>Số nguyên 32 bit tại field của object.</summary>
    public int? I32(long obj, string field) => obj != 0 && Memory.Read(obj + Il2Cpp.Field(ClassOf(obj), field), 4) is { } d ? Bin.I32(d, 0) : null;

    /// <summary>Số thực tại field của object.</summary>
    public float? F32(long obj, string field) => obj != 0 && Memory.Read(obj + Il2Cpp.Field(ClassOf(obj), field), 4) is { } d ? Bin.F32(d, 0) : null;

    /// <summary>Các phần tử (con trỏ) của List&lt;T&gt; object.</summary>
    public List<long> ListItems(long list)
    {
        var head = list != 0 ? Memory.Read(list, 0x20) : null;
        if (head == null) return [];
        var fields = Il2Cpp.DeclaredFields(Bin.U64(head, 0));
        var items = Bin.U64(head, fields.GetValueOrDefault("_items", 0x10));
        var size = Bin.I32(head, fields.GetValueOrDefault("_size", 0x18));
        return ArrayItems(items, size);
    }

    /// <summary>Phần tử con trỏ của mảng T[] (phần tử từ +0x20); <paramref name="count"/> &lt; 0 = đọc độ dài ở +0x18.</summary>
    public List<long> ArrayItems(long array, int count = -1)
    {
        if (array == 0) return [];
        if (count < 0) count = Memory.Read(array + 0x18, 4) is { } n ? Bin.I32(n, 0) : 0;
        var data = count is > 0 and <= 4096 ? Memory.Read(array + 0x20, count * 8) : null;
        if (data == null) return [];
        var result = new List<long>(count);
        for (var i = 0; i < count; i++) result.Add(Bin.U64(data, i * 8));
        return result;
    }

    /// <summary>Các cặp (khoá số 4 byte thấp, giá trị object) của Dictionary; entry {hash, next, key 8, value*}, hash &lt; 0 = ô trống.</summary>
    public List<(long Key, long Value)> DictItems(long dict)
    {
        var head = dict != 0 ? Memory.Read(dict, 0x30) : null;
        if (head == null) return [];
        var fields = Il2Cpp.DeclaredFields(Bin.U64(head, 0));
        var entries = Bin.U64(head, fields.GetValueOrDefault("_entries", 0x18));
        var count = Bin.I32(head, fields.GetValueOrDefault("_count", 0x20));
        var table = entries != 0 && count is > 0 and <= 200000 ? Memory.Read(entries + 0x20, count * 24) : null;
        if (table == null) return [];
        var result = new List<(long, long)>(count);
        for (var i = 0; i < count; i++)
        {
            var at = i * 24;
            var value = Bin.U64(table, at + 16);
            if (Bin.I32(table, at) >= 0 && value != 0) result.Add((Bin.U32(table, at + 8), value));
        }
        return result;
    }

    /// <summary>Các giá trị int của HashSet&lt;int / enum&gt; (slot {hash, next, value}).</summary>
    public HashSet<int> IntSet(long set)
    {
        var result = new HashSet<int>();
        if (set == 0) return result;
        var fields = Il2Cpp.DeclaredFields(ClassOf(set));
        var head = Memory.Read(set, 0x40);
        if (head == null || !fields.TryGetValue("_slots", out var slotsAt) || !fields.TryGetValue("_count", out var countAt)) return result;
        var count = Bin.I32(head, countAt);
        var last = fields.TryGetValue("_lastIndex", out var lastAt) ? Math.Max(Bin.I32(head, lastAt), count) : count;
        var raw = last is > 0 and <= 4096 ? Memory.Read(Bin.U64(head, slotsAt) + 0x20, 12 * last) : null;
        for (var i = 0; raw != null && i < last; i++) if (Bin.I32(raw, 12 * i) >= 0) result.Add(Bin.I32(raw, 12 * i + 8));
        return result;
    }
}
