using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace DTA.Server.Store;

/// <summary>1 vị trí TELE chuẩn của máy chủ.</summary>
public sealed class TelePosition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int MapId { get; set; }
    public string MapName { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public double[] Rotation { get; set; } = [];
    public string Category { get; set; } = "server";
    public string Description { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public int OrderIndex { get; set; }
    public long CreatedAt { get; set; }
    public long UpdatedAt { get; set; }
    public string Source { get; set; } = "server";
    public int Version { get; set; } = 1;
}

/// <summary>
/// Vị trí TELE chuẩn (nguồn duy nhất cho mọi tool): SQLite + RAM + bản JSON để xem; số phiên bản tăng mỗi lần sửa (lưu riêng trong tele_meta nên
/// xoá vị trí không làm phiên bản tụt). Câu trả lời cho tool được dựng sẵn thành bytes, tool đang có đúng phiên bản thì trả "không đổi".
/// </summary>
public sealed class TeleStore
{
    public static readonly IReadOnlyDictionary<int, string> MapNames = new Dictionary<int, string> { [1001] = "Plaza", [1201] = "Khu cắm trại", [1301] = "Khu nghỉ dưỡng" };
    private const string Columns = "id, name, map_id, map_name, x, y, z, rotation, category, description, enabled, order_index, created_at, updated_at, version";
    private readonly object _lock = new();
    private readonly SqliteConnection _db;
    private readonly string _jsonPath;
    private readonly Dictionary<string, TelePosition> _positions = [];
    private int _version = 1;
    private byte[] _payload = [];

    public TeleStore(SqliteConnection db, string jsonPath)
    {
        (_db, _jsonPath) = (db, jsonPath);
        Execute("""
            CREATE TABLE IF NOT EXISTS tele_positions (
                id TEXT PRIMARY KEY, name TEXT NOT NULL, map_id INTEGER NOT NULL, map_name TEXT NOT NULL, x REAL NOT NULL, y REAL NOT NULL, z REAL NOT NULL,
                rotation TEXT NOT NULL DEFAULT '', category TEXT NOT NULL DEFAULT 'server', description TEXT NOT NULL DEFAULT '', enabled INTEGER NOT NULL DEFAULT 1,
                order_index INTEGER NOT NULL DEFAULT 0, created_at INTEGER NOT NULL, updated_at INTEGER NOT NULL, version INTEGER NOT NULL DEFAULT 1);
            CREATE INDEX IF NOT EXISTS idx_tele_map ON tele_positions(map_id);
            CREATE INDEX IF NOT EXISTS idx_tele_enabled ON tele_positions(enabled);
            CREATE TABLE IF NOT EXISTS tele_meta (k TEXT PRIMARY KEY, v INTEGER NOT NULL);
            """);
        lock (_lock)
        {
            if (Scalar("SELECT COUNT(*) FROM tele_positions") == 0) Seed();
            Reload();
            _version = (int)Math.Max(_version, Scalar("SELECT COALESCE(MAX(v), 1) FROM tele_meta WHERE k = 'version'"));
            Snapshot();
        }
    }

    private static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private int Execute(string sql, params object[] args)
    {
        using var command = _db.CreateCommand();
        command.CommandText = sql;
        for (var i = 0; i < args.Length; i++) command.Parameters.AddWithValue($"${i}", args[i]);
        return command.ExecuteNonQuery();
    }

    private long Scalar(string sql)
    {
        using var command = _db.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar());
    }

    /// <summary>3 vị trí mẫu cho CSDL mới.</summary>
    private void Seed()
    {
        var now = Now;
        (string Id, string Name, int Map, string MapName, double X, double Y, double Z, double[] Rot, string Note, int Order)[] defaults =
        [
            ("server_plaza_center", "Quảng trường (Trung tâm)", 1001, "Plaza", -15.5, 1.2, -4.2, [0, 0, 0, 1], "Khu vực quảng trường chính", 1),
            ("server_resort_dock", "Bến tàu Khu nghỉ dưỡng", 1301, "Khu nghỉ dưỡng", -45, 2.5, 110, [0, 0.7071, 0, 0.7071], "Bãi biển / bến tàu resort", 2),
            ("server_camp_lake", "Hồ cắm trại", 1201, "Khu cắm trại", 80.2, 1.5, -35.6, [0, 0, 0, 1], "Bờ hồ khu cắm trại", 3),
        ];
        foreach (var p in defaults)
            Execute($"INSERT INTO tele_positions ({Columns}) VALUES ($0, $1, $2, $3, $4, $5, $6, $7, 'server', $8, 1, $9, $10, $10, 1)",
                p.Id, p.Name, p.Map, p.MapName, p.X, p.Y, p.Z, JsonSerializer.Serialize(p.Rot), p.Note, p.Order, now);
    }

    private void Reload()
    {
        _positions.Clear();
        using var command = _db.CreateCommand();
        command.CommandText = $"SELECT {Columns} FROM tele_positions ORDER BY order_index ASC, id ASC";
        using var r = command.ExecuteReader();
        while (r.Read())
        {
            double[] rotation;
            try
            {
                rotation = r.GetString(7) is { Length: > 0 } raw ? JsonSerializer.Deserialize<double[]>(raw) ?? [] : [];
            }
            catch (JsonException)
            {
                rotation = [];
            }
            var item = new TelePosition
            {
                Id = r.GetString(0), Name = r.GetString(1), MapId = r.GetInt32(2), MapName = r.GetString(3), X = r.GetDouble(4), Y = r.GetDouble(5), Z = r.GetDouble(6),
                Rotation = rotation, Category = r.GetString(8), Description = r.GetString(9), Enabled = r.GetInt64(10) != 0, OrderIndex = r.GetInt32(11),
                CreatedAt = r.GetInt64(12), UpdatedAt = r.GetInt64(13), Version = r.GetInt32(14),
            };
            _positions[item.Id] = item;
            _version = Math.Max(_version, item.Version);
        }
    }

    /// <summary>Dựng sẵn câu trả lời (chỉ vị trí đang bật) + ghi bản JSON để xem.</summary>
    private void Snapshot()
    {
        var enabled = _positions.Values.Where(p => p.Enabled).ToList();
        _payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { ok = true, message = "", version = _version, positions = enabled }, Json.Options));
        Json.Save(_jsonPath, _positions.Values.ToList());
    }

    /// <summary>Tăng phiên bản (lưu tele_meta) - gọi trong lock trước mỗi thay đổi.</summary>
    private int Bump()
    {
        _version++;
        Execute("INSERT INTO tele_meta (k, v) VALUES ('version', $0) ON CONFLICT(k) DO UPDATE SET v = excluded.v", _version);
        return _version;
    }

    public int Version
    {
        get
        {
            lock (_lock) return _version;
        }
    }

    /// <summary>Câu trả lời cho tool: đúng phiên bản tool đang có thì "không đổi", không thì toàn bộ (bytes dựng sẵn).</summary>
    public byte[] Response(int clientVersion)
    {
        lock (_lock)
        {
            return clientVersion > 0 && clientVersion == _version
                ? Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { ok = true, message = "", not_modified = true, version = _version }))
                : _payload;
        }
    }

    /// <summary>Mọi vị trí (kể cả đang tắt) cho panel quản trị.</summary>
    public List<TelePosition> All()
    {
        lock (_lock) return [.. _positions.Values];
    }

    /// <summary>Thêm (id null) hoặc sửa 1 vị trí; (lỗi - rỗng là được).</summary>
    public string Save(string? id, string name, int mapId, string mapName, double x, double y, double z, double[] rotation, string description, int order)
    {
        name = name.Trim();
        if (name.Length == 0) return "Tên vị trí không được để trống";
        mapName = mapName.Trim().Length > 0 ? mapName.Trim() : MapNames.GetValueOrDefault(mapId, $"Bản đồ {mapId}");
        var now = Now;
        lock (_lock)
        {
            if (id != null && !_positions.ContainsKey(id)) return $"Không tìm thấy vị trí ID: {id}";
            var version = Bump();
            var rot = JsonSerializer.Serialize(rotation);
            if (id == null)
                Execute($"INSERT INTO tele_positions ({Columns}) VALUES ($0, $1, $2, $3, $4, $5, $6, $7, 'server', $8, 1, $9, $10, $10, $11)",
                    $"server_{Guid.NewGuid():N}"[..17], name, mapId, mapName, x, y, z, rot, description, order, now, version);
            else
                Execute("""
                    UPDATE tele_positions SET name = $0, map_id = $1, map_name = $2, x = $3, y = $4, z = $5, rotation = $6, description = $7, order_index = $8,
                    updated_at = $9, version = $10 WHERE id = $11
                    """, name, mapId, mapName, x, y, z, rot, description, order, now, version, id);
            Reload();
            Snapshot();
            return "";
        }
    }

    public bool Delete(string id)
    {
        lock (_lock)
        {
            if (!_positions.ContainsKey(id)) return false;
            Bump();
            Execute("DELETE FROM tele_positions WHERE id = $0", id);
            Reload();
            Snapshot();
            return true;
        }
    }

    public bool Toggle(string id, bool enabled)
    {
        lock (_lock)
        {
            if (!_positions.ContainsKey(id)) return false;
            Execute("UPDATE tele_positions SET enabled = $0, updated_at = $1, version = $2 WHERE id = $3", enabled ? 1 : 0, Now, Bump(), id);
            Reload();
            Snapshot();
            return true;
        }
    }
}
