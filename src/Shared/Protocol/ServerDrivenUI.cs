using System.Text.Json.Serialization;

namespace DTA.Shared.Protocol;

public static class UIComponentTypes
{
    public const string Button = "button";
    public const string Toggle = "toggle";
    public const string Slider = "slider";
    public const string Segment = "segment";
    public const string Entry = "entry";
    public const string StatusCard = "status_card";
    public const string StatTile = "stat_tile";
    public const string Panel = "panel";
    public const string Label = "label";
    public const string Dropdown = "dropdown";
    public const string DataTable = "data_table";
    public const string FilterChips = "filter_chips";
    public const string Divider = "divider";
}

public sealed class UIComponentDTO
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = UIComponentTypes.Label;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public object? Value { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("visible")]
    public bool Visible { get; set; } = true;

    [JsonPropertyName("options")]
    public List<object> Options { get; set; } = [];

    [JsonPropertyName("style")]
    public Dictionary<string, object> Style { get; set; } = [];

    [JsonPropertyName("props")]
    public Dictionary<string, object> Props { get; set; } = [];
}

public sealed class UIScreenDTO
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = string.Empty;

    [JsonPropertyName("order")]
    public int Order { get; set; } = 0;

    [JsonPropertyName("components")]
    public List<UIComponentDTO> Components { get; set; } = [];
}

public sealed class UIPatchDTO
{
    [JsonPropertyName("target_id")]
    public string TargetId { get; set; } = string.Empty;

    [JsonPropertyName("changes")]
    public Dictionary<string, object> Changes { get; set; } = [];
}

public sealed class UIEventDTO
{
    [JsonPropertyName("component_id")]
    public string ComponentId { get; set; } = string.Empty;

    [JsonPropertyName("event")]
    public string Event { get; set; } = "click";

    [JsonPropertyName("value")]
    public object? Value { get; set; }
}

public sealed class AppNavigationDTO
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("features")]
    public List<UIScreenDTO> Features { get; set; } = [];
}
