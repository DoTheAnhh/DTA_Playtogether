using System.Text.Json;
using System.Text.Json.Nodes;
using DTA.Engine.Core;
using DTA.Engine.Movement;
using DTA.Engine.Net;
using DTA.Runtime.Core;
using DTA.Runtime.Storage;

namespace DTA.Features.Teleport;

/// <summary>1 vị trí dịch chuyển: mã (local_ / server_), tên, bản đồ, toạ độ, hướng (quaternion), nguồn.</summary>
public sealed record TelePlace(string Id, string Name, int MapId, string MapName, float X, float Y, float Z, float[]? Rotation, string Source = "local", bool Enabled = true)
{
    public bool Editable => Source == "local";
}

/// <summary>
/// Vị trí riêng của người dùng (data/local_tele_positions.json - không bao giờ gửi lên máy chủ) + vị trí chuẩn của máy chủ (đồng bộ theo
/// phiên bản, máy chủ báo không đổi thì dùng nguyên bộ nhớ; file data/cache/server_tele_positions_cache.json dự phòng khi mất mạng).
/// Giao diện hiện gộp: máy chủ trước, của tôi sau.
/// </summary>
public static class TelePlaces
{
    private static readonly string LocalPath = Path.Combine(Paths.DataDir, "local_tele_positions.json");
    private static readonly string ServerPath = Path.Combine(Paths.DataDir, "cache", "server_tele_positions_cache.json");
    private static readonly Logger L = Log.For("teleport");
    private static readonly object Gate = new();
    private static List<TelePlace>? _local;
    private static (int Version, List<TelePlace> Places)? _server;

    private sealed record ServerCache(int Version, List<TelePlace> Positions);

    private static List<TelePlace> Local => _local ??= JsonStore.Load<List<TelePlace>>(LocalPath, [], DeviceStore.Snake).Where(p => p.Id.StartsWith("local_")).ToList();

    private static (int Version, List<TelePlace> Places) Server
    {
        get
        {
            if (_server != null) return _server.Value;
            var saved = JsonStore.Load<ServerCache?>(ServerPath, null, DeviceStore.Snake);
            return (_server = (saved?.Version ?? 0, saved?.Positions.Select(p => p with { Source = "server" }).ToList() ?? [])).Value;
        }
    }

    /// <summary>Danh sách hiện trên giao diện: máy chủ trước, của tôi sau.</summary>
    public static List<TelePlace> All()
    {
        lock (Gate) return [.. Server.Places, .. Local];
    }

    /// <summary>Thêm vị trí riêng (làm tròn 2 số lẻ, hướng 5 số lẻ).</summary>
    public static TelePlace Add(string name, int mapId, string mapName, float x, float y, float z, float[]? rotation)
    {
        lock (Gate)
        {
            var place = new TelePlace($"local_{Convert.ToHexString(Guid.NewGuid().ToByteArray()[..4]).ToLowerInvariant()}",
                name.Trim().Length > 0 ? name.Trim() : $"Vị trí {Local.Count + 1}", mapId, mapName.Length > 0 ? mapName : Travel.Label(mapId),
                MathF.Round(x, 2), MathF.Round(y, 2), MathF.Round(z, 2), rotation is { Length: 4 } ? rotation.Select(v => MathF.Round(v, 5)).ToArray() : null);
            Local.Add(place);
            Persist();
            return place;
        }
    }

    /// <summary>Sửa vị trí riêng (vị trí máy chủ không sửa được).</summary>
    public static bool Update(string id, string name, float x, float y, float z, float[]? rotation)
    {
        lock (Gate)
        {
            var index = Local.FindIndex(p => p.Id == id);
            if (index < 0) return false;
            var old = Local[index];
            Local[index] = old with { Name = name.Trim().Length > 0 ? name.Trim() : old.Name, X = MathF.Round(x, 2), Y = MathF.Round(y, 2), Z = MathF.Round(z, 2), Rotation = rotation ?? old.Rotation };
            Persist();
            return true;
        }
    }

    public static bool Delete(string id)
    {
        lock (Gate)
        {
            if (Local.RemoveAll(p => p.Id == id) == 0) return false;
            Persist();
            return true;
        }
    }

    private static void Persist() => JsonStore.Save(LocalPath, Local, DeviceStore.Snake);

    /// <summary>Đồng bộ vị trí máy chủ (gửi phiên bản đang có; danh sách rỗng thì xin bản đầy đủ). True nếu có dữ liệu mới.</summary>
    public static async Task<bool> SyncAsync()
    {
        var (version, places) = Server;
        var reply = await ServerClient.Default.TelePositionsAsync(places.Count > 0 ? version : 0);
        if (reply["ok"]?.GetValue<bool>() != true || reply["not_modified"]?.GetValue<bool>() == true && places.Count > 0) return false;
        var fresh = reply["positions"]?.Deserialize<List<TelePlace>>(DeviceStore.Snake)?.Select(p => p with { Source = "server" }).ToList() ?? [];
        var newVersion = reply["version"]?.GetValue<int>() ?? 0;
        lock (Gate) _server = (newVersion, fresh);
        JsonStore.Save(ServerPath, new ServerCache(newVersion, fresh), DeviceStore.Snake);
        L.Info($"Vị trí máy chủ: {fresh.Count} (bản {newVersion})");
        return true;
    }
}
