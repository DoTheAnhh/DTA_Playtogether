using DTA.Engine.Bots;
using DTA.Engine.Core;
using DTA.Game.Session;
using DTA.Runtime.Core;
using DTA.Runtime.Device;

namespace DTA.Engine.Popups;

/// <summary>
/// Luồng nền xử lý popup CHUNG của game, chạy suốt khi đã chọn tab (không cần bật menu nào): màn nhận thưởng đóng ngay khi hiện
/// (kể cả lúc bot chạy), popup chung khác (OK, xác nhận, mở hộp...) chỉ khi không bot nào chạy trên tab (bot tự xử lý, tránh 2 nơi
/// bấm 1 bảng). Bán / giữ, sửa đồ, quăng cần... là việc của từng menu. Đổi bản đồ thì báo phiên + làm nóng dữ liệu dùng chung.
/// </summary>
public sealed class PopupWatcher
{
    private static readonly TimeSpan Fast = TimeSpan.FromMilliseconds(20), Tick = TimeSpan.FromMilliseconds(50), Retap = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan MapCheck = TimeSpan.FromSeconds(1), Reconnect = TimeSpan.FromSeconds(3);
    private static readonly Logger L = Log.For("popup");
    public static PopupWatcher Instance { get; } = new();

    private readonly object _lock = new();
    private CancellationTokenSource? _stop;
    private string _serial = "";

    /// <summary>Gắn với tab đang chọn (null = dừng).</summary>
    public void SetDevice(EmulatorDevice? device)
    {
        lock (_lock)
        {
            if (device != null && device.Serial == _serial) return;
            _stop?.Cancel();
            _serial = device?.Serial ?? "";
            if (device == null) return;
            var stop = _stop = new CancellationTokenSource();
            new Thread(() => Loop(device, stop.Token)) { IsBackground = true, Name = "PopupWatcher" }.Start();
        }
    }

    public void Stop() => SetDevice(null);

    /// <summary>Vòng lặp: mỗi 20 ms canh màn thưởng, mỗi 50 ms (không bot) soát bảng trên cùng, mỗi giây kiểm đổi bản đồ.</summary>
    private static void Loop(EmulatorDevice device, CancellationToken stop)
    {
        GameSession? session = null;
        RewardSuppressor? rewards = null;
        int? lastMap = null;
        DateTime mapChecked = DateTime.MinValue, fullChecked = DateTime.MinValue, lastTap = DateTime.MinValue;
        long tapped = 0;
        while (!stop.IsCancellationRequested)
        {
            if (!AuthSession.Active)
            {
                stop.WaitHandle.WaitOne(400);
                continue;
            }
            try
            {
                if (session == null || !session.Alive())
                {
                    session = SessionHub.Get(device);
                    rewards = new RewardSuppressor(session);
                    lastMap = null;
                    Warmer.Run(session);
                }
                var now = DateTime.UtcNow;
                if (now - mapChecked >= MapCheck)
                {
                    mapChecked = now;
                    var map = session.Camera.Map();
                    if (map is { } m && lastMap is { } previous && m.Id != previous)
                    {
                        L.Info($"Đổi bản đồ -> {m.Name}");
                        session.NotifySceneChanged();
                        session.LocateActor();
                        rewards = new RewardSuppressor(session);
                        Warmer.Run(session);
                    }
                    lastMap = map?.Id ?? lastMap;
                }
                rewards!.Run();
                if (now - fullChecked >= Tick && !BotRegistry.Running(device.Serial))
                {
                    fullChecked = now;
                    var top = session.Open.Top();
                    if (top.Addr != 0 && (top.Addr != tapped || now - lastTap >= Retap) && session.Dialogs.HandleCommon(top.Addr, top.Names))
                        (tapped, lastTap) = (top.Addr, DateTime.UtcNow);
                }
            }
            catch (Exception e)
            {
                L.Debug($"Canh popup lỗi, kết nối lại: {e.Message}");
                session = null;
                stop.WaitHandle.WaitOne(e is GameError or DeviceError ? Reconnect : TimeSpan.FromSeconds(1));
                continue;
            }
            stop.WaitHandle.WaitOne(Fast);
        }
    }
}
