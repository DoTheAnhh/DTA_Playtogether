using DTA.Ui;
using DTA.Engine.Bots;
using DTA.Engine.Core;
using DTA.Engine.Gather;
using DTA.Runtime.Device;

namespace DTA.App.Views.Gather;

/// <summary>Tab giả lập + bot: mỗi tab đúng 1 bot (giữ chung object tuỳ chọn với trang), thông báo của bot chuyển về luồng giao diện.</summary>
public abstract partial class GatherView<TOptions>
{
    private readonly Dictionary<string, (Bot Bot, TOptions Options)> _bots = [];
    /// <summary>Bot của lượt đang hiện trên trang (null khi không chạy).</summary>
    private Bot? _bot;
    protected EmulatorDevice? Device { get; private set; }

    /// <summary>Tạo bot cho 1 tab giả lập.</summary>
    protected abstract Bot CreateBot(EmulatorDevice device, BotEvents events, GatherEvents gather, TOptions options);

    public override void OnDevice(EmulatorDevice? device)
    {
        Device = device;
        var running = device != null && _bots.TryGetValue(device.Serial, out var entry) && entry.Bot.Running;
        _bot = running ? _bots[device!.Serial].Bot : null;
        SetRunningUi(running);
        _history = device != null ? DeviceStore.History(device.Serial, Key) : [];
        Options = device == null ? new TOptions() : _bots.TryGetValue(device.Serial, out var known) ? known.Options : DeviceStore.Options<TOptions>(device.Serial, Key);
        ShowOptions();
        ShowHistory();
        if (device == null) return;
        if (!running) SetStatus("Sẵn sàng", Theme.Muted);
        BotOf(device).WarmUp();
    }

    /// <summary>Bot của 1 tab (tạo lần đầu); thông báo của bot không còn hiện trên trang thì bỏ qua.</summary>
    protected Bot BotOf(EmulatorDevice device)
    {
        if (_bots.TryGetValue(device.Serial, out var entry)) return entry.Bot;
        Bot? bot = null;
        void Forward(Action action) => App.Ui(() => { if (bot != null && bot == _bot) action(); });
        var events = new BotEvents
        {
            Status = (text, level) => Forward(() => SetStatus(text, StatusColors.Of(level))),
            Tool = tool => Forward(() => SetTool(tool)),
            Stopped = error => Forward(() => OnBotStopped(error)),
        };
        var gather = new GatherEvents
        {
            Targets = (inRange, total) => Forward(() => SetTargets(inRange, total)),
            Target = (distance, name) => Forward(() => SetTarget(distance, name)),
            Find = record => Forward(() => OnFind(HistoryEntry.Of(record))),
        };
        bot = CreateBot(device, events, gather, Options);
        _bots[device.Serial] = (bot, Options);
        return bot;
    }

    protected void Start()
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
        SetTarget(null, "");
        SetStatus("Đã dừng", Theme.Muted);
    }

    /// <summary>Luồng làm việc đã kết thúc: người dùng tắt hoặc tự dừng vì lỗi.</summary>
    private void OnBotStopped(string? error)
    {
        SetRunningUi(false);
        SetTarget(null, "");
        SetStatus(error ?? "Đã dừng", error != null ? Theme.Danger : Theme.Muted);
    }

    public override void OnClose()
    {
        foreach (var (bot, _) in _bots.Values) bot.Close();
    }
}
