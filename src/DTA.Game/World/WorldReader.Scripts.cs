using DTA.Runtime.Core;

namespace DTA.Game.World;

/// <summary>Phần đọc thành phần phía game gắn trên GameObject của <see cref="WorldReader"/>.</summary>
public sealed partial class WorldReader
{
    private int? _handle, _pointers;
    private const int HierarchyScan = 0x100, MaxNodes = 1 << 17;

    /// <summary>
    /// Thành phần phía game trên cùng GameObject với từng Transform: {transform: {tên class: object}}. GameObject native giữ mảng
    /// {mã loại, thành phần native}; thành phần native có ô trỏ ngược về object phía game - offset ô đó kiểm lúc chạy bằng 1 thành
    /// phần đã biết cả 2 phía (HUD). Đọc theo lô: bao nhiêu Transform cũng vài lượt lệnh.
    /// </summary>
    public Dictionary<long, Dictionary<string, long>> Scripts(IReadOnlyCollection<long> transforms)
    {
        var result = transforms.Distinct().ToDictionary(t => t, _ => new Dictionary<string, long>());
        if (Layout() is not { Supported: true } l || Handle(l) is not { } handle) return result;
        var objects = Follow(Follow(transforms.Distinct().ToDictionary(t => t, t => t), l.Cached), l.ComponentGo);
        foreach (var ((transform, _), script) in ScriptsOf(objects, l, handle)) result[transform][script.Class] = script.Object;
        return result;
    }

    /// <summary>
    /// Thành phần phía game trên MỌI GameObject con cháu (kể cả chính nó) của GameObject chứa <paramref name="component"/>: [(tên class, object)]. Cây
    /// Transform native = 1 TransformHierarchy: mảng chỉ số cha + mảng con trỏ Transform (offset dò lúc chạy: ô có phần tử [chỉ số nút]
    /// trỏ đúng Transform của nút) -> lấy mọi nút có tổ tiên là nút này (<paramref name="wholeTree"/> = mọi nút của cả cây chứa nó).
    /// </summary>
    public List<(string Class, long Object)> DescendantScripts(long component, bool wholeTree = false) => TreeScripts(component, wholeTree ? Pick.All : Pick.Descendants);

    /// <summary>Thành phần phía game trên GameObject chứa <paramref name="component"/> và mọi GameObject cha của nó (lên tới gốc cây).</summary>
    public List<(string Class, long Object)> AncestorScripts(long component) => TreeScripts(component, Pick.Ancestors);

    private enum Pick { Descendants, Ancestors, All }

    /// <summary>Phần chung: chọn nút trong cây Transform theo <paramref name="pick"/> rồi đọc thành phần phía game trên các GameObject đó.</summary>
    private List<(string Class, long Object)> TreeScripts(long component, Pick pick)
    {
        if (Layout() is not { Supported: true } l || Handle(l) is not { } handle || component == 0) return [];
        var memory = session.Memory;
        var native = TransformOf(memory, (long)memory.U64(component + l.Cached), l);
        var head = native != 0 ? memory.Read(native + l.Hierarchy, 12) : null;
        if (head == null) return [];
        long hierarchy = Bin.U64(head, 0);
        var index = Bin.I32(head, 8);
        var table = hierarchy != 0 ? memory.Read(hierarchy, HierarchyScan) : null;
        if (table == null) return [];
        var capacity = Bin.I32(table, 0x10);
        if (index < 0 || index >= capacity || capacity > MaxNodes) return [];
        if (PointersOffset(table, native, index) is not { } at) return [];
        var parents = memory.Read(Bin.U64(table, 0x20), 4 * capacity);
        var nodes = memory.Read(Bin.U64(table, at), 8 * capacity);
        if (parents == null || nodes == null) return [];
        bool Under(int node)
        {
            for (var depth = 0; node >= 0 && node < capacity && depth < 64; depth++, node = Bin.I32(parents, 4 * node))
                if (node == index) return true;
            return false;
        }
        IEnumerable<int> Chain()
        {
            for (int node = index, depth = 0; node >= 0 && node < capacity && depth < 64; depth++, node = Bin.I32(parents, 4 * node)) yield return node;
        }
        var picked = pick switch { Pick.Ancestors => Chain(), Pick.All => Enumerable.Range(0, capacity), _ => Enumerable.Range(0, capacity).Where(Under) };
        var transforms = picked.Select(i => Bin.U64(nodes, 8 * i)).Where(t => t != 0).Distinct()
            .ToDictionary(t => (long)t, t => (long)t);
        return ScriptsOf(Follow(transforms, l.ComponentGo), l, handle).Select(p => p.Value).ToList();
    }

