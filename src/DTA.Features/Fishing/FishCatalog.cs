using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using DTA.Runtime.Core;
using DTA.Runtime.Device;
using DTA.Runtime.Storage;

namespace DTA.Features.Fishing;

/// <summary>1 loài thuộc 1 ID cá.</summary>
public sealed record Species(string Name, int ItemId, int Grade = 0);

/// <summary>1 ID cá (mã server gửi khi bóng cá hiện): cỡ bóng + các loài có thể ra.</summary>
public sealed record FishEntry(int FishId, int Shadow, List<Species> Species);

/// <summary>
/// Database ID cá, lưu trong BỘ ĐỆM theo phiên bản game (không còn file riêng): khởi tạo từ dữ liệu gốc đi kèm tool, cập nhật từ API KTools,
/// tự học thêm loài mới từ chính cá câu được. ID nào chưa có thì lấy cỡ bóng theo bảng của game.
/// </summary>
public sealed class FishCatalog
{
    private const string Key = "fish_catalog";
    private const string ApiUrl = "https://www.ktools.vip/fish-catalog/{0}.json";
    private static readonly Logger L = Log.For("fishing");
    private static readonly HttpClient Http = CreateHttp();
    private readonly VersionCache? _cache;
    private static readonly Dictionary<VersionCache, FishCatalog> Catalogs = [];
    private static readonly Dictionary<string, (VersionCache Cache, string Version)> Installs = [];
    private readonly object _lock = new();
    private Dictionary<int, FishEntry> _entries = [];

    /// <summary>ID vật phẩm -> tên loài; ID vật phẩm -> cấp nền (catalog + bảng Item của game).</summary>
    public Dictionary<int, string> Names { get; private set; } = [];
    public Dictionary<int, int> ItemGrades { get; } = [];
    /// <summary>Cỡ bóng theo bảng FishingDifficulty của game (dùng khi catalog chưa có ID).</summary>
    public Dictionary<int, int> GameShadows { get; set; } = [];

    /// <summary>Catalog trên bộ đệm 1 phiên bản game; null = chỉ dữ liệu gốc (chưa biết bản game).</summary>
    public FishCatalog(VersionCache? cache)
    {
        _cache = cache;
        var stored = cache?.Get<List<FishEntry>>(Key);
        if (stored is not { Count: > 0 })
        {
            stored = Seed();
            if (stored.Count > 0) cache?.Put(Key, stored);
        }
        Index(stored);
    }

    public int Count => _entries.Count;

