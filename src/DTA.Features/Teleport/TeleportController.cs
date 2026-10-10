using DTA.Engine.Core;
using DTA.Game.Actor;
using DTA.Game.Geometry;
using DTA.Game.Session;
using DTA.Runtime.Core;
using DTA.Runtime.Device;

namespace DTA.Features.Teleport;

/// <summary>Nhân vật đang ở đâu: bản đồ (null = chưa đọc được), vị trí, hướng.</summary>
public readonly record struct PlayerPlace((int Id, string Name)? Map, Vec3? Position, Quat? Rotation);

/// <summary>
/// Menu dịch chuyển trên 1 tab: theo dõi vị trí nhân vật mỗi 0,3 s (bản đồ mỗi 5 lần đọc), dịch chuyển tới vị trí đã lưu ở luồng nền (mỗi
/// lúc 1 lần, tạm dừng theo dõi trong lúc dịch chuyển), xoay camera ra sau lưng khi tới.
/// </summary>
public sealed class TeleportController(EmulatorDevice device)
{
    private const int MapEvery = 5;
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(300), RetryDelay = TimeSpan.FromSeconds(3);
    private static readonly Logger L = Log.For("teleport");
    private CancellationTokenSource? _tracking;
    private volatile bool _busy;

    public event Action<PlayerPlace>? Moved;
    public event Action<string, bool>? Status;
    /// <summary>Kết quả 1 lần dịch chuyển (thành công, thông báo).</summary>
    public event Action<bool, string>? Done;

    public bool Busy => _busy;

    public void StartTracking()
    {
        _tracking?.Cancel();
        var stop = _tracking = new CancellationTokenSource();
        new Thread(() => Track(stop.Token)) { IsBackground = true, Name = "PositionTracker" }.Start();
    }

    public void StopTracking() => _tracking?.Cancel();

    /// <summary>Đọc vị trí đều đặn tới khi tắt; lỗi thì báo rồi chờ kết nối lại.</summary>
    private void Track(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                Status?.Invoke("Đang kết nối bộ nhớ game...", false);
                var session = SessionHub.Get(device, text => Status?.Invoke(text, false));
                Status?.Invoke("Đang theo dõi vị trí nhân vật", false);
                (int, string)? map = null;
                for (var reads = 0; !stop.IsCancellationRequested; reads++)
                {
                    if (!_busy)
                    {
                        if (reads % MapEvery == 0) map = session.Camera.Map();
                        var pose = session.Player.Pose();
                        var position = pose?.Position ?? session.Player.Position();
                        if (position == null && !session.Alive()) throw new GameError("Game đã tắt", true);
                        Moved?.Invoke(new PlayerPlace(map, position, pose?.Rotation));
                    }
                    stop.WaitHandle.WaitOne(Interval);
                }
            }
            catch (Exception e) when (e is GameError or DeviceError)
            {
                Moved?.Invoke(default);
                Status?.Invoke(e.Message, true);
                stop.WaitHandle.WaitOne(RetryDelay);
            }
        }
    }

    /// <summary>Dịch chuyển tới vị trí đã lưu ở luồng nền (cần xác nhận rủi ro; đang dịch chuyển dở thì báo bận).</summary>
    public bool Go(TelePlace place)
    {
        if (!Risk.IsAccepted(RiskFeature.Teleport))
        {
            Done?.Invoke(false, "Chức năng chưa được xác nhận rủi ro!");
            return false;
        }
        if (_busy)
        {
            Done?.Invoke(false, "Đang có một tiến trình dịch chuyển đang thực hiện, vui lòng chờ hoàn tất!");
            return false;
        }
        _busy = true;
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                var session = SessionHub.Get(device);
                var rotation = place.Rotation is [var x, var y, var z, var w] ? new Quat(x, y, z, w) : (Quat?)null;
                var result = session.Teleport.Go(new TeleportRequest(new Vec3(place.X, place.Y, place.Z), place.MapId != 0 ? place.MapId : null, rotation, place.Name, AlignCamera: true),
                    text => Status?.Invoke(text, false));
                Done?.Invoke(result.Ok, result.Ok ? $"Đã tới: {place.Name}{(rotation != null ? " (đã xoay đúng hướng)" : "")}" : $"{result.Status}: Không tới được đích (vị trí đo được: {result.Position})");
            }
            catch (Exception e)
            {
                L.Error($"Dịch chuyển lỗi: {e}");
                Done?.Invoke(false, $"Lỗi dịch chuyển: {e.Message}");
            }
            finally
            {
                _busy = false;
            }
        });
        return true;
    }
}
