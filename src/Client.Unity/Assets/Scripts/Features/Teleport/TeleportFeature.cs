using DTA.Core.ActionDispatcher;
using DTA.Core.Events;
using DTA.Features.Base;
using DTA.Shared.Enums;
using DTA.Shared.Models;

namespace DTA.Features.Teleport;

public interface IWaypointRegistry
{
    IReadOnlyList<TelePositionDTO> GetAllWaypoints();
    IReadOnlyList<TelePositionDTO> GetWaypointsByMap(int mapId);
    TelePositionDTO? FindById(string id);
    void SetServerWaypoints(IEnumerable<TelePositionDTO> serverWaypoints);
    TelePositionDTO AddLocalWaypoint(string name, int mapId, Vector3 position, Quaternion rotation, CharacterViewpoint viewpoint, string description = "");
    bool UpdateLocalWaypoint(string id, string name, Vector3 position, Quaternion rotation, CharacterViewpoint viewpoint, string description = "");
    bool DeleteLocalWaypoint(string id);
}

public sealed class WaypointRegistry : IWaypointRegistry
{
    private readonly List<TelePositionDTO> _serverWaypoints = [];
    private readonly List<TelePositionDTO> _localWaypoints = [];
    private readonly object _lock = new();

    public WaypointRegistry()
    {
        // Seed default server waypoints (each with coordinates, character rotation and character viewpoint)
        _serverWaypoints.AddRange(
        [
            new TelePositionDTO(
                "server_plaza_center",
                "Quảng trường (Trung tâm)",
                MapConstants.MapPlaza,
                "Plaza",
                -15.50f, 1.20f, -4.20f,
                Quaternion.Identity,
                new CharacterViewpoint(0f, 10f, 4.5f, true),
                "server",
                "Khu vực quảng trường chính",
                1
            ),
            new TelePositionDTO(
                "server_resort_dock",
                "Bến tàu Khu nghỉ dưỡng",
                MapConstants.MapResort,
                "Khu nghỉ dưỡng",
                -45.00f, 2.50f, 110.00f,
                Quaternion.FromEuler(0f, 90f, 0f),
                new CharacterViewpoint(90f, 12f, 4.5f, true),
                "server",
                "Bãi biển / bến tàu resort",
                2
            ),
            new TelePositionDTO(
                "server_camp_lake",
                "Hồ cắm trại",
                MapConstants.MapCamp,
                "Khu cắm trại",
                80.20f, 1.50f, -35.60f,
                Quaternion.Identity,
                new CharacterViewpoint(180f, 8f, 4.5f, true),
                "server",
                "Bờ hồ khu cắm trại",
                3
            ),
            new TelePositionDTO(
                "server_mine_deep",
                "Khu mỏ tầng sâu",
                MapConstants.MapMine,
                "Khu mỏ khoáng sản",
                12.40f, -10.50f, 45.20f,
                Quaternion.FromEuler(0f, 45f, 0f),
                new CharacterViewpoint(45f, 5f, 4.0f, true),
                "server",
                "Khu vực nhiều mạch quặng lớn",
                4
            )
        ]);
    }

    public IReadOnlyList<TelePositionDTO> GetAllWaypoints()
    {
        lock (_lock)
        {
            var list = new List<TelePositionDTO>(_serverWaypoints.Count + _localWaypoints.Count);
            list.AddRange(_serverWaypoints);
            list.AddRange(_localWaypoints);
            return list;
        }
    }

    public IReadOnlyList<TelePositionDTO> GetWaypointsByMap(int mapId)
    {
        lock (_lock)
        {
            return GetAllWaypoints().Where(p => p.MapId == mapId).OrderBy(p => p.OrderIndex).ToList();
        }
    }

    public TelePositionDTO? FindById(string id)
    {
        lock (_lock)
        {
            return _serverWaypoints.FirstOrDefault(p => p.Id == id) ??
                   _localWaypoints.FirstOrDefault(p => p.Id == id);
        }
    }

    public void SetServerWaypoints(IEnumerable<TelePositionDTO> serverWaypoints)
    {
        lock (_lock)
        {
            _serverWaypoints.Clear();
            _serverWaypoints.AddRange(serverWaypoints);
        }
    }

    public TelePositionDTO AddLocalWaypoint(
        string name,
        int mapId,
        Vector3 position,
        Quaternion rotation,
        CharacterViewpoint viewpoint,
        string description = "")
    {
        var wp = new TelePositionDTO(
            $"local_{Guid.NewGuid():N}",
            name,
            mapId,
            MapConstants.GetMapName(mapId),
            position.X,
            position.Y,
            position.Z,
            rotation,
            viewpoint,
            "local",
            description,
            _localWaypoints.Count + 100
        )
        {
            Source = "local"
        };

        lock (_lock)
        {
            _localWaypoints.Add(wp);
        }
        return wp;
    }

    public bool UpdateLocalWaypoint(
        string id,
        string name,
        Vector3 position,
        Quaternion rotation,
        CharacterViewpoint viewpoint,
        string description = "")
    {
        lock (_lock)
        {
            var item = _localWaypoints.FirstOrDefault(p => p.Id == id);
            if (item == null) return false;

            item.Name = name;
            item.X = position.X;
            item.Y = position.Y;
            item.Z = position.Z;
            item.Rotation = rotation;
            item.Viewpoint = viewpoint;
            item.Description = description;
            item.Version++;
            return true;
        }
    }

