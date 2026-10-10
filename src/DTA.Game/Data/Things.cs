namespace DTA.Game.Data;

/// <summary>
/// Tên hiển thị vật thể bản đồ (game không có tên tiếng Việt cho chúng): dựng từ tên tài nguyên (spawn_vein_s_01, spawn_ing_shrimp...).
/// </summary>
public static class Things
{
    public const string Ore = "Đá & quặng", Plant = "Cây & nguyên liệu", Other = "Khác";

    private static readonly Dictionary<string, (string Group, string Base)> Bases = new()
    {
        ["vein"] = (Ore, "Mạch đá"), ["gemvein"] = (Ore, "Mạch đá quý"), ["ore"] = (Ore, "Quặng"), ["fossil"] = (Ore, "Hóa thạch"),
        ["plants"] = (Plant, ""), ["ing"] = (Plant, ""),
    };

    private static readonly Dictionary<string, string> Words = new()
    {
        ["treelog"] = "khúc gỗ", ["mushroom"] = "nấm", ["shrimp"] = "tôm", ["egg"] = "trứng", ["clover"] = "cỏ ba lá", ["wasabi"] = "wasabi",
        ["root"] = "rễ", ["glacier"] = "băng", ["gem"] = "đá quý", ["amber"] = "hổ phách", ["meteor"] = "thiên thạch", ["aurora"] = "cực quang",
        ["slime"] = "slime", ["snowman"] = "người tuyết", ["cardcollect"] = "thẻ sưu tầm", ["radar"] = "ra-đa", ["fishingzone"] = "vùng câu",
        ["fossil"] = "hóa thạch", ["veggiefrontier"] = "rau củ", ["onion"] = "hành", ["carrot"] = "cà rốt", ["broccoli"] = "cải xanh",
    };

    public static readonly IReadOnlyDictionary<string, string> Sizes = new Dictionary<string, string> { ["s"] = "nhỏ", ["m"] = "vừa", ["l"] = "lớn" };

    /// <summary>Các từ của tên tài nguyên (bỏ "spawn_").</summary>
    public static string[] Parts(string asset) => (asset.StartsWith("spawn_") ? asset[6..] : asset).Split('_');

    /// <summary>(nhóm, tên hiển thị) theo tên tài nguyên.</summary>
    public static (string Group, string Name) Describe(string asset)
    {
        var words = Parts(asset).Where(w => w.Length > 0 && !w.All(char.IsDigit)).ToList();
        var (group, baseName) = words.Count > 0 && Bases.TryGetValue(words[0], out var b) ? b : (Other, (string?)null);
        if (baseName != null) words.RemoveAt(0);
        var size = words.Select(w => Sizes.GetValueOrDefault(w)).FirstOrDefault(s => s != null) ?? "";
        var rest = string.Join(' ', words.Where(w => !Sizes.ContainsKey(w)).Select(w => Words.GetValueOrDefault(w, w)));
        var name = string.Join(' ', new[] { baseName, rest }.Where(p => !string.IsNullOrEmpty(p)));
        if (name.Length == 0) name = asset;
        return (group, char.ToUpper(name[0]) + name[1..] + (size.Length > 0 ? $" ({size})" : ""));
    }
}
