using DTA.Game.Session;
using DTA.Runtime.Core;

namespace DTA.Features.Farm;

/// <summary>
/// Đọc nông trại của người chơi (theo TÊN field, không offset cứng): cây + quả, túi đồ (hạt / nông sản / dụng cụ kèm kg + biến thể), bảng
/// biến thể, cửa hàng hạt. Nút + bảng ở FarmGame.Ui.cs.
/// </summary>
public sealed partial class FarmGame(GameSession session)
{
    private Dictionary<int, (string, float)>? _mutations;

    public GameSession Session { get; } = session;
    private DTA.Runtime.Il2Cpp.ManagedReader M => Session.Managed;

    public bool InFarm() => Session.Camera.Map()?.Id == FarmIds.FarmMap;

    /// <summary>Offset field theo class phần tử đầu + dữ liệu thô từng object (theo lô).</summary>
    private (Dictionary<string, int> At, Dictionary<long, byte[]> Data) Fields(List<long> objects, params string[] names)
    {
        if (objects.Count == 0) return ([], []);
        var klass = M.ClassOf(objects[0]);
        var at = names.ToDictionary(n => n, n => Session.Il2Cpp.Field(klass, n));
        return (at, Session.Memory.ReadObjects(objects, at.Values.Max() + 8));
    }

    /// <summary>NetMyFarmInfo: m_CurrentMap (MapMyFarm) -> Controller -> _cacheMyFarm -> CurInfo.</summary>
    private long FarmInfo() => M.Ptr(M.Ptr(M.Ptr(Session.Camera.CurrentMap(), "Controller"), "_cacheMyFarm"), "CurInfo");

    public List<Crop> Crops() => Session.Optional(() =>
    {
        var (at, data) = Fields(M.ListItems(M.Ptr(FarmInfo(), "NetPlantCropInfoList")), "ItemId", "CropUid", "EndTime");
        return data.Values.Select(r => new Crop(Bin.U64(r, at["CropUid"]), Bin.I32(r, at["ItemId"]), FarmIds.UnixTime(Bin.U64(r, at["EndTime"])))).ToList();
    }, "cây") ?? [];

    public List<Fruit> Fruits() => Session.Optional(() =>
    {
        var (at, data) = Fields(M.ListItems(M.Ptr(FarmInfo(), "NetPlantFruitInfoList")), "ItemId", "Mutations", "PlantedCropUid", "EndTime", "Weight");
        return data.Values.Select(r => new Fruit(Bin.U64(r, at["PlantedCropUid"]), Bin.I32(r, at["ItemId"]), FarmIds.UnixTime(Bin.U64(r, at["EndTime"])),
            Bin.F32(r, at["Weight"]), Bin.I32(r, at["Mutations"]))).ToList();
    }, "quả") ?? [];

    private long CacheUser() => M.Ptr(Session.System("sysCache"), "cacheUser");

    /// <summary>Số gốc tối đa (cacheUser.FarmConfigData.FarmMaxPlantCount).</summary>
    public int MaxPlants() => Session.Optional(() => M.I32(M.Ptr(CacheUser(), "FarmConfigData"), "FarmMaxPlantCount") ?? 0, "số gốc tối đa");

