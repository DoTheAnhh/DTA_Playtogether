using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Text;
using DTA.Runtime.Core;
using DTA.Runtime.Memory;

namespace DTA.Runtime.Il2Cpp;

/// <summary>
/// Giải mã Il2CppClass / FieldInfo trong bộ nhớ game theo TÊN. Offset dưới đây là bố cục struct của bản Unity game dùng (không
/// phải offset của game); mỗi lần dùng đều kiểm ô `klass` của class (luôn trỏ về chính nó).
/// </summary>
public sealed partial class Il2CppResolver(MemoryChannel memory, Il2CppLayouts store)
{
    internal const int Name = 0x10, Namespace = 0x18, Parent = 0x58, Self = 0x78, FieldsPtr = 0x80, StaticFields = 0xB8;
    internal const int InstanceSize = 0xFC, Token = 0x11C, FieldCount = 0x124, HeaderSize = 0x128, FieldInfoSize = 0x20;
    private static readonly byte[] MetadataMagic = [0xAF, 0x1B, 0xB1, 0xFA];

    private readonly ConcurrentDictionary<long, (string Full, long Parent, string Key)> _info = new();
    private readonly ConcurrentDictionary<long, (string[] Names, Dictionary<string, int> Fields)> _records = new();
    private readonly ConcurrentDictionary<(long, string), int> _fieldCache = new();
    private (long Start, long End)? _metadata;

    public MemoryChannel Memory { get; } = memory;
    public Il2CppLayouts Store { get; } = store;
    /// <summary>Vùng nhớ cấp phát động của game (có sau lần dò class đầu tiên).</summary>
    public List<(long Start, long End)> Anon { get; internal set; } = [];
    /// <summary>Có dữ liệu mới cần lưu vào VersionCache.</summary>
    public bool Changed { get; set; }

    /// <summary>
    /// Đọc sẵn tên + class cha của nhiều class trong 2 lượt lệnh. Khoá bố cục = tên + token; class generic dùng chung tên + token
    /// cho mọi kiểu T nên thêm cỡ object để không lẫn bố cục.
    /// </summary>
    public void LoadClasses(IEnumerable<long> klasses)
    {
        var todo = klasses.Where(k => k != 0 && k % 8 == 0 && !_info.ContainsKey(k)).Distinct().ToList();
        if (todo.Count == 0) return;
        var found = new Dictionary<long, (long Name, long Ns, long Parent, uint Size, uint Token)>();
        foreach (var (klass, head) in Memory.ReadObjects(todo, HeaderSize))
        {
            if ((long)U64(head, Self) != klass) continue;
            found[klass] = ((long)U64(head, Name), (long)U64(head, Namespace), (long)U64(head, Parent),
                BinaryPrimitives.ReadUInt32LittleEndian(head.AsSpan(InstanceSize)), BinaryPrimitives.ReadUInt32LittleEndian(head.AsSpan(Token)));
        }
        var texts = NamesAt(found.Values.SelectMany(f => new[] { f.Name, f.Ns }));
        foreach (var (klass, f) in found)
        {
            var name = texts.GetValueOrDefault(f.Name, "");
            var ns = texts.GetValueOrDefault(f.Ns, "");
            var full = ns == "" ? name : $"{ns}.{name}";
            var key = full.Contains('`') ? $"{full}#{f.Token:x}#{f.Size:x}" : $"{full}#{f.Token:x}";
            _info[klass] = (full, f.Parent, key);
        }
    }

    public string FullName(long klass) => ClassInfo(klass)?.Full ?? "";

    /// <summary>Tên class + lần lượt các class cha.</summary>
    public IReadOnlyList<string> Names(long klass) => Record(klass).Names;

    public bool Is(long klass, string name) => Record(klass).Names.Any(n => n.Contains(name, StringComparison.Ordinal));

    /// <summary>Field khai báo ngay trong class.</summary>
    public IReadOnlyDictionary<string, int> DeclaredFields(long klass) =>
        LayoutKey(klass) is { } key ? Store.Layouts[key].Fields : new Dictionary<string, int>();

    public IReadOnlyDictionary<string, int> AllFields(long klass) => Record(klass).Fields;

    public bool HasField(long klass, string name)
    {
        var fields = Record(klass).Fields;
        return fields.ContainsKey(name) || fields.ContainsKey($"<{name}>k__BackingField");
    }

    /// <summary>Offset field (kể cả thừa kế; tự thử tên backing field của property).</summary>
    public int Field(long klass, string name)
    {
        if (_fieldCache.TryGetValue((klass, name), out var cached)) return cached;
        var fields = Record(klass).Fields;
        if (!fields.TryGetValue(name, out var offset) && !fields.TryGetValue($"<{name}>k__BackingField", out offset))
            throw new GameError($"Game đã đổi cấu trúc: không thấy {name} trong {(FullName(klass) is { Length: > 0 } n ? n : "class")}");
        _fieldCache[(klass, name)] = offset;
        return offset;
    }

    /// <summary>Địa chỉ field static. 0 nếu class chưa được game khởi tạo.</summary>
    public long StaticFieldPtr(long klass, string name)
    {
        var owner = klass;
        while (owner != 0 && !DeclaredFields(owner).ContainsKey(name)) owner = ClassInfo(owner)?.Parent ?? 0;
        if (owner == 0) throw new GameError($"Game đã đổi cấu trúc: không thấy {name} trong {FullName(klass)}");
        var head = Header(owner);
        var baseAddr = head == null ? 0 : (long)U64(head, StaticFields);
        return baseAddr == 0 ? 0 : baseAddr + DeclaredFields(owner)[name];
    }

