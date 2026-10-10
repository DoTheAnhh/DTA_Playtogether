using DTA.Ui;
using DTA.Engine.Bots;
using DTA.Engine.Core;
using DTA.Features.Fishing;
using DTA.Runtime.Core;
using DTA.Runtime.Device;
using DTA.Runtime.Storage;

namespace DTA.App.Views.Fishing;

/// <summary>Tab giả lập + bot câu (mỗi tab đúng 1 bot), catalog ID cá theo bản game đang cài, cập nhật catalog.</summary>
public sealed partial class FishingView
{
    private static readonly Logger L = Log.For("ui");
    private readonly Dictionary<string, (FishingBot Bot, FishingOptions Options)> _bots = [];
    private FishingBot? _bot;
    private FishCatalog _catalog = FishCatalog.Seeded;
    private VersionCache? _cache;
    private string _version = FishCatalog.DefaultVersion;
    private FishingAssist? _assist;
    private bool _catalogChecked;
    private int _caught;
    private EmulatorDevice? Device { get; set; }

    public override void OnDevice(EmulatorDevice? device)
    {
        Device = device;
        var running = device != null && _bots.TryGetValue(device.Serial, out var entry) && entry.Bot.Running;
        _bot = running ? _bots[device!.Serial].Bot : null;
        SetRunningUi(running);
        _history = device != null ? DeviceStore.Records<FishLog>(device.Serial, "fishing") : [];
        _options = device == null ? new FishingOptions()
            : _bots.TryGetValue(device.Serial, out var known) ? known.Options : DeviceStore.Options<FishingOptions>(device.Serial, "fishing");
        _options.Normalize(AuthSession.Free);
        ShowOptions();
        ShowHistory();
        WatchAssist(device);
        if (device == null)
        {
            SetStatus("Chưa chọn tab giả lập", Theme.Muted);
            return;
        }
        if (!running) SetStatus("Sẵn sàng", Theme.Muted);
        _ = Install(device);
    }

    /// <summary>Đọc bản game đang cài (ADB, chậm) ở nền rồi mới đổi catalog, tạo bot + kết nối sẵn; lần đầu mở tool thì cập nhật catalog.</summary>
    private async Task Install(EmulatorDevice device)
    {
        try
        {
            var (catalog, version, cache) = await Task.Run(() => FishCatalog.Installed(device));
            if (Device != device) return;
            (_catalog, _version, _cache) = (catalog, version, cache);
            _catalogInfo.Text = $"Database: {_catalog.Count} ID cá";
            RefreshFakeZones();
            BotOf(device).WarmUp();
            if (_catalogChecked) return;
            _catalogChecked = true;
            UpdateCatalog();
        }
        catch (Exception e)
        {
            L.Swallowed("đọc bản game", e);
        }
    }

    /// <summary>Theo dõi tiện ích câu (POV / cắn nhanh) của tab đang chọn.</summary>
    private void WatchAssist(EmulatorDevice? device)
    {
        if (_assist != null) _assist.Changed -= OnAssistChanged;
        _assist = device != null ? FishingAssist.For(device.Serial) : null;
        if (_assist != null) _assist.Changed += OnAssistChanged;
        ShowAssist(_assist?.PovEnabled ?? false, _assist?.FastBiteEnabled ?? false);
    }

    private void OnAssistChanged(bool pov, bool fast) => App.Ui(() => ShowAssist(pov, fast));

    /// <summary>Bot câu của 1 tab (tạo lần đầu); thông báo của bot không còn hiện trên trang thì bỏ qua.</summary>
    private FishingBot BotOf(EmulatorDevice device)
    {
        if (_bots.TryGetValue(device.Serial, out var entry)) return entry.Bot;
        FishingBot? bot = null;
        void Forward(Action action) => App.Ui(() => { if (bot != null && bot == _bot) action(); });
        var events = new BotEvents
        {
            Status = (text, level) => Forward(() => SetStatus(text, StatusColors.Of(level))),
            Tool = rod => Forward(() => SetRod(rod)),
            Stopped = error => Forward(() => OnBotStopped(error)),
        };
        var fishing = new FishingEvents { Fish = id => Forward(() => SetFishInfo(id)), Catch = record => Forward(() => OnCatch(FishLog.Of(record))) };
        var catalog = _catalog == FishCatalog.Seeded ? FishCatalog.Installed(device).Catalog : _catalog;
        bot = new FishingBot(device, events, fishing, _options, catalog);
        _bots[device.Serial] = (bot, _options);
        return bot;
    }

    private void Start()
    {
        if (_bot is { Running: true }) return;
        if (Device == null)
        {
            SetStatus("Chưa chọn tab giả lập", Theme.Warn);
            return;
        }
        App.StopOthers(this);
        var bot = BotOf(Device);
        if (!bot.Start()) return;
        _bot = bot;
        SetRunningUi(true);
        SetStatus("Đang khởi động...", Theme.Accent);
    }

    public override void Halt() => Stop();

    private void Stop()
    {
        foreach (var (bot, _) in _bots.Values) if (bot.Running) bot.Stop();
        _bot = null;
        SetRunningUi(false);
        SetStatus("Đã dừng", Theme.Muted);
    }

    /// <summary>Luồng câu đã kết thúc: người dùng tắt hoặc tự dừng vì lỗi.</summary>
    private void OnBotStopped(string? error)
    {
        SetRunningUi(false);
        SetStatus(error ?? "Đã dừng", error != null ? Theme.Danger : Theme.Muted);
    }

    public override void OnClose()
    {
        foreach (var (bot, _) in _bots.Values) bot.Close();
    }

    /// <summary>Tải catalog ID cá mới nhất của bản game đang cài (chạy nền): mỗi lần mở tool 1 lần + khi bấm nút.</summary>
    private void UpdateCatalog()
    {
        _updateCatalog.SetEnabled(false, "Đang tải...");
        _catalogInfo.Set("Đang cập nhật...", Theme.Accent);
        var (catalog, version) = (_catalog, _version);
        Task.Run(() => catalog.UpdateAsync(version)).ContinueWith(task => App.Ui(() =>
        {
            _updateCatalog.SetEnabled(true, "Cập nhật ID cá");
            var (ok, message) = task.IsCompletedSuccessfully ? task.Result : (false, task.Exception?.InnerException?.Message ?? "lỗi");
            var text = ok ? $"✔ {message}" : $"✖ {message}";
            _catalogInfo.Set(text.Length > 90 ? text[..90] : text, ok ? Theme.Ok : Theme.Danger);
        }));
    }
}
