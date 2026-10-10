namespace DTA.Features.Fishing;

/// <summary>Trạng thái câu (ActorDefaultControl.eFishingState) + mốc cá lớn (BigFish_*: Begin 15 .. StunRecovery 25).</summary>
public static class FishStates
{
    public const int Idle = 0, Waiting = 3, Shadow = 4, Bite = 5, Result = 9, BigPumpin = 16, BigDrag = 17, BigStun = 24, Max = 64;
    public static bool IsBig(int state) => state is >= 15 and <= 25;
}

/// <summary>1 lần đọc trạng thái câu: trạng thái, ID món vừa câu, cỡ (cm), cặp EncryptInt của _hiddenLevel (ID cá).</summary>
public readonly record struct FishState(int State, int CatchItem, int CatchSize, uint LevelKey, int LevelRand);

/// <summary>Đặc điểm cá trong bảng kết quả: đột biến, có biến thể, mã các biến thể (<see cref="FishMutations"/>; null = không đọc được).</summary>
public sealed record CatchTraits(bool? Mutant, bool? Variant, List<int> VariantTypes);

/// <summary>
/// Các loại biến thể cá (PlayTogether.Table.Mutation_Type 1..19). Cá vừa câu giữ các biến thể ở ItemExtraData.Mutations dạng cờ bit: loại t là
/// bit (1 &lt;&lt; (t - 1)) - đúng như hàm game HasMutation. Biến thể chỉ biết sau khi câu lên (gói kết quả FishingCatchA).
/// </summary>
public static class FishMutations
{
    public static readonly IReadOnlyList<(int Type, string Name)> All =
    [
        (1, "Vàng"), (2, "Cầu vồng"), (3, "Ướt"), (4, "Điện giật"), (5, "Gió cuốn"), (6, "Ướp lạnh"), (7, "Cát"), (8, "Ánh trăng"), (9, "Đóng băng"),
        (10, "Cực quang"), (11, "Sương mai"), (12, "Phơi nắng"), (13, "Nguyền rủa"), (14, "Đèn lồng"), (15, "Kỳ ảo"), (16, "Pháo hoa"), (17, "Hoa"),
        (18, "Âm nhạc"), (19, "Kỹ thuật số"),
    ];

    /// <summary>Mã các biến thể có trong cờ bit.</summary>
    public static List<int> FromBits(uint bits) => All.Where(m => (bits & (1u << (m.Type - 1))) != 0).Select(m => m.Type).ToList();

    /// <summary>Tên 1 biến thể.</summary>
    public static string NameOf(int type) => All.FirstOrDefault(m => m.Type == type).Name ?? $"biến thể {type}";

    /// <summary>Chỉ giữ mã hợp lệ.</summary>
    public static HashSet<int> Valid(IEnumerable<int> types) => types.Where(t => All.Any(m => m.Type == t)).ToHashSet();
}

/// <summary>Vùng câu đặc biệt trên bản đồ (tên tài nguyên + tâm vùng).</summary>
public sealed record ZoneSpot(string Asset, float X, float Y, float Z)
{
    /// <summary>
    /// Mọi loại vùng câu đặc biệt game có (bảng tài nguyên vật thể, asset "spawn_fishingzone_*", bản 2.32) -> (mã loại vật thể, tên hiển thị).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, (int Id, string Name)> Known = new Dictionary<string, (int Id, string Name)>
    {
        ["spawn_fishingzone_eel"] = (3001, "Vùng lươn"),
        ["spawn_fishingzone_octo"] = (3002, "Vùng bạch tuộc"),
        ["spawn_fishingzone_bluedragon"] = (3004, "Vùng rồng xanh"),
        ["spawn_fishingzone_forestspirits"] = (3005, "Vùng tinh linh rừng"),
        ["spawn_fishingzone_radar_01"] = (3009, "Vùng ra-đa 1"),
        ["spawn_fishingzone_radar_02"] = (3010, "Vùng ra-đa 2"),
        ["spawn_fishingzone_radar_mutant"] = (3019, "Vùng cá đột biến (ra-đa)"),
        ["spawn_fishingzone_Lyngbakur"] = (3011, "Quái vật biển Lyngbakur"),
        ["spawn_fishingzone_Nauthveli"] = (3012, "Quái vật biển Nauthveli"),
        ["spawn_fishingzone_Skeljungur"] = (3013, "Quái vật biển Skeljungur"),
        ["spawn_fishingzone_Hrosshvalur"] = (3014, "Quái vật biển Hrosshvalur"),
        ["spawn_fishingzone_Taumafiskur"] = (3015, "Quái vật biển Taumafiskur"),
        ["spawn_fishingzone_Sverdhvalur"] = (3016, "Quái vật biển Sverdhvalur"),
        ["spawn_fishingzone_Mushveli"] = (3017, "Quái vật biển Mushveli"),
        ["spawn_fishingzone_Katthveli"] = (3018, "Quái vật biển Katthveli"),
        ["spawn_fishingzone_dragonvillage3"] = (3021, "Vùng làng rồng"),
        ["spawn_fishingzone_sushimaster"] = (3022, "Vùng bậc thầy sushi"),
        ["spawn_fishingzone_sweethalloween"] = (3023, "Vùng Halloween ngọt ngào"),
    };