    public bool DeleteLocalWaypoint(string id)
    {
        lock (_lock)
        {
            int idx = _localWaypoints.FindIndex(p => p.Id == id);
            if (idx >= 0)
            {
                _localWaypoints.RemoveAt(idx);
                return true;
            }
            return false;
        }
    }
}

public interface ITeleportService : IFeatureService
{
    IWaypointRegistry Registry { get; }
    ValueTask<FeatureResult> TeleportToAsync(TelePositionDTO destination, bool alignCamera = true, CancellationToken ct = default);
    ValueTask<FeatureResult> TeleportCoordinatesAsync(int mapId, Vector3 position, Quaternion rotation, CharacterViewpoint viewpoint, bool alignCamera = true, CancellationToken ct = default);
}

public sealed class TeleportService : BaseFeatureService<TeleportOptions, TeleportStats>, ITeleportService
{
    public override FeatureId Id => FeatureId.Teleport;
    public override string Name => "Dịch chuyển";

    public IWaypointRegistry Registry { get; }
    private readonly IEventBus _eventBus;
    private int _currentMapId = MapConstants.MapPlaza;

    public TeleportService(
        IGameActionDispatcher dispatcher,
        IWaypointRegistry registry,
        IEventBus eventBus) : base(dispatcher)
    {
        Registry = registry;
        _eventBus = eventBus;
    }

    public override ValueTask<FeatureResult> StartAsync(CancellationToken ct = default)
    {
        IsRunning = true;
        return ValueTask.FromResult(FeatureResult.Ok("Teleport Service ready"));
    }

    public override ValueTask<FeatureResult> StopAsync(CancellationToken ct = default)
    {
        IsRunning = false;
        return ValueTask.FromResult(FeatureResult.Ok("Teleport Service stopped"));
    }

    public async ValueTask<FeatureResult> TeleportToAsync(TelePositionDTO destination, bool alignCamera = true, CancellationToken ct = default)
    {
        return await TeleportCoordinatesAsync(
            destination.MapId,
            destination.Position,
            destination.Rotation,
            destination.Viewpoint,
            alignCamera,
            ct
        );
    }

    public async ValueTask<FeatureResult> TeleportCoordinatesAsync(
        int mapId,
        Vector3 position,
        Quaternion rotation,
        CharacterViewpoint viewpoint,
        bool alignCamera = true,
        CancellationToken ct = default)
    {
        // 1. Cross-map check
        if (_currentMapId != mapId)
        {
            var zoneAction = new GameAction
            {
                Type = GameActionType.ConnectToZoneMove,
                IntParam = mapId,
                Priority = ActionPriority.High,
                TimeoutMs = 15000
            };
            var zoneRes = await Dispatcher.EnqueueAsync(zoneAction, ct);
            if (!zoneRes.Success)
            {
                return FeatureResult.Fail($"Chuyển map sang {MapConstants.GetMapName(mapId)} thất bại: {zoneRes.Message}");
            }
            int prevMap = _currentMapId;
            _currentMapId = mapId;
            _eventBus.Publish(new MapChangedEvent(prevMap, mapId, MapConstants.GetMapName(mapId)));
        }

        // 2. Anti-fall elevation calculation (0.1m safe elevation clearance)
        float safeY = position.Y;
        if (safeY < 0.1f) safeY = 0.15f; // ensure elevation buffer

        var adjustedPos = new Vector3(position.X, safeY, position.Z);

        // 3. Dispatch teleport action with Position, Rotation, AND Character Viewpoint
        var teleAction = new GameAction
        {
            Type = GameActionType.Teleport,
            VectorParam = adjustedPos,
            RotationParam = rotation,
            ViewpointParam = viewpoint,
            Priority = ActionPriority.High,
            TimeoutMs = 5000
        };

        var teleRes = await Dispatcher.EnqueueAsync(teleAction, ct);
        if (!teleRes.Success)
        {
            _eventBus.Publish(new TeleportExecutedEvent(adjustedPos, rotation, viewpoint, false));
            return FeatureResult.Fail($"Lỗi dịch chuyển: {teleRes.Message}");
        }

        // 4. Align camera behind player if requested
        if (alignCamera && viewpoint.AlignCameraBehind)
        {
            var camAction = new GameAction
            {
                Type = GameActionType.AlignCamera,
                FloatParam = viewpoint.Yaw,
                Priority = ActionPriority.Normal
            };
            await Dispatcher.EnqueueAsync(camAction, ct);
        }

        Stats.TotalTeleports++;
        Stats.LastPosition = adjustedPos;
        Stats.LastViewpoint = viewpoint;

        _eventBus.Publish(new TeleportExecutedEvent(adjustedPos, rotation, viewpoint, true));
        return FeatureResult.Ok($"Đã dịch chuyển tới {adjustedPos} với góc nhìn {viewpoint}");
    }
}

public sealed class TeleportOptions
{
    public bool AlignCameraDefault { get; set; } = true;
    public float SafeElevationClearance { get; set; } = 0.10f;
}

public sealed class TeleportStats
{
    public int TotalTeleports { get; set; }
    public Vector3 LastPosition { get; set; }
    public CharacterViewpoint LastViewpoint { get; set; } = new();
}
