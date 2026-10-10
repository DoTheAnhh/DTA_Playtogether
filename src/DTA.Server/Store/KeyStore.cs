using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace DTA.Server.Store;

/// <summary>1 key: mã, ghi chú, lúc tạo, thời hạn (giây, tính từ lần nhập đầu), hết hạn (giây Unix; 0 = chưa / không), số máy tối đa, máy đang mở {máy: lần báo gần nhất}...</summary>
public sealed class KeyItem
{
    public string Key { get; set; } = "";
    public string Note { get; set; } = "";
    public long Created { get; set; }
    public long Duration { get; set; }
    public long Expires { get; set; }
    public int MaxDevices { get; set; } = 1;
    public Dictionary<string, long> Devices { get; set; } = [];
    public bool Blocked { get; set; }
    public long Activated { get; set; }
    public long LastSeen { get; set; }
    public string LastIp { get; set; } = "";

    public KeyItem Copy()
    {
        var copy = (KeyItem)MemberwiseClone();
        copy.Devices = new Dictionary<string, long>(Devices);
        return copy;
    }
}

/// <summary>Thay đổi 1 key từ panel quản trị (null = giữ nguyên).</summary>
public sealed record KeyChanges(string? Note = null, long? Duration = null, long? Expires = null, int? MaxDevices = null, Dictionary<string, long>? Devices = null,
                                bool? Blocked = null, long? Activated = null);

/// <summary>
/// Kho key: SQLite (WAL, dùng chung file server.db với bản Python) là nguồn chính, bộ nhớ đệm trong RAM để đọc nhanh, đồng bộ ra key.json (sửa tay
/// key.json rồi mở lại máy chủ = nạp lại). Mọi thao tác khoá chung 1 lock.
/// </summary>
public sealed class KeyStore : IDisposable
{
    /// <summary>Máy im quá ngần này giây coi như đã tắt tool (trả chỗ cho máy khác).</summary>
    public const int SlotSeconds = 150;
    public const string Invalid = "Key không đúng", Blocked = "Key đã bị khoá", Expired = "Key đã hết hạn", OtherDevice = "Key này đang được dùng trên máy khác";
    private const string Fields = "key, note, created, duration, expires, max_devices, devices, blocked, activated, last_seen, last_ip";
    private readonly object _lock = new();
    private readonly SqliteConnection _db;
    private readonly string _jsonPath;
    private Dictionary<string, KeyItem> _cache = [];

    public KeyStore(string dbPath, string? jsonPath = null)
    {
        _db = Open(dbPath);
        Execute("""
            CREATE TABLE IF NOT EXISTS keys (
            key TEXT PRIMARY KEY, note TEXT NOT NULL DEFAULT '', created INTEGER NOT NULL, duration INTEGER NOT NULL DEFAULT 0,
            expires INTEGER NOT NULL DEFAULT 0, max_devices INTEGER NOT NULL DEFAULT 1, devices TEXT NOT NULL DEFAULT '{}',
            blocked INTEGER NOT NULL DEFAULT 0, activated INTEGER NOT NULL DEFAULT 0, last_seen INTEGER NOT NULL DEFAULT 0,
            last_ip TEXT NOT NULL DEFAULT '')
            """);
        _jsonPath = jsonPath ?? DefaultJson(Path.GetDirectoryName(Path.GetFullPath(dbPath))!);
        SyncJson();
        Rebuild();
    }

