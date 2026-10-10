namespace DTA.Features.Farm;

/// <summary>1 gốc cây đã trồng (NetPlantCropInfo): mã, ID hạt, lúc lớn xong (giây Unix).</summary>
public sealed record Crop(long Uid, int SeedId, double End);

/// <summary>1 quả trên cây (NetPlantFruitInfo): gốc, ID nông sản, lúc chín, kg, cờ biến thể (bit Mutationid - 1).</summary>
public sealed record Fruit(long CropUid, int ItemId, double End, float Weight, int Mutations);

/// <summary>1 ô túi đồ (UserItem): mã, ID, số lượng, đã khoá (không bao giờ bán), kg, cờ biến thể.</summary>
public sealed record BagItem(long Uid, int ItemId, int Count, bool Locked, float Weight, int Mutations);

/// <summary>1 món cửa hàng hạt: ID, còn trong kho, giá xu hoa, giá kim cương.</summary>
public sealed record ShopItem(int ItemId, int Stock, int FlowerPrice, int DiamondPrice);

/// <summary>Việc bot nông trại làm (chọn trước khi Bật).</summary>
public enum FarmMode { Scan, Harvest, Sell, Plant, BuySeeds }

/// <summary>Lọc biến thể khi bán: mọi quả / chỉ quả không biến thể / chỉ quả có biến thể.</summary>
public enum MutationMode { Any, None, Only }

/// <summary>Số liệu nông trại 1 lượt đọc (cho giao diện).</summary>
public sealed record FarmInfo(bool InFarm, List<Crop> Crops, int MaxPlants, List<Fruit> Ripe, List<Fruit> Growing, Dictionary<int, List<BagItem>> Bag,
                              (double Restock, List<ShopItem> Goods)? Shop, Dictionary<int, DTA.Game.Data.ItemInfo> Items, Dictionary<int, (string Name, float Multiplier)> Mutations, double Time);

/// <summary>1 món vừa thu hoạch / bán.</summary>
public sealed record FarmRecord(DateTime Time, string Action, int Id, string Name, int Grade, float Kg, int Mutations);

/// <summary>Hằng số nông trại.</summary>
public static class FarmIds
{
    public const int FarmMap = 11501, SeedGroup = 65, FruitGroup = 66, GearGroup = 67, SeedShop = 1, FarmMenu = 2;
    public static readonly int[] HarvestMins = [1, 10, 50, 100];
    private const long DotnetEpoch = 621355968000000000, TicksMask = (1L << 62) - 1;

    /// <summary>DateTime của game (8 byte, 2 bit cao là Kind) -> giây Unix; 0 nếu chưa có.</summary>
    public static double UnixTime(long value)
    {
        var ticks = value & TicksMask;
        return ticks > DotnetEpoch ? (ticks - DotnetEpoch) / 1e7 : 0;
    }

    public static double Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
}

/// <summary>Tuỳ chọn nông trại (lưu theo tab, đọc thẳng lúc chạy).</summary>
public sealed class FarmOptions
{
    public int HarvestMin { get; set; } = 1;
    public HashSet<int> HarvestFruits { get; set; } = [];
    public HashSet<int> HarvestGrades { get; set; } = [];
    public string HarvestSearch { get; set; } = "";
    public bool KeepMutationHarvest { get; set; }
    public HashSet<int> HarvestKeepMutations { get; set; } = [];
    public HashSet<int> SellFruits { get; set; } = [];
    public HashSet<int> SellGrades { get; set; } = [];
    public string SellSearch { get; set; } = "";
    public MutationMode MutationMode { get; set; } = MutationMode.Any;
    public bool KeepRare { get; set; } = true;
    public HashSet<int> SellKeepMutations { get; set; } = [];
    public HashSet<int> KeepGrades { get; set; } = [4, 5];
    public float MinKg { get; set; }
    public float MaxKg { get; set; }
    public float PlantSpacing { get; set; } = 0.5f;
    public HashSet<int> PlantPlots { get; set; } = [];
    public HashSet<int> PlantSeeds { get; set; } = [];
    public HashSet<int> PlantGrades { get; set; } = [];
    public string PlantSearch { get; set; } = "";
    public HashSet<int> BuySeeds { get; set; } = [];
    public bool BuyWithDiamond { get; set; }
    public int BuyLimit { get; set; } = 50;
    public HashSet<int> ShopGrades { get; set; } = [];
    public string ShopSearch { get; set; } = "";

    public void Normalize()
    {
        if (!FarmIds.HarvestMins.Contains(HarvestMin)) HarvestMin = 1;
        (MinKg, MaxKg) = (Math.Max(MinKg, 0), Math.Max(MaxKg, 0));
        PlantSpacing = Math.Clamp(PlantSpacing, 0.1f, 3);
        BuyLimit = Math.Max(1, BuyLimit);
    }

    /// <summary>Có bit biến thể nào trong danh sách không (danh sách rỗng = mọi biến thể).</summary>
    private static bool HasAny(int mutations, HashSet<int> wanted) => wanted.Count == 0 ? mutations != 0 : wanted.Any(id => (mutations >> (id - 1) & 1) != 0);

    /// <summary>Món túi đồ có được bán không (giao diện dùng để đếm trước).</summary>
    public bool Sellable(BagItem item, int grade)
    {
        if (item.Locked || KeepGrades.Contains(grade) || !SellFruits.Contains(item.ItemId)) return false;
        if (MutationMode == MutationMode.None && item.Mutations != 0 || MutationMode == MutationMode.Only && item.Mutations == 0) return false;
        if (KeepRare && item.Mutations != 0 && HasAny(item.Mutations, SellKeepMutations)) return false;
        return !(MinKg > 0 && item.Weight < MinKg || MaxKg > 0 && item.Weight > MaxKg);
    }

    /// <summary>Quả có được thu hoạch không.</summary>
    public bool Harvestable(Fruit fruit) =>
        HarvestFruits.Contains(fruit.ItemId) && !(KeepMutationHarvest && fruit.Mutations != 0 && HasAny(fruit.Mutations, HarvestKeepMutations));
}
