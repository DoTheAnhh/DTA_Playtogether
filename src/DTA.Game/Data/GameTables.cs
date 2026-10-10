using DTA.Game.Session;
using DTA.Runtime.Core;

namespace DTA.Game.Data;

/// <summary>
/// Bảng dữ liệu của game (FrameWork.sysTable): bảng Item (~28.000 dòng), bảng loại vật thể bản đồ, bảng chữ theo ngôn ngữ.
/// Đọc 1 lần / phiên; tên / giá vật phẩm nhớ theo phiên bản game (catalog cá nằm trong bộ đệm, không còn file riêng).
/// </summary>
public sealed class GameTables(GameSession session)
{
    public const string ItemTable = "TableItemImpl", SpawnTable = "TableSpawnObjectListImpl", MessageTable = "TableMessagesImpl";
    private static readonly Logger L = Log.For("tables");

    private Dictionary<string, long>? _tables;
    private Dictionary<long, long>? _items;
    private Dictionary<int, string>? _assets;
    private (long Language, long Keys, long Texts)? _strings;
    private bool _stringsTried;
    private VersionMap<ItemInfo>? _itemInfo;
    private VersionMap<int>? _prices, _useLimits;

    /// <summary>Các dòng (khoá, con trỏ dòng) của bảng có class <paramref name="tableClass"/>; rỗng nếu game không có.</summary>
    public List<(long Key, long Row)> Rows(string tableClass)
    {
        if (_tables is not { Count: > 0 }) _tables = ReadNamed("Tables");
        var table = _tables.GetValueOrDefault(tableClass);
        return table != 0 ? session.Managed.DictItems(session.Managed.Ptr(table, "_container")) : [];
    }

    /// <summary>Tên class các bảng trong 1 nhóm của sysTable (Tables / PreTables / CdnTables / ServerTables) -> địa chỉ bảng.</summary>
    public Dictionary<string, long> Group(string field) => field == "Tables" && _tables is { Count: > 0 } ? _tables : ReadNamed(field);

    /// <summary>Các dòng của bảng dạng danh sách (TableListBaseImpl, _container là List) có class <paramref name="tableClass"/>; rỗng nếu không có.</summary>
    public List<long> ListRows(string tableClass)
    {
        if (_tables is not { Count: > 0 }) _tables = ReadNamed("Tables");
        var table = _tables.GetValueOrDefault(tableClass);
        return table != 0 ? session.Managed.ListItems(session.Managed.Ptr(table, "_container")) : [];
    }

    /// <summary>Tên class mọi bảng game đã nạp (sysTable.Tables).</summary>
    public IEnumerable<string> Names()
    {
        if (_tables is not { Count: > 0 }) _tables = ReadNamed("Tables");
        return _tables.Keys;
    }

    /// <summary>ID vật phẩm -> dòng bảng Item.</summary>
    public IReadOnlyDictionary<long, long> Items()
    {
        if (_items != null) return _items;
        _items = Rows(ItemTable).ToDictionary(r => r.Key, r => r.Row);
        L.Info($"Bảng vật phẩm: {_items.Count} dòng");
        return _items;
    }

    /// <summary>Loại vật thể bản đồ -> tên tài nguyên (nhớ theo phiên bản game).</summary>
    public IReadOnlyDictionary<int, string> SpawnAssets()
    {
        if (_assets != null) return _assets;
        var saved = session.Cache.Get<Dictionary<int, string>>("spawn_objects");
        if (saved is not { Count: > 0 })
        {
            saved = session.Optional(ReadSpawnAssets, "bảng vật thể") ?? [];
            if (saved.Count > 0) session.Cache.Put("spawn_objects", saved);
        }
        return _assets = saved;
    }

    /// <summary>Tên + cấp nền của vật phẩm (tra bảng chữ theo NameId; nhớ theo phiên bản + ngôn ngữ).</summary>
    public ItemInfo Item(long itemId)
    {
        return ItemMap.GetOrRead(itemId, () => session.Optional(() =>
        {
            var row = Items().GetValueOrDefault(itemId);
            var name = session.Managed.I32(row, "NameId");
            var grade = session.Managed.I32(row, "Grade");
            return name is { } n && grade is { } g && (n != 0 || g != 0) ? new ItemInfo(Text(n), g) : null;
        }, $"vật phẩm {itemId}")) ?? ItemInfo.Unknown;
    }

    /// <summary>Giá bán (SellValue nếu IsSell); 0 nếu không bán được.</summary>
    public int Price(long itemId)
    {
        _prices ??= new VersionMap<int>(session.Cache, "prices");
        if (_prices.TryGet(itemId, out var known)) return known;
        var row = Items().GetValueOrDefault(itemId);
        var klass = session.Managed.ClassOf(row);
        if (klass == 0) return 0;
        var sellAt = session.Il2Cpp.Field(klass, "IsSell");
        var valueAt = session.Il2Cpp.Field(klass, "SellValue");
        var data = session.Memory.Read(row, Math.Max(sellAt + 1, valueAt + 4));
        if (data == null) return 0;
        var price = data[sellAt] != 0 ? Bin.I32(data, valueAt) : 0;
        _prices.Set(itemId, price);
        return price;
    }