    /// <summary>Mở CSDL ở chế độ WAL (dùng chung cho kho vị trí TELE).</summary>
    public static SqliteConnection Open(string path)
    {
        var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadWriteCreate }.ToString());
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
        command.ExecuteNonQuery();
        return db;
    }

    /// <summary>Chỗ để key.json: biến môi trường DTA_KEYS_PATH, hoặc file đã có ở 1 trong các chỗ quen thuộc quanh CSDL.</summary>
    private static string DefaultJson(string dbDir)
    {
        if (Environment.GetEnvironmentVariable("DTA_KEYS_PATH") is { } env && File.Exists(env)) return Path.GetFullPath(env);
        string[] candidates = [Path.GetFullPath(Path.Combine(dbDir, "..", "..", "key.json")), Path.GetFullPath(Path.Combine(dbDir, "..", "key.json")),
            Path.GetFullPath("key.json"), Path.Combine(dbDir, "key.json")];
        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }

    /// <summary>Các máy đang mở tool (báo về trong <see cref="SlotSeconds"/> giây).</summary>
    public static Dictionary<string, long> Live(Dictionary<string, long> devices, long now) => devices.Where(d => now - d.Value < SlotSeconds).ToDictionary();

    private static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private int Execute(string sql, params object[] args)
    {
        using var command = _db.CreateCommand();
        command.CommandText = sql;
        for (var i = 0; i < args.Length; i++) command.Parameters.AddWithValue($"${i}", args[i]);
        return command.ExecuteNonQuery();
    }

    private static KeyItem Read(SqliteDataReader r)
    {
        var item = new KeyItem
        {
            Key = r.GetString(0), Note = r.GetString(1), Created = r.GetInt64(2), Duration = r.GetInt64(3), Expires = r.GetInt64(4), MaxDevices = r.GetInt32(5),
            Blocked = r.GetInt64(7) != 0, Activated = r.GetInt64(8), LastSeen = r.GetInt64(9), LastIp = r.GetString(10),
        };
        item.Devices = ParseDevices(r.GetString(6), item.LastSeen);
        return item;
    }

    /// <summary>Máy đang mở lưu dạng {máy: lần báo}; bản cũ lưu danh sách máy thì coi lần báo = lần dùng cuối.</summary>
    private static Dictionary<string, long> ParseDevices(string json, long lastSeen)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind switch
            {
                JsonValueKind.Object => doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.TryGetInt64(out var v) ? v : (long)p.Value.GetDouble()),
                JsonValueKind.Array => doc.RootElement.EnumerateArray().ToDictionary(e => e.ToString(), _ => lastSeen),
                _ => [],
            };
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private List<KeyItem> Select(string where = "", params object[] args)
    {
        using var command = _db.CreateCommand();
        command.CommandText = $"SELECT {Fields} FROM keys {where}";
        for (var i = 0; i < args.Length; i++) command.Parameters.AddWithValue($"${i}", args[i]);
        using var reader = command.ExecuteReader();
        var items = new List<KeyItem>();
        while (reader.Read()) items.Add(Read(reader));
        return items;
    }

    private void Rebuild() => _cache = Select("ORDER BY rowid DESC").ToDictionary(i => i.Key);

    private void SaveJson() => Json.Save(_jsonPath, _cache.Values.ToList());

    /// <summary>Nạp key.json (nếu có) vào CSDL: thêm / sửa theo file, xoá key không còn trong file; rồi ghi lại file từ CSDL.</summary>
    private void SyncJson()
    {
        if (File.Exists(_jsonPath))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(_jsonPath));
                var list = doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement
                    : doc.RootElement.TryGetProperty("keys", out var keys) ? keys : default;
                var valid = new HashSet<string>();
                if (list.ValueKind == JsonValueKind.Array)
                {
                    foreach (var it in list.EnumerateArray())
                    {
                        var key = Text(it, "key").Trim().ToLowerInvariant();
                        if (key.Length == 0) continue;
                        valid.Add(key);
                        var devices = it.TryGetProperty("devices", out var d) && d.ValueKind == JsonValueKind.Object ? d.GetRawText() : "{}";
                        Execute("""
                            INSERT INTO keys (key, note, created, duration, expires, max_devices, devices, blocked, activated, last_seen, last_ip)
                            VALUES ($0, $1, $2, $3, $4, $5, $6, $7, $8, $9, $10)
                            ON CONFLICT(key) DO UPDATE SET note = excluded.note, duration = excluded.duration, expires = excluded.expires,
                                max_devices = excluded.max_devices, blocked = excluded.blocked, activated = excluded.activated, last_seen = excluded.last_seen,
                                last_ip = excluded.last_ip
                            """, key, Text(it, "note"), Number(it, "created", Now), Number(it, "duration"), Number(it, "expires"), Number(it, "max_devices", 1), devices,
                            it.TryGetProperty("blocked", out var b) && b.ValueKind == JsonValueKind.True ? 1 : 0, Number(it, "activated"), Number(it, "last_seen"), Text(it, "last_ip"));
                    }
                }
                if (valid.Count == 0) Execute("DELETE FROM keys");
                else Execute($"DELETE FROM keys WHERE key NOT IN ({string.Join(",", valid.Select((_, i) => $"${i}"))})", [.. valid]);
            }
            catch (Exception e) when (e is JsonException or IOException or SqliteException or InvalidOperationException)
            {
            }
        }
        Rebuild();
        SaveJson();
    }

    private static string Text(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";

    private static long Number(JsonElement e, string name, long fallback = 0) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? (long)v.GetDouble() : fallback;

    // ----- Đọc -----
    public List<KeyItem> Keys()
    {
        lock (_lock) return _cache.Values.Select(i => i.Copy()).ToList();
    }

    public KeyItem? Get(string key)
    {
        lock (_lock) return _cache.GetValueOrDefault(key)?.Copy();
    }

    // ----- Ghi -----
    /// <summary>Tạo <paramref name="count"/> key "dta-(uuid)".</summary>
    public List<string> Create(int count, string note, long duration, long expires, int maxDevices)
    {
        var made = new List<string>();
        var now = Now;
        lock (_lock)
        {
            while (made.Count < Math.Max(1, count))
            {
                var key = $"dta-{Guid.NewGuid()}";
                Execute("INSERT INTO keys (key, note, created, duration, expires, max_devices) VALUES ($0, $1, $2, $3, $4, $5)", key, note, now, duration, expires, maxDevices);
                _cache[key] = new KeyItem { Key = key, Note = note, Created = now, Duration = duration, Expires = expires, MaxDevices = maxDevices };
                made.Add(key);
            }
            SaveJson();
        }
        return made;
    }

    public bool Update(string key, KeyChanges changes)
    {
        var columns = new List<(string Column, object Value)>();
        if (changes.Note != null) columns.Add(("note", changes.Note));
        if (changes.Duration != null) columns.Add(("duration", changes.Duration));
        if (changes.Expires != null) columns.Add(("expires", changes.Expires));
        if (changes.MaxDevices != null) columns.Add(("max_devices", changes.MaxDevices));
        if (changes.Devices != null) columns.Add(("devices", JsonSerializer.Serialize(changes.Devices)));
        if (changes.Blocked != null) columns.Add(("blocked", changes.Blocked.Value ? 1 : 0));
        if (changes.Activated != null) columns.Add(("activated", changes.Activated));
        lock (_lock)
        {
            if (columns.Count == 0) return _cache.ContainsKey(key);
            var sql = $"UPDATE keys SET {string.Join(", ", columns.Select((c, i) => $"{c.Column} = ${i}"))} WHERE key = ${columns.Count}";
            if (Execute(sql, [.. columns.Select(c => c.Value), key]) == 0) return false;
            _cache[key] = Select("WHERE key = $0", key)[0];
            SaveJson();
            return true;
        }
    }

    /// <summary>Gỡ mọi máy đang mở của key.</summary>
    public bool ResetDevices(string key) => Update(key, new KeyChanges(Devices: []));

    /// <summary>Máy tắt tool: trả chỗ ngay.</summary>
    public void Release(string key, string device)
    {
        lock (_lock)
        {
            if (!_cache.TryGetValue(key, out var item) || !item.Devices.Remove(device)) return;
            Execute("UPDATE keys SET devices = $0 WHERE key = $1", JsonSerializer.Serialize(item.Devices), key);
            SaveJson();
        }
    }

    public int Delete(IEnumerable<string> keys)
    {
        lock (_lock)
        {
            var deleted = 0;
            foreach (var key in keys)
            {
                if (Execute("DELETE FROM keys WHERE key = $0", key) == 0) continue;
                _cache.Remove(key);
                deleted++;
            }
            if (deleted > 0) SaveJson();
            return deleted;
        }
    }

    /// <summary>
    /// Kiểm + giữ chỗ cho 1 máy (kích hoạt / nhịp tim): key sai / bị khoá / hết hạn / đủ máy khác đang mở thì trả lý do. Lần dùng đầu của key có
    /// thời hạn thì bắt đầu tính hạn từ lúc này. (lý do - rỗng là được, thông tin key).
    /// </summary>
    public (string Problem, KeyItem? Item) Use(string key, string device, string address)
    {
        var now = Now;
        lock (_lock)
        {
            if (!_cache.TryGetValue(key, out var item)) return (Invalid, null);
            if (item.Blocked) return (Blocked, item.Copy());
            if (item.Expires > 0 && now >= item.Expires) return (Expired, item.Copy());
            var devices = Live(item.Devices, now);
            if (!devices.ContainsKey(device) && devices.Count >= item.MaxDevices) return (OtherDevice, item.Copy());
            devices[device] = now;
            item.Devices = devices;
            if (item.Duration > 0 && item.Expires == 0) item.Expires = now + item.Duration;
            item.Activated = item.Activated > 0 ? item.Activated : now;
            (item.LastSeen, item.LastIp) = (now, address);
            Execute("UPDATE keys SET expires = $0, activated = $1, devices = $2, last_seen = $3, last_ip = $4 WHERE key = $5",
                item.Expires, item.Activated, JsonSerializer.Serialize(item.Devices), now, address, key);
            SaveJson();
            return ("", item.Copy());
        }
    }

    public void Dispose() => _db.Dispose();
}
