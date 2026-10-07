using System.Windows;
using System.Windows.Controls;
using DTA.Core.ActionDispatcher;
using DTA.Core.Cache;
using DTA.Core.Events;
using DTA.Features.Excavation;
using DTA.Features.Fishing;
using DTA.Features.Insect;
using DTA.Features.Mining;
using DTA.Features.Teleport;
using DTA.Shared.Models;

namespace DTA.Client.Gui;

public partial class MainWindow : Window
{
    private readonly GameActionDispatcher _dispatcher;
    private readonly EventBus _eventBus;
    private readonly WaypointRegistry _waypointRegistry;
    private readonly TeleportService _teleportService;
    private readonly FishingService _fishingService;
    private readonly MiningService _miningService;
    private readonly InsectService _insectService;
    private readonly ExcavationService _excavationService;

    private List<TelePositionDTO> _displayedWaypoints = [];

    public MainWindow()
    {
        InitializeComponent();

        // 1. Initialize Subsystems
        _dispatcher = new GameActionDispatcher();
        _eventBus = new EventBus();
        var cache = new MemoryCache<int, FishItemDTO>();
        var catalog = new FishingCatalog(cache);

        // 2. Initialize Services
        _waypointRegistry = new WaypointRegistry();
        _teleportService = new TeleportService(_dispatcher, _waypointRegistry, _eventBus);
        _fishingService = new FishingService(_dispatcher, catalog);
        _miningService = new MiningService(_dispatcher);
        _insectService = new InsectService(_dispatcher);
        _excavationService = new ExcavationService(_dispatcher);

        // 3. Initialize Filters & Data
        InitFilters();
        RefreshWaypoints();
    }

    private void InitFilters()
    {
        CmbMapFilter.Items.Add("Tất cả bản đồ");
        CmbMapFilter.Items.Add("Plaza (1001)");
        CmbMapFilter.Items.Add("Khu nghỉ dưỡng (1301)");
        CmbMapFilter.Items.Add("Khu cắm trại (1201)");
        CmbMapFilter.Items.Add("Khu trung tâm (1101)");
        CmbMapFilter.Items.Add("Khu mỏ (21001)");
        CmbMapFilter.SelectedIndex = 0;
    }

    private void RefreshWaypoints()
    {
        string search = TxtSearchTeleport?.Text?.Trim().ToLower() ?? "";
        int mapFilter = CmbMapFilter?.SelectedIndex switch
        {
            1 => MapConstants.MapPlaza,
            2 => MapConstants.MapResort,
            3 => MapConstants.MapCamp,
            4 => MapConstants.MapDowntown,
            5 => MapConstants.MapMine,
            _ => 0
        };

        var query = _waypointRegistry.GetAllWaypoints().AsEnumerable();
        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(w => w.Name.ToLower().Contains(search) || w.MapName.ToLower().Contains(search));
        }
        if (mapFilter > 0)
        {
            query = query.Where(w => w.MapId == mapFilter);
        }

