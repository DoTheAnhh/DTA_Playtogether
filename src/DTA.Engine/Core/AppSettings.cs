using DTA.Runtime.Storage;

namespace DTA.Engine.Core;

/// <summary>Cài đặt chung của tool (data/app.json, khoá snake_case như bản Python): ghi nhớ tuỳ chọn, chờ đổi bản đồ, tab chọn gần nhất...</summary>
public sealed class AppSettings
{
    /// <summary>Đuôi file tuỳ chọn từng chức năng (data/(tab).(đuôi).json) - chỉ các file này bị xoá khi tắt "Ghi nhớ cài đặt".</summary>
    public static readonly string[] OptionKeys = ["esp", "fishing", "excavation", "mining", "insect", "collect", "farm"];

    public bool RememberOptions { get; set; } = true;
    public int HopWait { get; set; } = 5;
    /// <summary>Tab giả lập (serial) chọn lần trước.</summary>
    public string Device { get; set; } = "";
    /// <summary>Giả lập chọn lần trước (LDPlayer / MEmu).</summary>
    public string Emulator { get; set; } = "";
    /// <summary>Trang (tên menu) đang xem lần trước.</summary>
    public string Page { get; set; } = "";

    private static readonly string FilePath = Path.Combine(Paths.DataDir, "app.json");
    private static AppSettings? _current;

    /// <summary>Cài đặt hiện tại (đọc 1 lần).</summary>
    public static AppSettings Current => _current ??= JsonStore.Load(FilePath, new AppSettings(), DeviceStore.Snake);

    /// <summary>Thời gian chờ trước khi đổi bản đồ (1..30 giây).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public int HopWaitSeconds => Math.Clamp(HopWait, 1, 30);

    public void Save() => JsonStore.Save(FilePath, this, DeviceStore.Snake);

    /// <summary>Xoá tuỳ chọn mọi chức năng của mọi tab (về mặc định).</summary>
    public static void ResetOptions()
    {
        if (!Directory.Exists(Paths.DataDir)) return;
        foreach (var file in Directory.EnumerateFiles(Paths.DataDir, "*.json").Where(f => OptionKeys.Any(k => f.EndsWith($".{k}.json"))))
            File.Delete(file);
    }
}
