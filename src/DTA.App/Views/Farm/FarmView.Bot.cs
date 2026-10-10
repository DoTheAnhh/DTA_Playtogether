using System.Globalization;
using System.Windows.Media;
using System.Windows.Threading;
using DTA.Ui;
using DTA.Engine.Bots;
using DTA.Engine.Core;
using DTA.Features.Farm;
using DTA.Runtime.Device;

namespace DTA.App.Views.Farm;

/// <summary>Tab giả lập, tuỳ chọn, bot nông trại (đọc số liệu ngầm mỗi 8 s khi rảnh), Bật / Tắt theo việc của từng tab.</summary>
public sealed partial class FarmView
{
    private FarmOptions _options = new();
    private FarmInfo? _info;
    private List<FarmLog> _history = [];
    private readonly Dictionary<string, FarmBot> _bots = [];
    /// <summary>Tuỳ chọn theo tab (bot giữ chung object nên đổi tuỳ chọn có tác dụng ngay).</summary>
    private readonly Dictionary<string, FarmOptions> _optionSets = [];
    private FarmBot? _bot;
    private EmulatorDevice? _device;
    private DispatcherTimer? _refresh;
    private StatusTicker? _ticker;

    public override void OnDevice(EmulatorDevice? device)
    {
        _device = device;
        _bot = null;
        _info = null;
        _history = device != null ? DeviceStore.Records<FarmLog>(device.Serial, "farm") : [];
        _options = device == null ? new FarmOptions() : _optionSets.TryGetValue(device.Serial, out var known) ? known : _optionSets[device.Serial] = Loaded(device.Serial);
        _reapMin.Set(_options.HarvestMin);
        _mutation.Set(_options.MutationMode);
        _keepRare.Set(_options.KeepRare);
        _sellMutButton.SetEnabled(_options.KeepRare);
        _harvestMutation.Set(_options.KeepMutationHarvest);
        _harvestMutButton.SetEnabled(_options.KeepMutationHarvest);
        _currency.Set(_options.BuyWithDiamond);
        _buyLimit.Set(_options.BuyLimit.ToString());
        foreach (var (chips, set) in new[] { (_saleGrades2, _options.SellGrades), (_harvestGrades, _options.HarvestGrades), (_plantGrades, _options.PlantGrades), (_shopGrades, _options.ShopGrades) })
            foreach (var (grade, chip) in chips) chip.Set(set.Contains(grade));
        _minKg.Set(_options.MinKg > 0 ? _options.MinKg.ToString("0.##", CultureInfo.InvariantCulture) : "");
        _maxKg.Set(_options.MaxKg > 0 ? _options.MaxKg.ToString("0.##", CultureInfo.InvariantCulture) : "");
        _spacing.Set(_options.PlantSpacing.ToString("0.##", CultureInfo.InvariantCulture));
        _plantSearch.Set(_options.PlantSearch);
        _shopSearch.Set(_options.ShopSearch);
        _harvestSearch.Set(_options.HarvestSearch);
        _saleSearch.Set(_options.SellSearch);
        _allPlots.Set(_options.PlantPlots.Count == 0);
        foreach (var (plot, chip) in _plots) chip.Set(_options.PlantPlots.Contains(plot));
        ShowInfo();
        ShowHistory();
        foreach (var button in _starts) button.SetEnabled(device != null);
        foreach (var button in _refreshes) button.SetEnabled(device != null);
        foreach (var button in _stops) button.SetEnabled(false);
        if (device == null)
        {
            SetStatus("Chưa chọn tab giả lập", Theme.Muted);
            return;
        }
        SetStatus("Sẵn sàng - đang tự động đọc số liệu...", Theme.Muted);
        BotOf(device).WarmUp();
        StartAutoRefresh();
    }

    private static FarmOptions Loaded(string serial)
    {
        var options = DeviceStore.Options<FarmOptions>(serial, "farm");
        options.Normalize();
        return options;
    }

    /// <summary>Đọc số liệu ngầm mỗi 8 s khi bot rảnh.</summary>
    private void StartAutoRefresh()
    {
        if (_refresh != null) return;
        _refresh = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
        _refresh.Tick += (_, _) => { if (_device != null && _bot is not { Running: true }) Scan(); };
        _refresh.Start();
        Scan();
    }

    /// <summary>Đổi tuỳ chọn: lưu ngay, vẽ lại bảng theo tuỳ chọn mới.</summary>
    private void Change(Action<FarmOptions> change)
    {
        change(_options);
        if (_device != null) DeviceStore.SaveOptions(_device.Serial, "farm", _options);
        ShowSale();
        if (_info != null) ShowInfo();
    }

    private static void Toggle(HashSet<int> set, int value, bool on)
    {
        if (on) set.Add(value);
        else set.Remove(value);
    }

