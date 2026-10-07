using System.Text.Json;
using DTA.Core.ActionDispatcher;
using DTA.Core.Cache;
using DTA.Core.Events;
using DTA.Features.Collect;
using DTA.Features.Esp;
using DTA.Features.Excavation;
using DTA.Features.Farm;
using DTA.Features.Fishing;
using DTA.Features.Insect;
using DTA.Features.Mining;
using DTA.Features.Settings;
using DTA.Features.Teleport;
using DTA.Platform;
using DTA.Shared.Models;
using DTA.Shared.Protocol;
using DTA.UI.ViewModels;

namespace DTA.Launcher;

internal class Program
{
    private static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.Title = "DTA Play Together — Client Hub (C# + Unity Engine)";

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(@"
==================================================================
        DTA PLAY TOGETHER — C# / UNITY AUTOMATION SYSTEM
           Tái cấu trúc 100% C# — Clean Architecture
==================================================================");
        Console.ResetColor();

        // 1. Initialize Platform & Dispatcher
        var platform = new MockPlatformAdapter();
        var instances = await platform.DiscoverInstancesAsync();
        Console.WriteLine($"[+] Phát hiện {instances.Count} thiết bị / giả lập:");
        foreach (var inst in instances)
        {
            Console.WriteLine($"    - [{inst.Kind}] {inst.Name} ({inst.Host}:{inst.Port})");
        }

        await platform.AttachAsync(instances[0]);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[+] Đã kết nối thành công tới thiết bị: {platform.CurrentInstance.Name}");
        Console.ResetColor();

        // 2. Initialize Core Subsystems
        using var dispatcher = new GameActionDispatcher();
        var eventBus = new EventBus();
        var fishCache = new MemoryCache<int, FishItemDTO>();
        var catalog = new FishingCatalog(fishCache);

        // 3. Initialize Feature Services
        var waypointRegistry = new WaypointRegistry();
        var teleService = new TeleportService(dispatcher, waypointRegistry, eventBus);
        var fishingService = new FishingService(dispatcher, catalog);
        var miningService = new MiningService(dispatcher);
        var insectService = new InsectService(dispatcher);
        var excavateService = new ExcavationService(dispatcher);
        var farmService = new FarmService(dispatcher);
        var collectService = new CollectService(dispatcher);
        var espService = new EspService();
        var settingsService = new SettingsService();

        // 4. Initialize UI ViewModels
        var teleVM = new TeleportViewModel(teleService);
        var appShellVM = new AppShellViewModel(
            teleVM, fishingService, miningService, insectService,
            excavateService, farmService, collectService, espService, settingsService
        );

        // 5. Check Server Connection
        await TrySyncServerPositionsAsync(waypointRegistry);

        Console.WriteLine("\n[i] Khởi tạo hoàn tất. Hệ thống sẵn sàng nhận lệnh.\n");

        // 6. Interactive Command Loop
        bool running = true;
        while (running)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("------------------------- MENU CHỨC NĂNG -------------------------");
            Console.WriteLine(" 1. [Teleport] Xem danh sách vị trí (Kèm góc nhìn nhân vật & camera)");
            Console.WriteLine(" 2. [Teleport] Dịch chuyển tới vị trí chỉ định");
            Console.WriteLine(" 3. [Teleport] Thêm vị trí mới (Lưu toạ độ + hướng xoay + góc nhìn POV)");
            Console.WriteLine(" 4. [Fishing]  Thử thả cần câu (Fishing Bot)");
            Console.WriteLine(" 5. [Mining]   Thử đập đá (Mining Bot)");
            Console.WriteLine(" 6. [Insect]   Thử vung vợt bắt bọ (Insect Bot)");
            Console.WriteLine(" 7. [Excavate] Thử đào cổ vật (Excavation Bot)");
            Console.WriteLine(" 8. [Server]   Đồng bộ lại dữ liệu từ Server (http://localhost:5000)");
            Console.WriteLine(" 0. Thoát chương trình");
            Console.WriteLine("------------------------------------------------------------------");
            Console.ResetColor();
            Console.Write("Nhập lựa chọn của bạn (0-8): ");

            var input = Console.ReadLine()?.Trim();
            switch (input)
            {
                case "1":
                    ShowTeleportWaypoints(waypointRegistry);
                    break;

                case "2":
                    await ExecuteTeleportAsync(teleService, waypointRegistry);
                    break;

                case "3":
                    AddCustomWaypoint(waypointRegistry);
                    break;

                case "4":
                    await RunFishingTestAsync(fishingService);
                    break;

                case "5":
                    await RunMiningTestAsync(miningService);
                    break;

                case "6":
                    await RunInsectTestAsync(insectService);
                    break;

                case "7":
                    await RunExcavationTestAsync(excavateService);
                    break;

                case "8":
                    await TrySyncServerPositionsAsync(waypointRegistry);
                    break;

                case "0":
                    running = false;
                    break;

                default:
                    Console.WriteLine("[-] Lựa chọn không hợp lệ, vui lòng thử lại.");
                    break;
            }
            Console.WriteLine();
        }

