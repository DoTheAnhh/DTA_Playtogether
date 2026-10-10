using System.Text.Json;
using System.Text.Json.Serialization;
using DTA.Engine.Gather;
using DTA.Runtime.Storage;

namespace DTA.Engine.Core;

/// <summary>1 dòng lịch sử thu hoạch: lúc ("yyyy-MM-dd HH:mm:ss"), vật thu hoạch, món nhận được, nền, giá bán, giữ / bán.</summary>
public sealed record HistoryEntry(string Time, string Source, string Name, int? Grade, int? Price, string? Action = null)
{
    public const string TimeFormat = "yyyy-MM-dd HH:mm:ss";

    /// <summary>Cách ghi của bản tool cũ: "relic" thay cho "source".</summary>
    public string? Relic { get; init; }

    public static HistoryEntry Of(FindRecord record) => new(record.Time.ToString(TimeFormat), record.Source, record.Name, record.Grade, record.Price, record.Action);
}

/// <summary>
/// Tuỳ chọn + lịch sử riêng của từng tab giả lập: data/(serial).(chức năng).json và data/(serial).(chức năng)-history.json. Khoá JSON kiểu
/// snake_case như bản Python nên đọc tiếp được file cũ.
/// </summary>
public static class DeviceStore
{
    /// <summary>Số lượt gần nhất giữ trong lịch sử.</summary>
    public const int HistoryLimit = 1000;

    public static readonly JsonSerializerOptions Snake = new(JsonStore.Options)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private static string FileOf(string serial, string name) => Paths.DeviceFile(serial, $"{name}.json");

    /// <summary>Tuỳ chọn đã lưu (hỏng / chưa có thì mặc định).</summary>
    public static T Options<T>(string serial, string key) where T : new()
    {
        var options = JsonStore.Load(FileOf(serial, key), new T(), Snake);
        if (options is GatherOptions gather) gather.Normalize();
        return options;
    }

    public static void SaveOptions<T>(string serial, string key, T options) => JsonStore.Save(FileOf(serial, key), options, Snake);

    /// <summary>Lịch sử thu hoạch (cũ nhất trước).</summary>
    public static List<HistoryEntry> History(string serial, string key) => Records<HistoryEntry>(serial, key)
        .Select(e => e.Relic is { Length: > 0 } relic && string.IsNullOrEmpty(e.Source) ? e with { Source = relic, Relic = null } : e).ToList();

    /// <summary>Lịch sử kiểu bất kỳ của 1 chức năng (cũ nhất trước).</summary>
    public static List<T> Records<T>(string serial, string key) => JsonStore.Load(FileOf(serial, $"{key}-history"), new List<T>(), Snake);

    /// <summary>Ghi lịch sử, chỉ giữ <see cref="HistoryLimit"/> lượt gần nhất.</summary>
    public static void SaveHistory<T>(string serial, string key, List<T> history)
    {
        if (history.Count > HistoryLimit) history.RemoveRange(0, history.Count - HistoryLimit);
        JsonStore.Save(FileOf(serial, $"{key}-history"), history, Snake);
    }
}
