using DTA.Runtime.Core;
using DTA.Runtime.Il2Cpp;

namespace DTA.Runtime.Storage;

/// <summary>Nội dung bộ đệm của 1 phiên bản game.</summary>
public sealed class VersionData
{
    public Il2CppLayouts Il2Cpp { get; set; } = new();
    /// <summary>Dữ liệu tĩnh khác của phiên bản (bảng game, catalog cá, mã gốc trampoline...): khoá -> JSON.</summary>
    public Dictionary<string, string> Blobs { get; set; } = new();
}

/// <summary>
/// Bộ đệm theo phiên bản game, lưu runtime/cache/(package)-(versionCode).json.gz. Dùng chung cho mọi phiên game cùng phiên bản
/// trong 1 tiến trình tool; ghi nguyên tử, chỉ ghi khi có thay đổi.
/// </summary>
public sealed class VersionCache
{
    private static readonly Logger L = Log.For("cache");
    private static readonly Dictionary<string, VersionCache> Open = new();
    private readonly string _path;

    public VersionData Data { get; }

    private VersionCache(string key)
    {
        _path = Path.Combine(Paths.CacheDir, $"{key}.json.gz");
        Data = JsonStore.Load(_path, new VersionData());
        L.Info($"Bộ đệm {key}: {Data.Il2Cpp.Layouts.Count} class, {Data.Blobs.Count} mục");
    }

    /// <summary>Bộ đệm của phiên bản (dùng chung trong tiến trình).</summary>
    public static VersionCache For(string package, string versionCode)
    {
        var key = $"{package}-{versionCode}";
        lock (Open)
        {
            if (!Open.TryGetValue(key, out var cache)) Open[key] = cache = new VersionCache(key);
            return cache;
        }
    }

    /// <summary>Đọc 1 mục dữ liệu tĩnh; chưa có thì null.</summary>
    public T? Get<T>(string key) where T : class =>
        Data.Blobs.TryGetValue(key, out var json) ? System.Text.Json.JsonSerializer.Deserialize<T>(json, JsonStore.Options) : null;

    /// <summary>Ghi 1 mục dữ liệu tĩnh (lưu xuống đĩa ở lần <see cref="Save"/> kế).</summary>
    public void Put<T>(string key, T value)
    {
        lock (Data.Blobs) Data.Blobs[key] = System.Text.Json.JsonSerializer.Serialize(value, JsonStore.Options);
        Dirty = true;
    }

    public bool Dirty { get; set; }

    /// <summary>Ghi xuống đĩa nếu có thay đổi.</summary>
    public void Save()
    {
        lock (Data.Blobs)
        {
            if (!Dirty) return;
            JsonStore.Save(_path, Data);
            Dirty = false;
        }
        L.Debug($"Đã lưu bộ đệm {Path.GetFileName(_path)}");
    }
}