        Console.WriteLine("[*] Tạm biệt!");
    }

    private static void ShowTeleportWaypoints(IWaypointRegistry registry)
    {
        var waypoints = registry.GetAllWaypoints();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n=== DANH SÁCH {waypoints.Count} ĐIỂM DỊCH CHUYỂN ===");
        int idx = 1;
        foreach (var wp in waypoints)
        {
            Console.WriteLine($"[{idx++}] {wp.Name} ({wp.MapName}) [{wp.Category.ToUpper()}]");
            Console.WriteLine($"     Toạ độ:    XYZ=({wp.X:F2}, {wp.Y:F2}, {wp.Z:F2})");
            Console.WriteLine($"     Hướng xoay: Quaternion={wp.Rotation}");
            Console.WriteLine($"     Góc nhìn:  {wp.Viewpoint}");
        }
        Console.ResetColor();
    }

    private static async Task ExecuteTeleportAsync(ITeleportService teleService, IWaypointRegistry registry)
    {
        var waypoints = registry.GetAllWaypoints();
        ShowTeleportWaypoints(registry);
        Console.Write("\nNhập số thứ tự điểm muốn dịch chuyển (1 - " + waypoints.Count + "): ");
        if (int.TryParse(Console.ReadLine(), out int choice) && choice >= 1 && choice <= waypoints.Count)
        {
            var target = waypoints[choice - 1];
            Console.WriteLine($"\n[*] Đang dịch chuyển tới '{target.Name}'...");
            Console.WriteLine($"    -> Đặt toạ độ ({target.X:F2}, {target.Y:F2}, {target.Z:F2})");
            Console.WriteLine($"    -> Hướng xoay nhân vật: {target.Rotation}");
            Console.WriteLine($"    -> Căn chỉnh góc nhìn camera: {target.Viewpoint}");

            var res = await teleService.TeleportToAsync(target, alignCamera: true);
            if (res.Success)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[✓] {res.Message}");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[✗] {res.Message}");
            }
            Console.ResetColor();
        }
        else
        {
            Console.WriteLine("[-] Số thứ tự không hợp lệ.");
        }
    }

    private static void AddCustomWaypoint(IWaypointRegistry registry)
    {
        Console.Write("Nhập tên điểm mới: ");
        string name = Console.ReadLine()?.Trim() ?? "Vị trí mới";

        Console.Write("Nhập toạ độ X (vd: 25.5): ");
        float.TryParse(Console.ReadLine(), out float x);

        Console.Write("Nhập toạ độ Y (vd: 1.5): ");
        float.TryParse(Console.ReadLine(), out float y);

        Console.Write("Nhập toạ độ Z (vd: -40.0): ");
        float.TryParse(Console.ReadLine(), out float z);

        Console.Write("Nhập góc quay ngang Yaw (0 - 360 độ): ");
        float.TryParse(Console.ReadLine(), out float yaw);

        Console.Write("Nhập góc ngẩng Pitch (-30 đến 60 độ): ");
        float.TryParse(Console.ReadLine(), out float pitch);

        var rot = Quaternion.FromEuler(pitch, yaw, 0f);
        var viewpoint = new CharacterViewpoint(yaw, pitch, 4.5f, true);

        var added = registry.AddLocalWaypoint(name, MapConstants.MapPlaza, new Vector3(x, y, z), rot, viewpoint, "Điểm tự lưu");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[✓] Đã lưu thành công điểm '{added.Name}' kèm góc nhìn ({added.Viewpoint})!");
        Console.ResetColor();
    }

    private static async Task RunFishingTestAsync(IFishingService fishingService)
    {
        await fishingService.StartAsync();
        Console.WriteLine("[*] Bắt đầu phiên câu cá...");
        var castRes = await fishingService.CastRodAsync();
        Console.WriteLine($"    [1] Thả cần: {castRes.Message} (Độ bền cần: {fishingService.Stats.RodDurability}%)");
        await Task.Delay(500);
        var reelRes = await fishingService.ReelInAsync();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"    [2] Kéo cần: {reelRes.Message} (Tổng câu: {fishingService.Stats.TotalCaught})");
        Console.ResetColor();
    }

    private static async Task RunMiningTestAsync(IMiningService miningService)
    {
        await miningService.StartAsync();
        Console.WriteLine("[*] Bắt đầu phiên đập đá...");
        var rock = new RockEntity { Uid = 555, Position = new Vector3(12, 0, 45), Hp = 2 };
        var res1 = await miningService.MineRockAsync(rock);
        Console.WriteLine($"    [1] Lần đập 1: {res1.Message} (Máu còn lại: {rock.Hp})");
        var res2 = await miningService.MineRockAsync(rock);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"    [2] Lần đập 2: {res2.Message} (Đã vỡ quặng! Tổng đập: {miningService.Stats.TotalRocksMined})");
        Console.ResetColor();
    }

    private static async Task RunInsectTestAsync(IInsectService insectService)
    {
        await insectService.StartAsync();
        var bug = new BugEntity { Uid = 777, Name = "Bướm xanh khổng lồ", Grade = 4, Position = new Vector3(5, 0, 5) };
        var playerPos = new Vector3(4f, 0, 4f);
        var res = await insectService.CatchBugAsync(bug, playerPos);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[✓] Kết quả bắt bọ: {res.Message} (Tổng bọ: {insectService.Stats.TotalCaught})");
        Console.ResetColor();
    }

    private static async Task RunExcavationTestAsync(IExcavationService excavateService)
    {
        await excavateService.StartAsync();
        var spot = new ExcavationSpot { Uid = 888, AssetName = "Kho báu sa mạc cổ đại", Hp = 200 };
        var res = await excavateService.DigSpotAsync(spot);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[✓] Kết quả đào: {res.Message} (Tổng cổ vật: {excavateService.Stats.TotalExcavated})");
        Console.ResetColor();
    }

    private static async Task TrySyncServerPositionsAsync(IWaypointRegistry registry)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var response = await http.GetAsync("http://localhost:5000/api/teleport/positions");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var envelope = JsonSerializer.Deserialize<ResponseEnvelope<List<TelePositionDTO>>>(json);
                if (envelope?.Payload != null && envelope.Payload.Count > 0)
                {
                    registry.SetServerWaypoints(envelope.Payload);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[✓] Đã đồng bộ thành công {envelope.Payload.Count} điểm dịch chuyển từ Server (v{envelope.DataVersion})!");
                    Console.ResetColor();
                    return;
                }
            }
        }
        catch
        {
            // Server might not be running yet
        }
        Console.WriteLine("[i] Server http://localhost:5000 chưa bật. Đang sử dụng danh mục vị trí Offline.");
    }
}
