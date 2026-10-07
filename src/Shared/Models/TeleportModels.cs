using System.Text.Json.Serialization;

namespace DTA.Shared.Models;

public static class MapConstants
{
    public const int MapPlaza = 1001;
    public const int MapDowntown = 1101;
    public const int MapCamp = 1201;
    public const int MapResort = 1301;
    public const int MapMine = 21001;

    public static readonly IReadOnlyDictionary<int, string> MapNames = new Dictionary<int, string>
    {
        { MapPlaza, "Plaza" },
        { MapDowntown, "Khu trung tâm" },
        { MapCamp, "Khu cắm trại" },
        { MapResort, "Khu nghỉ dưỡng" },
        { MapMine, "Khu mỏ khoáng sản" },
    };

    public static string GetMapName(int mapId) =>
        MapNames.TryGetValue(mapId, out var name) ? name : $"Bản đồ {mapId}";
}

/// <summary>
/// Vị trí dịch chuyển - luôn chứa toạ độ (XYZ), hướng quay nhân vật (Rotation) và góc nhìn camera (Viewpoint).
/// Tuân thủ quy định: vị trí tele luôn phải có thêm cả góc nhìn nhân vật.
/// </summary>
public sealed class TelePositionDTO
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("map_id")]
    public int MapId { get; set; }

    [JsonPropertyName("map_name")]
    public string MapName { get; set; } = string.Empty;

    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }

    [JsonPropertyName("z")]
    public float Z { get; set; }

    /// <summary>
    /// Hướng xoay của nhân vật (Quaternion)
    /// </summary>
    [JsonPropertyName("rotation")]
    public Quaternion Rotation { get; set; } = Quaternion.Identity;

    /// <summary>
    /// Góc nhìn nhân vật & camera (Yaw / Pitch / Camera Alignment)
    /// </summary>
    [JsonPropertyName("viewpoint")]
    public CharacterViewpoint Viewpoint { get; set; } = new();

    [JsonPropertyName("category")]
    public string Category { get; set; } = "server"; // "server" hoặc "local"

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("order_index")]
    public int OrderIndex { get; set; } = 0;

    [JsonPropertyName("source")]
    public string Source { get; set; } = "server";

    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonIgnore]
    public Vector3 Position => new(X, Y, Z);

    public TelePositionDTO() { }

    public TelePositionDTO(
        string id,
        string name,
        int mapId,
        string mapName,
        float x,
        float y,
        float z,
        Quaternion? rotation = null,
        CharacterViewpoint? viewpoint = null,
        string category = "server",
        string description = "",
        int orderIndex = 0)
    {
        Id = id;
        Name = name;
        MapId = mapId;
        MapName = string.IsNullOrEmpty(mapName) ? MapConstants.GetMapName(mapId) : mapName;
        X = x;
        Y = y;
        Z = z;
        Rotation = rotation ?? Quaternion.Identity;
        Viewpoint = viewpoint ?? CharacterViewpoint.FromQuaternion(Rotation);
        Category = category;
        Description = description;
        OrderIndex = orderIndex;
        Source = category;
    }
}
