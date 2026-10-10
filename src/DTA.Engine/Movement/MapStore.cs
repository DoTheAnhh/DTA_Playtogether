using System.Collections.Concurrent;
using DTA.Game.Session;
using DTA.Game.World;
using DTA.Runtime.Core;
using DTA.Runtime.Storage;

namespace DTA.Engine.Movement;

/// <summary>
/// Địa hình (data/ground-(mã).json, dựng từ NavMesh ~0,6 s) và cửa / cổng (data/doors-(mã).json, đọc ~6 s) của từng bản đồ: dựng 1 lần
/// rồi giữ trong RAM + file, game đổi lưới / số cửa thì dựng lại. Dùng chung mọi tab.
/// </summary>
public static class MapStore
{
    private static readonly Logger L = Log.For("terrain");
    private static readonly ConcurrentDictionary<string, (DateTime Stamp, string Mark, Terrain Terrain)> Terrains = new();
    private static readonly ConcurrentDictionary<int, (int Count, List<(float X, float Z)> Spots)> DoorCache = new();

    private sealed record StoredDoors(int Count, List<float[]> Spots);

    /// <summary>Địa hình game của bản đồ; null nếu bản đồ không có lưới / không đọc được.</summary>
    public static Terrain? TerrainFor(GameSession session, int mapId)
    {
        var path = Path.Combine(Paths.DataDir, $"ground-{mapId}.json");
        var (mark, terrain) = Load(path);
        if (session.Ground.Read(mark) is not var (newMark, surfaces)) return null;
        if (surfaces == null) return terrain;
        try
        {
            terrain = Terrain.Build(surfaces, NavMap.CellSize);
        }
        catch (Exception e) when (e is FormatException or IndexOutOfRangeException or ArgumentException)
        {
            L.Warn($"Lưới bản đồ {mapId} khác định dạng: {e.Message}");
            return null;
        }
        terrain.Save(path, newMark);
        Terrains[path] = (File.GetLastWriteTimeUtc(path), newMark, terrain);
        L.Info($"Dựng địa hình bản đồ {mapId}: {terrain.Ground.Count} ô");
        return terrain;
    }

    /// <summary>(dấu lưới, địa hình) từ RAM, file đổi thì đọc lại.</summary>
    private static (string Mark, Terrain? Terrain) Load(string path)
    {
        if (!File.Exists(path)) return ("", null);
        var stamp = File.GetLastWriteTimeUtc(path);
        if (Terrains.TryGetValue(path, out var hit) && hit.Stamp == stamp) return (hit.Mark, hit.Terrain);
        var (mark, terrain) = Terrain.Load(path, NavMap.CellSize);
        if (terrain != null) Terrains[path] = (stamp, mark, terrain);
        return (mark, terrain);
    }

    /// <summary>Cửa / cổng của bản đồ (walker né); chỉ đếm lại số cửa (1-2 lượt đọc) để biết có cần đọc lại không.</summary>
    public static List<(float X, float Z)> DoorsFor(GameSession session, int mapId)
    {
        var count = session.Camera.DoorCount();
        var path = Path.Combine(Paths.DataDir, $"doors-{mapId}.json");
        if (!DoorCache.TryGetValue(mapId, out var hit) && JsonStore.Load<StoredDoors?>(path, null) is { } stored)
            hit = (stored.Count, stored.Spots.Select(s => (s[0], s[1])).ToList());
        if (hit.Spots != null && hit.Count == count)
        {
            DoorCache[mapId] = hit;
            return hit.Spots;
        }
        var spots = session.Camera.Doors();
        if (spots.Count > 0 || count == 0)
        {
            DoorCache[mapId] = (count, spots);
            JsonStore.Save(path, new StoredDoors(count, spots.Select(s => new[] { s.X, s.Z }).ToList()));
        }
        return spots;
    }

    /// <summary>Nạp sẵn địa hình + cửa bản đồ đang đứng (lúc kết nối ở nền) để Bật là đi được ngay.</summary>
    public static void Preload(GameSession session)
    {
        if (session.Camera.Map() is not { } map) return;
        try
        {
            TerrainFor(session, map.Id);
            DoorsFor(session, map.Id);
        }
        catch (Exception e)
        {
            L.Debug($"Nạp sẵn địa hình lỗi: {e.Message}");
        }
    }
}
