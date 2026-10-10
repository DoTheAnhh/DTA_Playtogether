using DTA.Engine.Core;
using DTA.Game.Data;
using DTA.Game.Session;
using DTA.Runtime.Core;
using DTA.Runtime.Device;

namespace DTA.Engine.Bots;

/// <summary>
/// Khung chung của 1 chức năng tự chạy trên 1 tab (câu cá, đào cổ vật...): chạy trên luồng riêng, dùng chung phiên game của tab
/// (<see cref="SessionHub"/>) + giữ sẵn kênh bấm giữa các lần Bật / Tắt, tự kết nối lại khi lỗi tạm thời. Lớp con chỉ viết
/// <see cref="Work"/> (và tuỳ chọn Prepare / Preload / Durability).
/// </summary>
public abstract partial class Bot(EmulatorDevice device, BotEvents events)
{
    private const int ReconnectAttempts = 5;
    private static readonly TimeSpan FailureWindow = TimeSpan.FromSeconds(60);
    protected Logger L => new(Channel);

    /// <summary>Kênh log của chức năng (trang Log lọc theo kênh).</summary>
    protected virtual string Channel => "bot";

    private readonly object _connectLock = new();
    private Touch? _touch;
    private Thread? _thread;
    private int _generation;
    private CancellationTokenSource _cancel = new();

    /// <summary>Huỷ khi bị tắt (cho các lần chờ dài: tải bản đồ...).</summary>
    protected CancellationToken Cancel => _cancel.Token;

    public EmulatorDevice Device { get; } = device;
    protected BotEvents Events { get; } = events;
    public bool Running { get; private set; }

    /// <summary>Tên dụng cụ chức năng dùng (ghép vào thông báo).</summary>
    protected virtual string ToolName => "món đồ đang cầm";

    /// <summary>Class dụng cụ khi đang cầm (rỗng = làm tay không).</summary>
    protected virtual string ToolClass => "";

    /// <summary>Phiên game + kênh bấm của lần chạy hiện tại.</summary>
    protected GameSession Session { get; private set; } = null!;
    protected Touch Touch { get; private set; } = null!;

    /// <summary>Bắt đầu phiên mới ngay (không chặn giao diện); false nếu key hết hạn.</summary>
    public bool Start()
    {
        if (!AuthSession.Active)
        {
            Events.Status("Key đã hết hạn hoặc phiên không còn hiệu lực", Level.Warn);
            return false;
        }
        var generation = Interlocked.Increment(ref _generation);
        _cancel = new CancellationTokenSource();
        Running = true;
        BotRegistry.Add(this);
        var previous = _thread;
        _thread = new Thread(() =>
        {
            previous?.Join(1000);
            if (Running && _generation == generation) Run(generation);
        }) { IsBackground = true, Name = GetType().Name };
        _thread.Start();
        return true;
    }

    public void Stop()
    {
        Interlocked.Increment(ref _generation);
        Running = false;
        _cancel.Cancel();
        BotRegistry.Remove(this);
    }

    /// <summary>Kết nối sẵn ở nền lúc chọn tab (lỗi thì thôi, lúc Bật sẽ báo).</summary>
    public void WarmUp() => ThreadPool.QueueUserWorkItem(_ =>
    {
        try
        {
            Connect(false);
        }
        catch (Exception e)
        {
            L.Debug($"Chưa kết nối sẵn được: {e.Message}");
        }
    });

    /// <summary>Dừng + đóng kênh bấm (đóng tool).</summary>
    public void Close()
    {
        Stop();
        lock (_connectLock)
        {
            _touch?.Dispose();
            _touch = null;
        }
    }

    /// <summary>Vòng lặp làm việc tới khi bị tắt; trả chuỗi lỗi nếu phải tự dừng.</summary>
    protected abstract string? Work();

    /// <summary>Vừa kết nối xong: đọc trước thứ vòng lặp cần (lần đầu dò cấu trúc nên chậm).</summary>
    protected virtual void Prepare() { }

    /// <summary>Nạp sẵn dữ liệu nặng lúc kết nối ở nền để Bật là chạy ngay.</summary>
    protected virtual void Preload() { }

    /// <summary>(lượt còn, tối đa) của dụng cụ đang cầm; null nếu không đọc được.</summary>
    protected virtual (int, int)? Durability() => ToolClass.Length > 0 ? Session.Held.Read(ToolClass).Uses : null;

    /// <summary>Kết nối rồi làm việc; lỗi tạm thời (ADB ngắt, game chuyển cảnh) thì thử lại tối đa 5 lần liên tiếp.</summary>
    private void Run(int generation)
    {
        string? error = null;
        var failures = 0;
        while (Running && _generation == generation && AuthSession.Active)
        {
            var started = DateTime.UtcNow;
            try
            {
                Events.Status("Đang kết nối bộ nhớ game...", Level.Info);
                Connect(true);
                if (!Running || _generation != generation) break;
                error = Work();
                break;
            }
            catch (Exception e) when (e is DeviceError { Retry: true } or GameError { Retry: true })
            {
                error = e.Message;
                failures = DateTime.UtcNow - started > FailureWindow ? 1 : failures + 1;
                if (failures >= ReconnectAttempts) break;
                Events.Status($"{error} - thử lại ({failures}/{ReconnectAttempts})", Level.Warn);
                SessionHub.Close(Device.Serial);
                Sleep(3);
            }
            catch (Exception e)
            {
                L.Error($"{GetType().Name} lỗi: {e}");
                error = $"Lỗi: {e.Message}";
                break;
            }
        }
        if (_generation != generation) return;
        error = Running ? error : null;
        Running = false;
        BotRegistry.Remove(this);
        Events.Stopped(error);
    }

    /// <summary>Phiên game (dùng chung tab) + kênh bấm (mở song song); <paramref name="report"/> = báo tiến trình dò cấu trúc.</summary>
    protected void Connect(bool report = false)
    {
        lock (_connectLock)
        {
            var opener = _touch == null ? Task.Run(() => new Touch(Device)) : Task.FromResult(_touch);
            var session = SessionHub.Get(Device, text => { if (report && Running) Events.Status(text, Level.Info); });
            _touch = opener.Result;
            session.Screen = (_touch.Width, _touch.Height);
            (Session, Touch) = (session, _touch);
            session.Ui.HudButton("reel");
            session.Ui.HudButton("cast");
            session.Open.Top();
            Prepare();
            Preload();
            session.SaveCache();
        }
    }

    /// <summary>Ngủ theo nhịp ngắn để lệnh tắt có tác dụng ngay.</summary>
    protected void Sleep(double seconds)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (Running && DateTime.UtcNow < deadline) Thread.Sleep(Math.Min(100, Math.Max(1, (int)(deadline - DateTime.UtcNow).TotalMilliseconds)));
    }

    /// <summary>Kiểm <paramref name="condition"/> mỗi 50 ms tới khi đúng; false nếu hết giờ / bị tắt.</summary>
    protected bool WaitUntil(Func<bool> condition, double timeout)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeout);
        while (Running && DateTime.UtcNow < deadline)
        {
            if (condition()) return true;
            Thread.Sleep(50);
        }
        return false;
    }

    /// <summary>Thông báo khi chưa cầm dụng cụ (không tự mở túi lấy).</summary>
    protected string NotEquipped() => $"Bạn chưa trang bị {ToolName}. Hãy trang bị và bật lại chức năng";

    /// <summary>Cầm đúng dụng cụ chưa (null = không đọc được).</summary>
    protected bool? Holding() => ToolClass.Length > 0 ? Session.Held.Read(ToolClass).Holding : true;
}