    /// <summary>Bảng chọn được: bấm dòng = bật / tắt món đó; bấm tiêu đề "[ Chọn ]" = bật / tắt hết các dòng đang hiện.</summary>
    private void Selectable(CTable table, Func<FarmOptions, HashSet<int>> set)
    {
        table.RowClicked += (row, _) => Change(o =>
        {
            var id = int.Parse(row.Key);
            if (!set(o).Remove(id)) set(o).Add(id);
        });
        table.HeadingClicked += column =>
        {
            if (column != "sel" || table.Rows.Count == 0) return;
            var ids = table.Rows.Select(r => int.Parse(r.Key)).ToList();
            var on = !ids.All(set(_options).Contains);
            Change(o => ids.ForEach(id => Toggle(set(o), id, on)));
        };
    }

    private void OnAllPlots(bool on)
    {
        if (!on) return;
        Change(o => o.PlantPlots.Clear());
        foreach (var chip in _plots.Values) chip.Set(false);
    }

    private void OnPlot(int plot, bool on)
    {
        Change(o => Toggle(o.PlantPlots, plot, on));
        _allPlots.Set(_options.PlantPlots.Count == 0);
    }

    private void OnWeight()
    {
        static float Number(CEntry entry) => float.TryParse(entry.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? Math.Max(v, 0) : 0;
        Change(o => (o.MinKg, o.MaxKg) = (Number(_minKg), Number(_maxKg)));
    }

    /// <summary>Hộp chọn các biến thể muốn giữ (để trống = giữ tất cả).</summary>
    private void EditMutations(string title, Func<FarmOptions, HashSet<int>> set)
    {
        var table = _info?.Mutations is { Count: > 0 } known ? known : MutationDialog.Defaults;
        if (MutationDialog.Ask(App, title, table, set(_options)) is not { } chosen) return;
        Change(o =>
        {
            set(o).Clear();
            set(o).UnionWith(chosen);
        });
    }

    // ----- Trạng thái + bật / tắt -----
    private void SetStatus(string text, Color color) => (_ticker ??= new StatusTicker(_states, _dots, 90)).Set(text, color);

    public override void ShowMessage(string text, Color color) => SetStatus(text, color);

    private void SetRunningUi(bool running)
    {
        foreach (var button in _starts) button.SetEnabled(!running);
        foreach (var button in _stops) button.SetEnabled(running);
        App.SetRunning(running, "Đang làm nông trại");
    }

    private FarmBot BotOf(EmulatorDevice device)
    {
        if (_bots.TryGetValue(device.Serial, out var known)) return known;
        FarmBot? bot = null;
        void Forward(Action action) => App.Ui(() => { if (bot != null && bot == _bot) action(); });
        var events = new BotEvents
        {
            Status = (text, level) => Forward(() => SetStatus(text, StatusColors.Of(level))),
            Stopped = error => Forward(() => OnBotStopped(error)),
        };
        var farm = new FarmEvents
        {
            Info = info => Forward(() =>
            {
                _info = info;
                ShowInfo();
            }),
            Record = record => Forward(() =>
            {
                _history.Add(FarmLog.Of(record));
                if (_device != null) DeviceStore.SaveHistory(_device.Serial, "farm", _history);
                ShowHistory();
            }),
        };
        return _bots[device.Serial] = bot = new FarmBot(device, events, farm, _options);
    }

    private bool Launch(FarmMode mode)
    {
        if (_device == null)
        {
            SetStatus("Chưa chọn tab giả lập", Theme.Warn);
            return false;
        }
        var bot = BotOf(_device);
        bot.Mode = mode;
        if (!bot.Start()) return false;
        _bot = bot;
        return true;
    }

    /// <summary>Đọc số liệu 1 lượt (đang làm việc khác thì thôi, không chen ngang).</summary>
    private void Scan()
    {
        if (_bot is { Running: true }) return;
        Launch(FarmMode.Scan);
    }

    private void Start(FarmMode mode)
    {
        if (_bot is { Running: true }) return;
        App.StopOthers(this);
        if (!Launch(mode)) return;
        SetRunningUi(true);
        SetStatus("Đang khởi động...", Theme.Accent);
    }

    public override void Halt() => Stop();

    private void Stop()
    {
        foreach (var bot in _bots.Values) if (bot.Running) bot.Stop();
        _bot = null;
        SetRunningUi(false);
        SetStatus("Đã dừng", Theme.Muted);
    }

    private void OnBotStopped(string? error)
    {
        if (_bot is { Mode: not FarmMode.Scan }) SetRunningUi(false);
        if (error != null) SetStatus(error, Theme.Danger);
    }

    public override void OnClose()
    {
        _refresh?.Stop();
        foreach (var bot in _bots.Values) bot.Close();
    }
}
