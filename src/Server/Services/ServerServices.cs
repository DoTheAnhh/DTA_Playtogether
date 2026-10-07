using System.Text.Json;
using DTA.Shared.Models;
using DTA.Shared.Protocol;

namespace DTA.Server.Services;

public interface IServerTeleportService
{
    int DataVersion { get; }
    IReadOnlyList<TelePositionDTO> GetAllPositions();
    IReadOnlyList<TelePositionDTO> GetPositionsByMap(int mapId);
    TelePositionDTO? GetById(string id);
    TelePositionDTO UpsertPosition(TelePositionDTO pos);
    bool DeletePosition(string id);
}

public sealed class ServerTeleportService : IServerTeleportService
{
    private readonly List<TelePositionDTO> _positions = [];
    private readonly object _lock = new();
    public int DataVersion { get; private set; } = 1;

    public ServerTeleportService()
    {
        // Seed standard authoritative server positions with character rotation and character viewpoint
        _positions.AddRange(
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
                "server_downtown_station",
                "Trạm trung tâm Downtown",
                MapConstants.MapDowntown,
                "Khu trung tâm",
                -120.0f, 2.0f, 55.4f,
                Quaternion.FromEuler(0f, 180f, 0f),
                new CharacterViewpoint(180f, 10f, 4.5f, true),
                "server",
                "Trạm tàu khu trung tâm",
                4
            ),
            new TelePositionDTO(
                "server_mine_ore_room",
                "Hầm quặng Khu mỏ",
                MapConstants.MapMine,
                "Khu mỏ khoáng sản",
                15.20f, -8.0f, 62.10f,
                Quaternion.FromEuler(0f, 60f, 0f),
                new CharacterViewpoint(60f, 5f, 4.0f, true),
                "server",
                "Mỏ khoáng sản tầng 1",
                5
            )
        ]);
    }

    public IReadOnlyList<TelePositionDTO> GetAllPositions()
    {
        lock (_lock)
        {
            return [.. _positions];
        }
    }

    public IReadOnlyList<TelePositionDTO> GetPositionsByMap(int mapId)
    {
        lock (_lock)
        {
            return _positions.Where(p => p.MapId == mapId).OrderBy(p => p.OrderIndex).ToList();
        }
    }

    public TelePositionDTO? GetById(string id)
    {
        lock (_lock)
        {
            return _positions.FirstOrDefault(p => p.Id == id);
        }
    }

    public TelePositionDTO UpsertPosition(TelePositionDTO pos)
    {
        lock (_lock)
        {
            int idx = _positions.FindIndex(p => p.Id == pos.Id);
            if (idx >= 0)
            {
                pos.Version = _positions[idx].Version + 1;
                _positions[idx] = pos;
            }
            else
            {
                _positions.Add(pos);
            }
            DataVersion++;
            return pos;
        }
    }

    public bool DeletePosition(string id)
    {
        lock (_lock)
        {
            int idx = _positions.FindIndex(p => p.Id == id);
            if (idx >= 0)
            {
                _positions.RemoveAt(idx);
                DataVersion++;
                return true;
            }
            return false;
        }
    }
}

public interface IServerUIService
{
    AppNavigationDTO GetNavigation();
    UIScreenDTO? GetScreen(string screenId);
}

public sealed class ServerUIService : IServerUIService
{
    private readonly Dictionary<string, UIScreenDTO> _screens = [];

    public ServerUIService()
    {
        InitScreens();
    }

    private void InitScreens()
    {
        _screens["teleport"] = new UIScreenDTO
        {
            Id = "teleport",
            Title = "Dịch chuyển",
            Icon = "teleport",
            Order = 1,
            Components =
            [
                new UIComponentDTO { Id = "tele_status", Type = UIComponentTypes.StatusCard, Text = "Sẵn sàng dịch chuyển" },
                new UIComponentDTO { Id = "tele_search", Type = UIComponentTypes.Entry, Text = "Tìm kiếm vị trí..." },
                new UIComponentDTO { Id = "tele_table", Type = UIComponentTypes.DataTable, Text = "Danh sách điểm dịch chuyển" },
                new UIComponentDTO { Id = "tele_action_warp", Type = UIComponentTypes.Button, Text = "Dịch chuyển", Props = new() { ["variant"] = "primary" } }
            ]
        };

        _screens["fishing"] = new UIScreenDTO
        {
            Id = "fishing",
            Title = "Câu cá",
            Icon = "fishing",
            Order = 2,
            Components =
            [
                new UIComponentDTO { Id = "fish_status", Type = UIComponentTypes.StatusCard, Text = "Sẵn sàng câu cá" },
                new UIComponentDTO { Id = "fish_action_mode", Type = UIComponentTypes.Segment, Text = "Hành động", Value = "keep", Options = ["Bảo quản", "Bán nhanh"] },
                new UIComponentDTO { Id = "fish_auto_repair", Type = UIComponentTypes.Toggle, Text = "Tự sửa cần", Value = true },
                new UIComponentDTO { Id = "fish_fast_bite", Type = UIComponentTypes.Toggle, Text = "Cá cắn nhanh", Value = false },
                new UIComponentDTO { Id = "fish_lock_pov", Type = UIComponentTypes.Toggle, Text = "Khóa POV camera", Value = false },
                new UIComponentDTO { Id = "fish_btn_start", Type = UIComponentTypes.Button, Text = "Bắt đầu", Props = new() { ["variant"] = "primary" } }
            ]
        };

        _screens["mining"] = new UIScreenDTO
        {
            Id = "mining",
            Title = "Đập đá",
            Icon = "mining",
            Order = 3,
            Components =
            [
                new UIComponentDTO { Id = "mining_status", Type = UIComponentTypes.StatusCard, Text = "Sẵn sàng đập đá" },
                new UIComponentDTO { Id = "mining_kind", Type = UIComponentTypes.Segment, Text = "Loại quặng", Value = "all", Options = ["Tất cả", "Đá thường", "Quặng sự kiện"] },
                new UIComponentDTO { Id = "mining_btn_start", Type = UIComponentTypes.Button, Text = "Bắt đầu", Props = new() { ["variant"] = "primary" } }
            ]
        };

        _screens["insect"] = new UIScreenDTO
        {
            Id = "insect",
            Title = "Bắt bọ",
            Icon = "insect",
            Order = 4,
            Components =
            [
                new UIComponentDTO { Id = "insect_status", Type = UIComponentTypes.StatusCard, Text = "Sẵn sàng bắt bọ" },
                new UIComponentDTO { Id = "insect_btn_start", Type = UIComponentTypes.Button, Text = "Bắt đầu", Props = new() { ["variant"] = "primary" } }
            ]
        };
    }

    public AppNavigationDTO GetNavigation() =>
        new()
        {
            Version = 1,
            Features = _screens.Values.OrderBy(s => s.Order).ToList()
        };

    public UIScreenDTO? GetScreen(string screenId) =>
        _screens.TryGetValue(screenId, out var s) ? s : null;
}

public interface IServerLicenseService
{
    LicenseDTO Verify(string key, string hwid);
}

public sealed class ServerLicenseService : IServerLicenseService
{
    public LicenseDTO Verify(string key, string hwid)
    {
        return new LicenseDTO
        {
            Key = key,
            HardwareId = hwid,
            Role = "vip",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds(),
            EnabledFeatures = ["Fishing", "Mining", "Insect", "Excavation", "Farm", "Collect", "Teleport", "Esp", "Settings"],
            Signature = "verified_rsa256"
        };
    }
}
