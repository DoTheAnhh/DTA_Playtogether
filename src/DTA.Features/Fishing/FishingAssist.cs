using DTA.Engine.Core;
using DTA.Game.Session;
using DTA.Runtime.Core;

namespace DTA.Features.Fishing;

/// <summary>
/// Tiện ích câu (1 / tab): Khoá POV (giữ camera FreeLook, không cho game đổi sang góc câu) và Cá cắn nhanh (bỏ các lần rỉa giả). Mặc định
/// tắt; cắn nhanh cần xác nhận rủi ro; bản miễn phí không có. Bot câu báo vòng đời từng lượt; câu tay thì luồng canh tự theo trạng thái.
/// </summary>
public sealed class FishingAssist
{
    private static readonly Logger L = Log.For("fishing");
    private static readonly Dictionary<string, FishingAssist> Assists = [];
    private readonly string _serial;
    private readonly object _lock = new();
    private CancellationTokenSource? _watcher;
    private bool _botActive;
    private PovLock? _pov;
    private FastBite? _bite;

    private FishingAssist(string serial)
    {
        _serial = serial;
        AuthSession.OnStop(_ => StopAll());
    }

    public static FishingAssist For(string serial)
    {
        lock (Assists)
        {
            if (!Assists.TryGetValue(serial, out var assist)) Assists[serial] = assist = new FishingAssist(serial);
            return assist;
        }
    }

    public bool PovEnabled { get; private set; }
    public bool FastBiteEnabled { get; private set; }
    /// <summary>Báo giao diện (POV, cắn nhanh).</summary>
    public event Action<bool, bool>? Changed;

    /// <summary>Bật / tắt khoá POV (bản miễn phí không có).</summary>
    public bool SetPov(bool enabled)
    {
        if (enabled && AuthSession.Free) return false;
        lock (_lock)
        {
            PovEnabled = enabled && Risk.IsAccepted(RiskFeature.FishingPov);
            if (!PovEnabled) _pov?.Release();
            SyncWatcher();
        }
        Changed?.Invoke(PovEnabled, FastBiteEnabled);
        return PovEnabled;
    }

    /// <summary>Bật / tắt cá cắn nhanh (cần xác nhận rủi ro; bản miễn phí không có).</summary>
    public bool SetFastBite(bool enabled)
    {
        if (enabled && (AuthSession.Free || !Risk.IsAccepted(RiskFeature.FastBite))) return false;
        lock (_lock)
        {
            FastBiteEnabled = enabled;
            if (!enabled) _bite?.Restore();
            SyncWatcher();
        }
        Changed?.Invoke(PovEnabled, FastBiteEnabled);
        return FastBiteEnabled;
    }

    /// <summary>Bot câu đang chạy thì luồng canh nhường bot (bot tự báo vòng đời).</summary>
    public void SetBotActive(bool active)
    {
        lock (_lock)
        {
            _botActive = active;
            SyncWatcher();
        }
    }

    /// <summary>Dừng hết (key hết hạn): tắt cả 2, trả nguyên camera + phao.</summary>
    public void StopAll()
    {
        lock (_lock)
        {
            PovEnabled = FastBiteEnabled = false;
            _pov?.Release();
            _bite?.Restore();
            SyncWatcher();
        }
        Changed?.Invoke(false, false);
    }

    private (PovLock, FastBite)? Parts(FishingGame game)
    {
        if (_pov?.Session != game.Session) (_pov, _bite) = (new PovLock(game.Session), new FastBite(game));
        return (_pov, _bite!);
    }

    /// <summary>Lượt quăng mới: chốt góc nhìn hiện tại.</summary>
    public void CastStarted(FishingGame game)
    {
        lock (_lock)
        {
            var (pov, bite) = Parts(game)!.Value;
            bite.NewCast();
            if (PovEnabled) pov.Capture();
        }
    }

    /// <summary>Đang chờ cá (gọi mỗi nhịp): giữ camera, bỏ rỉa giả.</summary>
    public void Step(FishingGame game, bool waiting)
    {
        lock (_lock)
        {
            var (pov, bite) = Parts(game)!.Value;
            if (PovEnabled) pov.Maintain();
            if (FastBiteEnabled && waiting) bite.Apply();
        }
    }

    /// <summary>Lượt câu kết thúc (kết quả / cá sổng / dừng): nhả camera, quên phao.</summary>
    public void CastEnded(FishingGame game, bool restoreFloat = false)
    {
        lock (_lock)
        {
            var (pov, bite) = Parts(game)!.Value;
            pov.Release();
            if (restoreFloat) bite.Restore();
            else bite.Forget();
        }
    }

    private void SyncWatcher()
    {
        var want = (PovEnabled || FastBiteEnabled) && !_botActive;
        if (want == (_watcher != null)) return;
        _watcher?.Cancel();
        _watcher = null;
        if (!want) return;
        var stop = _watcher = new CancellationTokenSource();
        new Thread(() => Watch(stop.Token)) { IsBackground = true, Name = "FishingAssist" }.Start();
    }

    /// <summary>Câu tay: theo trạng thái câu (40 ms khi đang câu, 150 ms khi rảnh) để chốt / giữ POV và bỏ rỉa giả.</summary>
    private void Watch(CancellationToken stop)
    {
        FishingGame? game = null;
        var last = -1;
        var casting = false;
        while (!stop.IsCancellationRequested)
        {
            var delay = 150;
            try
            {
                if (SessionHub.Peek(_serial) is not { } session)
                {
                    stop.WaitHandle.WaitOne(500);
                    continue;
                }
                if (game?.Session != session) game = new FishingGame(session);
                var state = game.Poll().State;
                var active = state is 1 or 2 or FishStates.Waiting or FishStates.Shadow or FishStates.Bite;
                if (active && !casting) CastStarted(game);
                if (state != last && state is FishStates.Result or FishStates.Idle && casting) CastEnded(game);
                casting = active || (casting && state is not (FishStates.Result or FishStates.Idle));
                last = state;
                if (active)
                {
                    Step(game, true);
                    delay = 40;
                }
            }
            catch (Exception e)
            {
                L.Debug($"Canh câu tay lỗi: {e.Message}");
                delay = 200;
            }
            stop.WaitHandle.WaitOne(delay);
        }
    }
}