    /// <summary>Tên hiển thị loại vùng.</summary>
    public static string NameOf(string asset)
    {
        if (Known.TryGetValue(asset, out var known)) return known.Name;
        var words = DTA.Game.Data.Things.Parts(asset).Where(w => w.Length > 0 && w != "fishingzone" && !w.All(char.IsDigit)).ToList();
        return words.Count > 0 ? "Vùng câu " + string.Join(' ', words) : "Vùng câu";
    }
}

/// <summary>
/// Tuỳ chọn câu (đọc thẳng lúc đang câu): giữ / bán; điều kiện giữ khi bán (biến thể, đột biến, ID, cỡ bóng, nền); lọc cá (chỉ giật cá khớp
/// TẤT CẢ điều kiện đặt); dịch chuyển tới vùng câu; giả vùng câu.
/// </summary>
public sealed class FishingOptions
{
    public const string AnyZone = "*";
    public bool Sell { get; set; }
    public bool HasPackage { get; set; }
    public bool AutoRepair { get; set; }
    public bool KeepVariant { get; set; }
    /// <summary>Biến thể cần giữ (mã <see cref="FishMutations"/>); rỗng = mọi biến thể.</summary>
    public HashSet<int> KeepVariantTypes { get; set; } = [];
    public bool KeepMutant { get; set; }
    public HashSet<int> KeepIds { get; set; } = [];
    public HashSet<int> KeepShadows { get; set; } = [];
    public HashSet<int> KeepGrades { get; set; } = [];
    public bool FilterOn { get; set; }
    public HashSet<int> WantedIds { get; set; } = [];
    public HashSet<int> WantedShadows { get; set; } = [];
    public HashSet<int> WantedGrades { get; set; } = [];
    public bool ZoneTele { get; set; }
    public string ZoneFocus { get; set; } = AnyZone;
    public bool FakeZoneOn { get; set; }
    public int FakeZone { get; set; }

    public bool HasKeepConditions => KeepVariant || KeepMutant || KeepIds.Count > 0 || KeepShadows.Count > 0 || KeepGrades.Count > 0;

    /// <summary>Sửa dữ liệu hỏng; bản miễn phí không có lọc / điều kiện giữ / vùng câu.</summary>
    public void Normalize(bool free)
    {
        Sell &= HasPackage;
        KeepShadows = KeepShadows.Where(s => s is >= 1 and <= 7).ToHashSet();
        WantedShadows = WantedShadows.Where(s => s is >= 1 and <= 7).ToHashSet();
        KeepGrades = KeepGrades.Where(g => g is >= 1 and <= 5).ToHashSet();
        WantedGrades = WantedGrades.Where(g => g is >= 1 and <= 5).ToHashSet();
        KeepVariantTypes = FishMutations.Valid(KeepVariantTypes);
        if (!free) return;
        FilterOn = KeepVariant = KeepMutant = ZoneTele = FakeZoneOn = false;
        WantedIds.Clear(); WantedShadows.Clear(); WantedGrades.Clear(); KeepVariantTypes.Clear(); KeepIds.Clear(); KeepShadows.Clear(); KeepGrades.Clear();
    }

    /// <summary>
    /// Cá qua bộ lọc: đúng ID, VÀ đúng cỡ bóng, VÀ (nếu chọn nền) MỌI nền ID đó có thể ra đều nằm trong các nền đã chọn - không bao giờ câu
    /// phải nền không mong muốn.
    /// </summary>
    public bool Wants(int fishId, FishCatalog catalog)
    {
        if (WantedIds.Count > 0 && !WantedIds.Contains(fishId)) return false;
        if (WantedShadows.Count > 0 && !WantedShadows.Contains(catalog.ShadowOf(fishId))) return false;
        if (WantedGrades.Count == 0) return true;
        var grades = catalog.GradesOf(fishId);
        return grades.Count > 0 && grades.IsSubsetOf(WantedGrades);
    }
}