    private (string Full, long Parent, string Key)? ClassInfo(long klass)
    {
        if (!_info.ContainsKey(klass)) LoadClasses([klass]);
        return _info.TryGetValue(klass, out var info) ? info : null;
    }

    private byte[]? Header(long klass)
    {
        var data = klass != 0 && klass % 8 == 0 ? Memory.Read(klass, HeaderSize) : null;
        return data != null && (long)U64(data, Self) == klass ? data : null;
    }

    /// <summary>(tên class + class cha, mọi field kể cả thừa kế - field con đè field cha). Chưa đọc được thì rỗng.</summary>
    private (string[] Names, Dictionary<string, int> Fields) Record(long klass)
    {
        if (_records.TryGetValue(klass, out var record)) return record;
        if (LayoutKey(klass) is not { } key) return ([], new());
        var chain = new List<ClassLayout>();
        for (var current = key; Store.Layouts.TryGetValue(current, out var layout) && chain.Count < 16; current = layout.Parent) chain.Add(layout);
        var fields = new Dictionary<string, int>();
        for (var i = chain.Count - 1; i >= 0; i--) foreach (var (n, o) in chain[i].Fields) fields[n] = o;
        return _records[klass] = (chain.Select(l => l.Name).ToArray(), fields);
    }

    /// <summary>Khoá bố cục, sau khi chắc bố cục của class và mọi class cha đã có trong Store. Null nếu class chưa dựng xong.</summary>
    private string? LayoutKey(long klass)
    {
        if (ClassInfo(klass) is not var (name, parent, key)) return null;
        if (Store.Layouts.TryGetValue(key, out var layout) && (layout.Parent == "" || Store.Layouts.ContainsKey(layout.Parent))) return key;
        var fields = ReadFields(klass);
        var parentKey = parent != 0 ? LayoutKey(parent) : "";
        if (fields == null || parentKey == null) return null;
        Store.Layouts[key] = new ClassLayout { Name = name, Parent = parentKey, Fields = fields };
        Changed = true;
        return key;
    }

    private Dictionary<string, int>? ReadFields(long klass)
    {
        var head = Header(klass);
        if (head == null) return null;
        var table = (long)U64(head, FieldsPtr);
        var count = BinaryPrimitives.ReadUInt16LittleEndian(head.AsSpan(FieldCount));
        if (count == 0) return new();
        var raw = table != 0 ? Memory.Read(table, count * FieldInfoSize) : null;
        if (raw == null) return null;
        var infos = new List<(long NamePtr, int Offset)>();
        for (var i = 0; i < count; i++)
        {
            var at = i * FieldInfoSize;
            var namePtr = (long)U64(raw, at);
            if ((long)U64(raw, at + 16) == klass && namePtr != 0) infos.Add((namePtr, BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(at + 24))));
        }
        if (infos.Count == 0) return null;
        var names = CStrings(infos.Select(i => i.NamePtr));
        var result = new Dictionary<string, int>();
        foreach (var (ptr, offset) in infos) result[names.GetValueOrDefault(ptr, "")] = offset;
        return result;
    }

    /// <summary>Khối metadata (vùng cấp phát bắt đầu bằng chữ ký metadata); null nếu không thấy.</summary>
    public (long Start, long End)? Metadata()
    {
        if (_metadata is { End: > 0 }) return _metadata;
        var heads = Memory.ReadMany(Anon.Select(r => (r.Start, 4)).ToList());
        for (var i = 0; i < Anon.Count; i++)
            if (heads[i] is { } m && m.AsSpan().SequenceEqual(MetadataMagic)) return _metadata = Anon[i];
        return null;
    }

    /// <summary>Tên class / namespace: nằm trong khối metadata nạp nguyên từ file -> nhớ theo vị trí trong khối.</summary>
    private Dictionary<long, string> NamesAt(IEnumerable<long> pointerSource)
    {
        var pointers = pointerSource.Where(p => p != 0).Distinct().ToList();
        if (_metadata == null && Anon.Count > 0 && pointers.Count > 0)
        {
            var regions = pointers.Take(8).Select(p => Anon.LastOrDefault(r => r.Start <= p)).Where(r => r.End != 0).Distinct().Order();
            _metadata = regions.FirstOrDefault(r => Memory.Read(r.Start, 4) is { } m && m.AsSpan().SequenceEqual(MetadataMagic));
        }
        var (start, end) = _metadata ?? (0, 0);
        var result = new Dictionary<long, string>();
        foreach (var p in pointers)
            if (p >= start && p < end && Store.Texts.TryGetValue((p - start).ToString(), out var text)) result[p] = text;
        foreach (var (p, text) in CStrings(pointers.Where(p => !result.ContainsKey(p))))
        {
            if (p >= start && p < end) { Store.Texts[(p - start).ToString()] = text; Changed = true; }
            result[p] = text;
        }
        return result;
    }

    internal Dictionary<long, string> CStrings(IEnumerable<long> addresses)
    {
        var result = new Dictionary<long, string>();
        foreach (var (addr, raw) in Memory.ReadObjects(addresses, 96))
        {
            var end = Array.IndexOf(raw, (byte)0);
            result[addr] = Encoding.UTF8.GetString(raw, 0, end < 0 ? raw.Length : end);
        }
        return result;
    }

    internal static ulong U64(byte[] data, int at) => BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(at));
}