        _displayedWaypoints = query.ToList();
        GridWaypoints.ItemsSource = _displayedWaypoints;
    }

    private void NavTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string tag)
        {
            PanelTeleport.Visibility = tag == "PanelTeleport" ? Visibility.Visible : Visibility.Collapsed;
            PanelFishing.Visibility = tag == "PanelFishing" ? Visibility.Visible : Visibility.Collapsed;
            PanelMining.Visibility = tag == "PanelMining" ? Visibility.Visible : Visibility.Collapsed;
            PanelInsect.Visibility = tag == "PanelInsect" ? Visibility.Visible : Visibility.Collapsed;
            PanelExcavation.Visibility = tag == "PanelExcavation" ? Visibility.Visible : Visibility.Collapsed;
            PanelFarm.Visibility = tag == "PanelFarm" ? Visibility.Visible : Visibility.Collapsed;
            PanelCollect.Visibility = tag == "PanelCollect" ? Visibility.Visible : Visibility.Collapsed;
            PanelEsp.Visibility = tag == "PanelEsp" ? Visibility.Visible : Visibility.Collapsed;
            PanelSettings.Visibility = tag == "PanelSettings" ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void TxtSearchTeleport_TextChanged(object sender, TextChangedEventArgs e) => RefreshWaypoints();
    private void CmbMapFilter_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshWaypoints();

    private async void BtnExecuteTeleport_Click(object sender, RoutedEventArgs e)
    {
        if (GridWaypoints.SelectedItem is not TelePositionDTO selected)
        {
            TxtStatusLog.Text = "Vui lòng chọn một điểm trong danh sách để dịch chuyển!";
            return;
        }

        TxtStatusLog.Text = $"Đang dịch chuyển tới '{selected.Name}' kèm góc nhìn nhân vật ({selected.Viewpoint})...";
        var res = await _teleportService.TeleportToAsync(selected, alignCamera: true);

        if (res.Success)
        {
            TxtStatusLog.Text = $"[Thành công] Đã tới '{selected.Name}' tại {selected.Position} kèm góc nhìn nhân vật ({selected.Viewpoint})";
        }
        else
        {
            TxtStatusLog.Text = $"[Thất bại] {res.Message}";
        }
    }

    private void BtnAddCurrentPos_Click(object sender, RoutedEventArgs e)
    {
        // Custom prompt to add waypoint with character viewpoint
        var newPos = new Vector3(10.5f, 1.2f, -25.0f);
        var newRot = Quaternion.FromEuler(0f, 90f, 0f);
        var newViewpoint = new CharacterViewpoint(90f, 10f, 4.5f, true);

        var added = _waypointRegistry.AddLocalWaypoint("Bãi câu cá nhân #1", MapConstants.MapPlaza, newPos, newRot, newViewpoint, "Điểm thêm thủ công");
        RefreshWaypoints();
        TxtStatusLog.Text = $"Đã lưu vị trí '{added.Name}' kèm góc nhìn nhân vật ({added.Viewpoint})!";
    }

    private async void BtnToggleFishing_Click(object sender, RoutedEventArgs e)
    {
        if (!_fishingService.IsRunning)
        {
            await _fishingService.StartAsync();
            BtnToggleFishing.Content = "⏹ DỪNG CÂU";
            BtnToggleFishing.Background = System.Windows.Media.Brushes.Red;
            TxtStatusLog.Text = "Đang chạy bot câu cá tự động...";

            // Simulate cycle
            await _fishingService.CastRodAsync();
            await Task.Delay(400);
            await _fishingService.ReelInAsync();

            TxtFishCaught.Text = $"{_fishingService.Stats.TotalCaught} con";
            TxtFishSold.Text = $"{_fishingService.Stats.TotalSold} con";
            TxtRodDurability.Text = $"{_fishingService.Stats.RodDurability}%";
            TxtStatusLog.Text = $"Đã câu thành công cá {_fishingService.Stats.TotalCaught}!";
        }
        else
        {
            await _fishingService.StopAsync();
            BtnToggleFishing.Content = "▶ BẮT ĐẦU CÂU";
            BtnToggleFishing.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129));
            TxtStatusLog.Text = "Đã dừng bot câu cá.";
        }
    }

    private async void BtnToggleMining_Click(object sender, RoutedEventArgs e)
    {
        if (!_miningService.IsRunning)
        {
            await _miningService.StartAsync();
            BtnToggleMining.Content = "⏹ DỪNG ĐẬP ĐÁ";
            TxtStatusLog.Text = "Đang quét mạch đá & quặng...";

            var rock = new RockEntity { Uid = 101, Position = new Vector3(15, 0, 30), Hp = 1 };
            await _miningService.MineRockAsync(rock);

            TxtRocksMined.Text = _miningService.Stats.TotalRocksMined.ToString();
            TxtStatusLog.Text = "Đã đập vỡ 1 mạch đá!";
        }
        else
        {
            await _miningService.StopAsync();
            BtnToggleMining.Content = "▶ BẮT ĐẦU ĐẬP ĐÁ";
            TxtStatusLog.Text = "Đã dừng bot đập đá.";
        }
    }

    private async void BtnToggleInsect_Click(object sender, RoutedEventArgs e)
    {
        if (!_insectService.IsRunning)
        {
            await _insectService.StartAsync();
            BtnToggleInsect.Content = "⏹ DỪNG BẮT BỌ";
            TxtStatusLog.Text = "Đang tiếp cận côn trùng...";

            var bug = new BugEntity { Uid = 202, Name = "Bướm xanh", Grade = 3, Position = new Vector3(5, 0, 5) };
            await _insectService.CatchBugAsync(bug, new Vector3(4, 0, 4));

            TxtBugsCaught.Text = _insectService.Stats.TotalCaught.ToString();
            TxtStatusLog.Text = "Đã bắt được 1 con bướm!";
        }
        else
        {
            await _insectService.StopAsync();
            BtnToggleInsect.Content = "▶ BẮT ĐẦU BẮT BỌ";
            TxtStatusLog.Text = "Đã dừng bot bắt bọ.";
        }
    }

    private async void BtnToggleExcavation_Click(object sender, RoutedEventArgs e)
    {
        if (!_excavationService.IsRunning)
        {
            await _excavationService.StartAsync();
            BtnToggleExcavation.Content = "⏹ DỪNG ĐÀO";
            TxtStatusLog.Text = "Đang đào cổ vật...";

            var relic = new ExcavationSpot { Uid = 303, AssetName = "Pyramid", Hp = 100 };
            await _excavationService.DigSpotAsync(relic);

            TxtRelicsDug.Text = _excavationService.Stats.TotalExcavated.ToString();
            TxtStatusLog.Text = "Đã hoàn thành đào 1 kim tự tháp!";
        }
        else
        {
            await _excavationService.StopAsync();
            BtnToggleExcavation.Content = "▶ BẮT ĐẦU ĐÀO";
            TxtStatusLog.Text = "Đã dừng bot đào cổ vật.";
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _dispatcher.Dispose();
        base.OnClosed(e);
    }
}