    /// <summary>Transform native của GameObject chứa thành phần native <paramref name="native"/> (thành phần đầu tiên của GameObject); 0 nếu không đọc được.</summary>
    private static long TransformOf(DTA.Runtime.Memory.MemoryChannel memory, long native, NativeLayout l)
    {
        var gameObject = native != 0 ? (long)memory.U64(native + l.ComponentGo) : 0;
        var array = gameObject != 0 ? (long)memory.U64(gameObject + l.GoComponents) : 0;
        return array != 0 ? (long)memory.U64(array + 8) : 0;
    }

    /// <summary>Offset mảng con trỏ Transform trong TransformHierarchy (nhớ sau lần đầu); null nếu không dò được.</summary>
    private int? PointersOffset(byte[] table, long native, int index)
    {
        if (_pointers is { } known) return known;
        for (var o = 0x28; o + 8 <= table.Length; o += 8)
        {
            var array = (long)Bin.U64(table, o);
            if (!Bin.IsPtr(array) || (long)session.Memory.U64(array + 8L * index) != native) continue;
            _pointers = o;
            return o;
        }
        L.Debug("Không dò được mảng con trỏ Transform trong TransformHierarchy");
        return null;
    }

    /// <summary>{khoá: GameObject native} -> {(khoá, ô): (tên class, object phía game)} của mọi thành phần có object phía game.</summary>
    private Dictionary<(TKey, int), (string Class, long Object)> ScriptsOf<TKey>(Dictionary<TKey, long> objects, NativeLayout l, int handle) where TKey : notnull
    {
        var memory = session.Memory;
        var heads = memory.ReadObjects(objects.Values.Select(g => g + l.GoComponents), 0x14);
        var components = new Dictionary<(TKey, int), long>();
        foreach (var (key, gameObject) in objects)
        {
            if (!heads.TryGetValue(gameObject + l.GoComponents, out var head)) continue;
            long array = Bin.U64(head, 0);
            var count = Bin.I32(head, 0x10);
            var entries = array != 0 && count is > 0 and <= 64 ? memory.Read(array, 16 * count) : null;
            for (var i = 0; entries != null && i < count; i++) components[(key, i)] = Bin.U64(entries, 16 * i + 8);
        }
        var scripts = Follow(Follow(components, handle), 0);
        var classes = Follow(scripts, 0);
        session.Il2Cpp.LoadClasses(classes.Values);
        var result = new Dictionary<(TKey, int), (string, long)>();
        foreach (var (key, script) in scripts)
            if (classes.TryGetValue(key, out var klass)) result[key] = (session.Il2Cpp.FullName(klass), script);
        return result;
    }

    /// <summary>Offset ô trỏ ngược native -> game (thử <see cref="NativeLayout.HandleOffsets"/> trên nút HUD); null nếu không dò được.</summary>
    private int? Handle(NativeLayout l)
    {
        if (_handle != null) return _handle == 0 ? null : _handle;
        var sample = session.Ui.Hud().Values.Append(session.Ui.Stick).FirstOrDefault(o => o != 0);
        var native = sample != 0 ? (long)session.Memory.U64(sample + l.Cached) : 0;
        if (native == 0) return null;
        _handle = NativeLayout.HandleOffsets.FirstOrDefault(o => (long)session.Memory.U64((long)session.Memory.U64(native + o)) == sample);
        if (_handle == 0) L.Warn("Không dò được ô trỏ ngược native -> game");
        return _handle == 0 ? null : _handle;
    }

    /// <summary>{khoá: địa chỉ} -> {khoá: con trỏ đọc tại địa chỉ + offset} (bỏ chỗ rỗng / không đọc được), 1 lượt lệnh.</summary>
    private Dictionary<TKey, long> Follow<TKey>(Dictionary<TKey, long> addresses, int offset) where TKey : notnull
    {
        var data = session.Memory.ReadObjects(addresses.Values.Select(a => a + offset), 8);
        var result = new Dictionary<TKey, long>();
        foreach (var (key, address) in addresses)
            if (data.TryGetValue(address + offset, out var raw) && Bin.U64(raw, 0) is var value and not 0) result[key] = value;
        return result;
    }
}
