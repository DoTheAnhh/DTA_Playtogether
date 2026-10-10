namespace DTA.Game.Data;

/// <summary>Vật thể trên bản đồ (đá, quặng, cây, nguyên liệu...): mã server, object quản lý, tên tài nguyên, bước, trạng thái, vị trí.</summary>
public sealed record MapThing(int Uid, long Ref, string Asset, int Step, int State, float X, float Y, float Z);

/// <summary>Con côn trùng (chim, gói thẻ bay...) đang sống: mã, object điều khiển, ID vật phẩm của loài.</summary>
public readonly record struct Insect(uint Uid, long Control, int Item);

/// <summary>Tên + cấp nền (1..5) của 1 vật phẩm.</summary>
public sealed record ItemInfo(string Name, int Grade)
{
    public static readonly ItemInfo Unknown = new("", 0);
}

/// <summary>Món vừa nhận đúng như bảng kết quả của game đang ghi.</summary>
public sealed record CatchInfo(string Name, string Price, int Grade, bool IsFish);

/// <summary>Tên hiển thị bản đồ / cấp nền.</summary>
public static class GameNames
{
    public static readonly IReadOnlyDictionary<string, string> Maps = new Dictionary<string, string>
    {
        ["Resort Island"] = "Khu nghỉ dưỡng", ["Square"] = "Plaza", ["CampGround"] = "Khu cắm trại",
        ["Lost Island"] = "Hòn đảo bị mất", ["LostIsland"] = "Hòn đảo bị mất", ["TreasureHunt"] = "Hòn đảo bị mất",
    };

    public static readonly IReadOnlyDictionary<int, string> Grades = new Dictionary<int, string>
    {
        [1] = "Trắng", [2] = "Xanh lá", [3] = "Xanh dương", [4] = "Tím", [5] = "VVIP",
    };

    /// <summary>Tên bản đồ cho người dùng (chưa có thì giữ tên cảnh).</summary>
    public static string Map(string scene) => Maps.GetValueOrDefault(scene, scene);
}