    /// <summary>Dữ liệu gốc đi kèm tool (tài nguyên nhúng fish_catalog.json).</summary>
    private static List<FishEntry> Seed()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("DTA.Features.Fishing.fish_catalog.json");
        return stream != null ? JsonSerializer.Deserialize<List<FishEntry>>(stream, JsonStore.Options) ?? [] : [];
    }

    /// <summary>Dựng bảng tra (gán nguyên bảng 1 bước - luồng câu không bao giờ thấy bảng dở).</summary>
    private void Index(List<FishEntry> entries)
    {
        var names = new Dictionary<int, string>();
        foreach (var species in entries.SelectMany(e => e.Species))
        {
            names[species.ItemId] = species.Name;
            if (species.Grade > 0) ItemGrades[species.ItemId] = species.Grade;
        }
        (_entries, Names) = (entries.ToDictionary(e => e.FishId), names);
    }

    /// <summary>Cỡ bóng 1..7 (catalog trước, bảng game sau); 0 = không rõ.</summary>
    public int ShadowOf(int fishId) => _entries.TryGetValue(fishId, out var e) && e.Shadow > 0 ? e.Shadow : GameShadows.GetValueOrDefault(fishId);

    /// <summary>Các nền 1 ID cá có thể ra = cấp nền các loài thuộc ID đó.</summary>
    public HashSet<int> GradesOf(int fishId) =>
        _entries.TryGetValue(fishId, out var e) ? e.Species.Select(s => ItemGrades.GetValueOrDefault(s.ItemId)).Where(g => g > 0).ToHashSet() : [];

    /// <summary>Ghi thêm loài vừa câu mà catalog chưa có dưới ID đó (bản game mới hay đổi loài giữa các ID). True = có thay đổi.</summary>
    public bool Learn(int fishId, int itemId, string name, int grade)
    {
        name = name.Trim();
        if (fishId == 0 || itemId == 0 || name.Length == 0) return false;
        lock (_lock)
        {
            if (_entries.TryGetValue(fishId, out var known) && known.Species.Any(s => s.ItemId == itemId)) return false;
            var entries = _entries.Values.ToList();
            var entry = known ?? new FishEntry(fishId, ShadowOf(fishId), []);
            if (known == null) entries.Add(entry);
            entry.Species.Add(new Species(name, itemId, grade));
            Save(entries);
        }
        L.Info($"Catalog học thêm: ID {fishId} - {name}");
        return true;
    }

    /// <summary>Tải catalog của đúng phiên bản game từ KTools rồi GỘP vào (bản mới lúc đầu còn ít ID - không ghi đè).</summary>
    public async Task<(bool Ok, string Message)> UpdateAsync(string version)
    {
        try
        {
            var fresh = await Http.GetFromJsonAsync<List<FishEntry>>($"{string.Format(ApiUrl, version)}?t={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}", JsonStore.Options)
                        ?? throw new InvalidDataException("dữ liệu trả về không đúng định dạng");
            lock (_lock) Save(Merge(_entries.Values.ToList(), fresh));
            return (true, $"Cập nhật thành công, ID: {_entries.Count}, Cá: {Names.Count}");
        }
        catch (Exception e)
        {
            L.Warn($"Cập nhật catalog {version} lỗi: {e.Message}");
            return (false, $"Lỗi cập nhật bản {version}: {e.Message}");
        }
    }

    /// <summary>Gộp: thêm ID mới, cập nhật cỡ bóng, thêm loài mới vào ID đã có.</summary>
    private static List<FishEntry> Merge(List<FishEntry> current, List<FishEntry> fresh)
    {
        var byId = current.ToDictionary(e => e.FishId, e => e with { Species = [.. e.Species] });
        foreach (var entry in fresh.Where(e => e.FishId != 0))
        {
            var known = byId.TryGetValue(entry.FishId, out var k) ? k with { Shadow = entry.Shadow } : entry with { Species = [] };
            known.Species.AddRange(entry.Species.Where(s => known.Species.All(o => o.ItemId != s.ItemId)));
            byId[entry.FishId] = known;
        }
        return [.. byId.Values.OrderBy(e => e.FishId)];
    }

    private void Save(List<FishEntry> entries)
    {
        _cache?.Put(Key, entries);
        _cache?.Save();
        Index(entries);
    }

    /// <summary>Catalog dùng chung của 1 phiên bản game (bot + giao diện cùng 1 object).</summary>
    public static FishCatalog Of(VersionCache cache)
    {
        lock (Catalogs)
        {
            if (!Catalogs.TryGetValue(cache, out var catalog)) Catalogs[cache] = catalog = new FishCatalog(cache);
            return catalog;
        }
    }

    /// <summary>
    /// Catalog của bản game đang cài trên tab + tên phiên bản (KTools dùng, ví dụ "2.32.0") + bộ đệm phiên bản. Gọi ADB (chậm) nên chạy ở luồng nền; nhớ theo tab.
    /// Không đọc được thì (catalog gốc, <see cref="DefaultVersion"/>).
    /// </summary>
    public static (FishCatalog Catalog, string Version, VersionCache? Cache) Installed(EmulatorDevice device)
    {
        lock (Installs)
        {
            if (Installs.TryGetValue(device.Serial, out var known)) return (Of(known.Cache), known.Version, known.Cache);
        }
        foreach (var package in DTA.Game.Session.GameProcess.Packages)
        {
            var info = device.Shell($"dumpsys package {package} | grep -E 'versionCode|versionName'", 5);
            var code = Value(info, "versionCode=");
            if (code.Length == 0) continue;
            var name = Value(info, "versionName=");
            var install = (Cache: VersionCache.For(package, code), Version: name.Length > 0 ? name : DefaultVersion);
            lock (Installs) Installs[device.Serial] = install;
            return (Of(install.Cache), install.Version, install.Cache);
        }
        return (Seeded, DefaultVersion, null);
    }

    /// <summary>Bản game dùng khi chưa đọc được bản đang cài.</summary>
    public const string DefaultVersion = "2.32.0";

    /// <summary>Catalog chỉ có dữ liệu gốc (chưa chọn tab).</summary>
    public static FishCatalog Seeded { get; } = new(null);

    private static string Value(string text, string key)
    {
        var at = text.IndexOf(key, StringComparison.Ordinal);
        return at < 0 ? "" : new string(text[(at + key.Length)..].TakeWhile(c => !char.IsWhiteSpace(c)).ToArray());
    }

    private static HttpClient CreateHttp()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        http.DefaultRequestHeaders.Referrer = new Uri("https://www.ktools.vip/id-ca");
        return http;
    }
}