    /// <summary>
    /// Món 1 nhóm túi đồ (không cần mở túi): cacheUser.userInvenList là Multimap giữ Dictionary&lt;nhóm, List&lt;UserItem&gt;&gt; trong 1 field kiểu
    /// Dictionary (tìm theo kiểu). Kg + biến thể từ UserItemOption.Additions "#12:&lt;Scale&gt;|&lt;kg&gt;|&lt;ShapeKey&gt;|&lt;Mutations&gt;".
    /// </summary>
    public List<BagItem> Bag(int group) => Session.Optional(() =>
    {
        var multimap = M.Ptr(CacheUser(), "userInvenList");
        var inner = Session.Ui.Children(multimap).FirstOrDefault(c => M.Names(c).Any(n => n.Contains("Dictionary")));
        var list = M.DictItems(inner).FirstOrDefault(p => p.Key == group).Value;
        var (at, data) = Fields(M.ListItems(list), "ItemUID", "ItemID", "ItemCount", "IsLock", "UserItemOption");
        var options = data.ToDictionary(p => p.Key, p => Bin.U64(p.Value, at["UserItemOption"]));
        var additions = options.Where(o => o.Value != 0).ToDictionary(o => o.Key, o => M.Ptr(o.Value, "Additions"));
        var texts = Session.Memory.Strings(additions.Values.Where(p => p != 0), 64);
        return data.Select(p =>
        {
            var (kg, mutations) = ParseAdditions(texts.GetValueOrDefault(additions.GetValueOrDefault(p.Key), ""));
            return new BagItem(Bin.U64(p.Value, at["ItemUID"]), Bin.I32(p.Value, at["ItemID"]), Bin.I32(p.Value, at["ItemCount"]), p.Value[at["IsLock"]] != 0, kg, mutations);
        }).ToList();
    }, "túi đồ") ?? [];

    private static (float Kg, int Mutations) ParseAdditions(string text)
    {
        var parts = text[(text.IndexOf(':') + 1)..].Split('|');
        return parts.Length > 3 && float.TryParse(parts[1], System.Globalization.CultureInfo.InvariantCulture, out var kg) && int.TryParse(parts[3], out var m) ? (kg, m) : (0, 0);
    }

    /// <summary>Biến thể nông sản {Mutationid: (tên, hệ số giá)} (TableMutationListImpl giữ dòng trong List, đọc 1 lần).</summary>
    public Dictionary<int, (string Name, float Multiplier)> Mutations()
    {
        if (_mutations != null) return _mutations;
        var table = Session.Optional(() =>
        {
            var (at, data) = Fields(M.ListItems(M.Ptr(TableObject("TableMutationListImpl"), "_container")), "ItemType", "Mutationid", "StringId", "SellMultiplier");
            return data.Values.Where(r => Bin.I32(r, at["ItemType"]) == FarmIds.FruitGroup)
                .ToDictionary(r => Bin.I32(r, at["Mutationid"]), r => (Session.Tables.Text(Bin.I32(r, at["StringId"])), Bin.F32(r, at["SellMultiplier"])));
        }, "bảng biến thể");
        return table != null ? _mutations = table : [];
    }

    /// <summary>Object bảng dữ liệu theo tên class (sysTable.Tables).</summary>
    private long TableObject(string name)
    {
        var tables = M.DictItems(M.Ptr(Session.System("sysTable"), "Tables")).Select(p => p.Value);
        return tables.FirstOrDefault(t => Session.Il2Cpp.FullName(M.ClassOf(t)) == name);
    }

    /// <summary>(lúc tự nhập hàng mới, hàng đang bán) của cửa hàng hạt: sysContent.MyFarm (List hệ thống con) -> MyFarmContent._shopDatas[1].</summary>
    public (double Restock, List<ShopItem> Goods)? SeedShop() => Session.Optional<(double, List<ShopItem>)?>(() =>
    {
        var content = M.ListItems(M.Ptr(Session.System("sysContent"), "MyFarm")).FirstOrDefault(i => M.Names(i).Contains("MyFarmContent"));
        var shop = M.DictItems(M.Ptr(content, "_shopDatas")).FirstOrDefault(p => p.Key == FarmIds.SeedShop).Value;
        if (shop == 0) return null;
        var close = Session.Memory.Read(shop + Session.Il2Cpp.Field(M.ClassOf(shop), "CloseTime"), 8);
        var (at, data) = Fields(M.ListItems(M.Ptr(shop, "ShopDatas")), "ItemId", "BuyCount", "PriceTypeStockValue", "PriceTypeAlwaysValue");
        var goods = data.Values.Select(r => new ShopItem(Bin.I32(r, at["ItemId"]), Bin.I32(r, at["BuyCount"]), Bin.I32(r, at["PriceTypeStockValue"]), Bin.I32(r, at["PriceTypeAlwaysValue"]))).ToList();
        return (close != null ? FarmIds.UnixTime(Bin.U64(close, 0)) : 0, goods);
    }, "cửa hàng hạt");
}
