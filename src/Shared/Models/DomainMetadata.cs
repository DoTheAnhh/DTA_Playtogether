using System.Text.Json.Serialization;
using DTA.Shared.Enums;

namespace DTA.Shared.Models;

public sealed class GameIdentity : IEquatable<GameIdentity>
{
    [JsonPropertyName("package_name")]
    public string PackageName { get; set; } = "com.vng.playtogether";

    [JsonPropertyName("version_code")]
    public int VersionCode { get; set; } = 227628;

    [JsonPropertyName("build_id")]
    public string BuildId { get; set; } = "2026.10";

    [JsonPropertyName("architecture")]
    public string Architecture { get; set; } = "arm64-v8a";

    [JsonPropertyName("platform")]
    public PlatformKind Platform { get; set; } = PlatformKind.LDPlayer;

    public bool Equals(GameIdentity? other)
    {
        if (other is null) return false;
        return PackageName == other.PackageName &&
               VersionCode == other.VersionCode &&
               BuildId == other.BuildId &&
               Architecture == other.Architecture &&
               Platform == other.Platform;
    }

    public override bool Equals(object? obj) => obj is GameIdentity other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(PackageName, VersionCode, BuildId, Architecture, Platform);
    public override string ToString() => $"{PackageName} v{VersionCode} ({Architecture}, {Platform})";
}

public sealed class OffsetProfile
{
    [JsonPropertyName("identity")]
    public GameIdentity Identity { get; set; } = new();

    [JsonPropertyName("signature_version")]
    public string SignatureVersion { get; set; } = "1.0.0";

    [JsonPropertyName("slots")]
    public Dictionary<string, long> Slots { get; set; } = [];

    [JsonPropertyName("native_methods")]
    public Dictionary<string, long> NativeMethods { get; set; } = [];

    [JsonPropertyName("field_offsets")]
    public Dictionary<string, Dictionary<string, int>> FieldOffsets { get; set; } = [];
}

public readonly struct ScreenMetrics
{
    public int Width { get; init; }
    public int Height { get; init; }
    public int Dpi { get; init; }
    public bool IsUpright { get; init; }

    public ScreenMetrics(int width, int height, int dpi = 240, bool isUpright = true)
    {
        Width = width;
        Height = height;
        Dpi = dpi;
        IsUpright = isUpright;
    }
}

public sealed class RuntimeCapabilities
{
    public bool CanReadMemory { get; set; } = true;
    public bool CanWriteMemory { get; set; } = true;
    public bool CanDispatchNative { get; set; } = true;
    public bool CanDirectTeleport { get; set; } = true;
    public bool CanZoneTransition { get; set; } = true;
    public bool CanBatchRead { get; set; } = true;
}

public sealed class LicenseDTO
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("hwid")]
    public string HardwareId { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = "user";

    [JsonPropertyName("expires_at")]
    public long ExpiresAt { get; set; }

    [JsonPropertyName("enabled_features")]
    public List<string> EnabledFeatures { get; set; } = [];

    [JsonPropertyName("signature")]
    public string Signature { get; set; } = string.Empty;

    [JsonIgnore]
    public bool IsExpired => DateTimeOffset.UtcNow.ToUnixTimeSeconds() > ExpiresAt;
}

public sealed class FishItemDTO
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("shadow_size")]
    public int ShadowSize { get; set; }

    [JsonPropertyName("grade")]
    public int Grade { get; set; }

    [JsonPropertyName("is_mutant")]
    public bool IsMutant { get; set; }

    [JsonPropertyName("variant")]
    public string Variant { get; set; } = string.Empty;

    [JsonPropertyName("map_id")]
    public int MapId { get; set; }
}

public sealed class RelicItemDTO
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("asset_name")]
    public string AssetName { get; set; } = string.Empty;

    [JsonPropertyName("hp")]
    public int Hp { get; set; }

    [JsonPropertyName("range")]
    public float Range { get; set; }

    [JsonPropertyName("is_rare")]
    public bool IsRare { get; set; }
}