    /// <summary>Số lượt dùng tối đa (cột UseCount) của dụng cụ.</summary>
    public int UseLimit(long itemId)
    {
        _useLimits ??= new VersionMap<int>(session.Cache, "rod_limits");
        if (_useLimits.TryGet(itemId, out var known)) return known;
        var limit = session.Managed.I32(Items().GetValueOrDefault(itemId), "UseCount");
        if (limit is { } value) _useLimits.Set(itemId, value);
        return limit ?? 0;
    }

    /// <summary>Chữ của game theo mã chuỗi, theo ngôn ngữ đang dùng; rỗng nếu không tra được.</summary>
    public string Text(int stringId) => session.Optional(() => ReadText(stringId), $"chữ {stringId}") ?? "";

    /// <summary>Danh mục mọi vật phẩm đã biết tên (catalog cá, côn trùng...).</summary>
    public IEnumerable<KeyValuePair<long, ItemInfo>> KnownItems() => ItemMap.Items;

    /// <summary>Bảng tên vật phẩm theo ngôn ngữ đang dùng.</summary>
    private VersionMap<ItemInfo> ItemMap => _itemInfo ??= new VersionMap<ItemInfo>(session.Cache, $"items_{Strings()?.Language ?? 0}");

    /// <summary>(ngôn ngữ, Dictionary mã chuỗi -> mục khoá, Dictionary id -> mục chữ) của StringTable bảng Messages.</summary>
    public (long Language, long Keys, long Texts)? Strings()
    {
        if (_stringsTried) return _strings;
        _stringsTried = true;
        _strings = session.Optional<(long, long, long)?>(() =>
        {
            var messages = ReadNamed("PreTables").GetValueOrDefault(MessageTable);
            var loaded = messages != 0 ? session.Managed.DictItems(session.Managed.Ptr(messages, "_bundleTable")) : [];
            if (loaded.Count == 0) return null;
            var (language, table) = loaded[0];
            var keys = session.Managed.Ptr(session.Managed.Ptr(table, "m_SharedData"), "m_KeyDictionary");
            var texts = session.Managed.Ptr(table, "m_TableEntries");
            return keys != 0 && texts != 0 ? (language, keys, texts) : null;
        }, "bảng chữ");
        if (_strings == null) _stringsTried = false;
        return _strings;
    }

    /// <summary>Tra 1 chữ: khoá chuỗi -> m_Id -> mục chữ -> Data.m_Localized.</summary>
    private string ReadText(int stringId)
    {
        if (Strings() is not var (_, keys, texts)) return "";
        var memory = session.Memory;
        var index = session.Get("dict-index", () => new DictIndex(session));
        var name = stringId.ToString();
        var keyEntry = index.Find(keys, DictIndex.StringHash(name), raw =>
        {
            var ptr = Bin.U64(raw, 0);
            return memory.Strings([ptr], 24).GetValueOrDefault(ptr) == name;
        });
        var rawId = keyEntry != 0 ? memory.Read(keyEntry + session.Il2Cpp.Field(session.Managed.ClassOf(keyEntry), "m_Id"), 8) : null;
        if (rawId == null) return "";
        var textEntry = index.Find(texts, DictIndex.LongHash(Bin.U64(rawId, 0)), raw => raw.AsSpan().SequenceEqual(rawId));
        var localized = session.Managed.Ptr(session.Managed.Ptr(textEntry, "Data"), "m_Localized");
        var text = localized != 0 ? memory.Strings([localized], 120).GetValueOrDefault(localized, "") : "";
        return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>Bảng loại vật thể: {loại: AssetName}.</summary>
    private Dictionary<int, string>? ReadSpawnAssets()
    {
        var rows = Rows(SpawnTable);
        if (rows.Count == 0) return null;
        var offset = session.Il2Cpp.Field(session.Managed.ClassOf(rows[0].Row), "AssetName");
        var data = session.Memory.ReadObjects(rows.Select(r => r.Row), offset + 8);
        var pointers = rows.Where(r => data.ContainsKey(r.Row)).ToDictionary(r => (int)r.Key, r => Bin.U64(data[r.Row], offset));
        var texts = session.Memory.Strings(pointers.Values, 80);
        return pointers.Where(p => texts.TryGetValue(p.Value, out var t) && t.Length > 0).ToDictionary(p => p.Key, p => texts[p.Value]);
    }

    /// <summary>Dictionary bảng của sysTable (<paramref name="field"/> = Tables / PreTables) theo tên class bảng.</summary>
    private Dictionary<string, long> ReadNamed(string field)
    {
        var system = session.System("sysTable");
        var dict = system != 0 ? session.Managed.Ptr(system, field) : 0;
        var heads = session.Memory.ReadObjects(session.Managed.DictItems(dict).Select(p => p.Value), 8);
        var classes = heads.ToDictionary(p => p.Key, p => Bin.U64(p.Value, 0));
        session.Il2Cpp.LoadClasses(classes.Values);
        var result = new Dictionary<string, long>();
        foreach (var (addr, klass) in classes) result[session.Il2Cpp.FullName(klass)] = addr;
        return result;
    }
}
