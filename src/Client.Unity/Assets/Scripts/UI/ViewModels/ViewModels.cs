using System.ComponentModel;
using System.Runtime.CompilerServices;
using DTA.Features.Fishing;
using DTA.Features.Mining;
using DTA.Features.Insect;
using DTA.Features.Excavation;
using DTA.Features.Farm;
using DTA.Features.Collect;
using DTA.Features.Teleport;
using DTA.Features.Esp;
using DTA.Features.Settings;
using DTA.Shared.Models;
using DTA.Shared.Protocol;

namespace DTA.UI.ViewModels;

public abstract class BaseViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

public sealed class TopBarViewModel : BaseViewModel
{
    private string _statusText = "Đang kết nối...";
    private string _statusColor = "#38bdf8";
    private string _gameVersion = "Play Together v227628";
    private string _platformName = "LDPlayer";

    public string StatusText { get => _statusText; set => SetField(ref _statusText, value); }
    public string StatusColor { get => _statusColor; set => SetField(ref _statusColor, value); }
    public string GameVersion { get => _gameVersion; set => SetField(ref _gameVersion, value); }
    public string PlatformName { get => _platformName; set => SetField(ref _platformName, value); }
}

public sealed class SidebarItemViewModel : BaseViewModel
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Icon { get; init; } = string.Empty;
    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set => SetField(ref _isSelected, value); }
}

public sealed class TeleportItemViewModel : BaseViewModel
{
    public TelePositionDTO Data { get; }

    public string Id => Data.Id;
    public string Name => Data.Name;
    public string MapName => Data.MapName;
    public string Category => Data.Category;
    public string CoordinatesText => $"XYZ: ({Data.X:F1}, {Data.Y:F1}, {Data.Z:F1})";
    public string ViewpointText => $"Góc nhìn: {Data.Viewpoint}";

    public TeleportItemViewModel(TelePositionDTO data)
    {
        Data = data;
    }
}

public sealed class TeleportViewModel : BaseViewModel
{
    private readonly ITeleportService _teleportService;
    private string _searchText = string.Empty;
    private string _selectedCategory = "all";
    private string _statusMessage = "Sẵn sàng";
    private TeleportItemViewModel? _selectedItem;

    public List<TeleportItemViewModel> Items { get; private set; } = [];

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetField(ref _searchText, value))
                FilterItems();
        }
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetField(ref _selectedCategory, value))
                FilterItems();
        }
    }

    public string StatusMessage { get => _statusMessage; set => SetField(ref _statusMessage, value); }
    public TeleportItemViewModel? SelectedItem { get => _selectedItem; set => SetField(ref _selectedItem, value); }

    public TeleportViewModel(ITeleportService teleportService)
    {
        _teleportService = teleportService;
        RefreshItems();
    }

    public void RefreshItems()
    {
        var raw = _teleportService.Registry.GetAllWaypoints();
        Items = raw.Select(w => new TeleportItemViewModel(w)).ToList();
        FilterItems();
    }

    private void FilterItems()
    {
        var query = _teleportService.Registry.GetAllWaypoints().AsEnumerable();
        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            query = query.Where(p => p.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                                     p.MapName.Contains(_searchText, StringComparison.OrdinalIgnoreCase));
        }
        if (_selectedCategory != "all")
        {
            query = query.Where(p => p.Category == _selectedCategory);
        }

        Items = query.Select(w => new TeleportItemViewModel(w)).ToList();
        OnPropertyChanged(nameof(Items));
    }

    public async Task<bool> TeleportToSelectedAsync()
    {
        if (SelectedItem == null)
        {
            StatusMessage = "Vui lòng chọn một vị trí để dịch chuyển!";
            return false;
        }

        StatusMessage = $"Đang dịch chuyển tới {SelectedItem.Name} kèm góc nhìn nhân vật ({SelectedItem.Data.Viewpoint})...";
        var res = await _teleportService.TeleportToAsync(SelectedItem.Data, true);
        StatusMessage = res.Message;
        return res.Success;
    }

    public void SaveCurrentLocation(string name, int mapId, Vector3 pos, Quaternion rot, CharacterViewpoint viewpoint, string desc = "")
    {
        var added = _teleportService.Registry.AddLocalWaypoint(name, mapId, pos, rot, viewpoint, desc);
        StatusMessage = $"Đã lưu vị trí '{added.Name}' kèm góc nhìn ({added.Viewpoint})";
        RefreshItems();
    }
}

public sealed class AppShellViewModel : BaseViewModel
{
    public TopBarViewModel TopBar { get; } = new();
    public List<SidebarItemViewModel> NavItems { get; } = [];
    private BaseViewModel? _currentScreen;

    public BaseViewModel? CurrentScreen { get => _currentScreen; set => SetField(ref _currentScreen, value); }

    public TeleportViewModel TeleportVM { get; }
    public IFishingService FishingService { get; }
    public IMiningService MiningService { get; }
    public IInsectService InsectService { get; }
    public IExcavationService ExcavationService { get; }
    public IFarmService FarmService { get; }
    public ICollectService CollectService { get; }
    public IEspService EspService { get; }
    public ISettingsService SettingsService { get; }

    public AppShellViewModel(
        TeleportViewModel teleportVM,
        IFishingService fishingService,
        IMiningService miningService,
        IInsectService insectService,
        IExcavationService excavationService,
        IFarmService farmService,
        ICollectService collectService,
        IEspService espService,
        ISettingsService settingsService)
    {
        TeleportVM = teleportVM;
        FishingService = fishingService;
        MiningService = miningService;
        InsectService = insectService;
        ExcavationService = excavationService;
        FarmService = farmService;
        CollectService = collectService;
        EspService = espService;
        SettingsService = settingsService;

        // Populate navigation items
        NavItems =
        [
            new SidebarItemViewModel { Id = "teleport", Title = "Dịch chuyển", Icon = "teleport", IsSelected = true },
            new SidebarItemViewModel { Id = "fishing", Title = "Câu cá", Icon = "fishing" },
            new SidebarItemViewModel { Id = "mining", Title = "Đập đá", Icon = "mining" },
            new SidebarItemViewModel { Id = "insect", Title = "Bắt bọ", Icon = "insect" },
            new SidebarItemViewModel { Id = "excavation", Title = "Đào cổ vật", Icon = "excavation" },
            new SidebarItemViewModel { Id = "farm", Title = "Nông trại", Icon = "farm" },
            new SidebarItemViewModel { Id = "collect", Title = "Thu thập", Icon = "collect" },
            new SidebarItemViewModel { Id = "esp", Title = "ESP & Radar", Icon = "esp" },
            new SidebarItemViewModel { Id = "settings", Title = "Cài đặt", Icon = "settings" }
        ];

        CurrentScreen = TeleportVM;
    }

    public void SelectTab(string tabId)
    {
        foreach (var item in NavItems)
            item.IsSelected = item.Id == tabId;

        if (tabId == "teleport")
            CurrentScreen = TeleportVM;
    }
}
