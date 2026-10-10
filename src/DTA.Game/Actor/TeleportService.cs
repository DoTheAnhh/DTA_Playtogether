using DTA.Game.Geometry;
using DTA.Game.Invoke;
using DTA.Game.Session;
using DTA.Runtime.Core;

namespace DTA.Game.Actor;

/// <summary>Kết quả dịch chuyển.</summary>
public enum TeleportStatus
{
    Success, RiskNotAccepted, InvalidCoordinates, NoValidGround, FallDetected, ConnectFailed, MapLoadTimeout, PlayerNotReady, TeleportFailed, Cancelled,
}

/// <summary>Yêu cầu dịch chuyển: bản đồ đích (null = bản đồ hiện tại), toạ độ, hướng, tên điểm, xoay camera sau lưng khi tới.</summary>
public sealed record TeleportRequest(Vec3 Target, int? MapId = null, Quat? Rotation = null, string Name = "", bool AlignCamera = false, float Tolerance = 1.5f);

public sealed record TeleportResult(TeleportStatus Status, Vec3? Position = null, int Corrections = 0)
{
    public bool Ok => Status == TeleportStatus.Success;
}

/// <summary>
/// Pipeline dịch chuyển chuẩn (cùng bản đồ + khác bản đồ): khác bản đồ thì gọi LayerSystem.ConnectToZoneMove (không NPC / cổng / điện
/// thoại) rồi chờ bản đồ mới sẵn sàng; tính mặt đất đích theo NavMesh; warp; căn camera; kiểm rơi 0,2 s sau (tin bộ dò đất của game)
/// và kéo lên sàn tối đa 3 lần. Mỗi lúc 1 lần dịch chuyển / tab.
/// </summary>
public sealed class TeleportService(GameSession session)
{
    public const int WhereFromQuickMove = 10;
    private const int WarpRetries = 3, FallCorrections = 3;
    private const float SettleBelow = 0.6f, FallLift = 0.15f;
    private static readonly TimeSpan MapTimeout = TimeSpan.FromSeconds(90), Poll = TimeSpan.FromMilliseconds(200);
    private const int SettleMs = 40, FallWindowMs = 200, StepMs = 30;
    private static readonly Logger L = Log.For("teleport");
    private readonly SemaphoreSlim _busy = new(1, 1);

    /// <summary>Cổng xác nhận rủi ro (tầng Engine gán qua Risk.Install); mặc định chặn mọi dịch chuyển.</summary>
    public static Func<bool> Allowed { get; set; } = () => false;

    /// <summary>Chạy pipeline; <paramref name="status"/> = báo tiến trình cho giao diện, <paramref name="cancel"/> = huỷ khi chờ tải bản đồ.</summary>
    public TeleportResult Go(TeleportRequest request, Action<string>? status = null, CancellationToken cancel = default)
    {
        var t = request.Target;
        if (!Allowed())
        {
            L.Error("Dịch chuyển chưa được xác nhận rủi ro");
            status?.Invoke("Chức năng chưa được xác nhận rủi ro!");
            return new(TeleportStatus.RiskNotAccepted);
        }
        if (!t.IsFinite) return new(TeleportStatus.InvalidCoordinates);
        if (!_busy.Wait(0)) return new(TeleportStatus.Cancelled);
        try
        {
            var here = session.Camera.Map();
            var mapId = request.MapId ?? here?.Id ?? 0;
            L.Info($"Dịch chuyển {(request.Name.Length > 0 ? $"'{request.Name}' " : "")}tới {t} bản đồ {mapId} (đang ở {here?.Name ?? "?"})");
            if (here is { } h && h.Id != mapId)
            {
                status?.Invoke($"Đang chuyển bản đồ sang {mapId}...");
                if (!ChangeMap(mapId)) return new(TeleportStatus.ConnectFailed);
                var ready = WaitMapReady(mapId, status, cancel);
                if (ready != TeleportStatus.Success) return new(ready);
            }
            else status?.Invoke($"Đang dịch chuyển tới {(request.Name.Length > 0 ? request.Name : "đích")}...");
            return Arrive(request, status);
        }
        finally
        {
            _busy.Release();
        }
    }

    /// <summary>Chuyển sang bản đồ <paramref name="mapId"/> (ConnectToZoneMove) rồi chờ bản đồ mới điều khiển được.</summary>
    public TeleportStatus Travel(int mapId, Action<string>? status = null, CancellationToken cancel = default)
    {
        if (session.Camera.Map()?.Id == mapId) return TeleportStatus.Success;
        if (!ChangeMap(mapId)) return TeleportStatus.ConnectFailed;
        return WaitMapReady(mapId, status, cancel);
    }

    /// <summary>
    /// Gọi ConnectToZoneMove(sysLayer, map, QuickMove, null, useIris 1, useAdabt 0) rồi trả mã gốc vùng trampoline. useAdabt = xoay màn thích
    /// ứng (AdabtiveOrientationRootManager): bật thì sang map mới game xoay theo thiết bị - trên giả lập thành màn DỌC; luôn tắt để giữ ngang.
    /// </summary>
    public bool ChangeMap(int mapId)
    {
        if (session.Camera.Map()?.Id == mapId) return true;
        var layer = session.System("sysLayer");
        if (!Bin.IsPtr(layer))
        {
            L.Warn("Không thấy LayerSystem");
            return false;
        }
        var ok = session.Invoker.Call(Fn.ConnectToZoneMove, layer, [(ulong)mapId, WhereFromQuickMove, 0, 1, 0], restoreTrampoline: true);
        L.Info(ok ? $"Đã gọi ConnectToZoneMove -> {mapId}" : "Game chưa chạy ConnectToZoneMove (hết giờ)");
        return ok;
    }

    /// <summary>Tính mặt đất -> warp -> camera -> kiểm rơi + kéo lên sàn.</summary>
    private TeleportResult Arrive(TeleportRequest request, Action<string>? status)
    {
        var t = request.Target;
        var vehicle = session.Vehicles.Current();
        var boat = vehicle?.IsBoat == true;
        var ground = session.Ground.Resolve(t.X, t.Z, t.Y, boat).Y;
        if (ground == null) L.Info($"Chưa thấy mặt đất NavMesh, dùng Y đích {t.Y:F2}");
        var warpY = boat ? ground ?? t.Y : ground is { } g ? Math.Max(t.Y, g) : t.Y;
        var target = t with { Y = warpY };
        if (!WarpWithRetry(target, request)) return new(TeleportStatus.TeleportFailed, session.Player.Position());
        if (request.AlignCamera) session.Camera.AlignBehind(request.Rotation);
        var (underground, p1, p2) = CheckFall(ground ?? t.Y, warpY, boat, ground != null);
        var corrections = 0;
        var actual = p2;
        if (underground)
        {
            L.Warn($"Phát hiện độn thổ: Y {p1:F2} -> {p2:F2}, mặt đất {ground ?? t.Y:F2}");
            for (corrections = 1; corrections <= FallCorrections && underground; corrections++)
            {
                var lifted = t with { Y = (ground ?? t.Y) + FallLift * corrections };
                session.Warp.To(lifted, request.Rotation, 8, StepMs, request.Tolerance);
                Thread.Sleep(60);
                actual = session.Player.Position()?.Y ?? actual;
                underground = !(actual >= (ground ?? t.Y) - 0.08f || session.Player.Grounded() == true);
            }
            corrections--;
            if (underground)
            {
                L.Error($"Kéo lên sàn thất bại sau {FallCorrections} lần");
                return new(TeleportStatus.FallDetected, session.Player.Position(), corrections);
            }
            if (request.AlignCamera) session.Camera.AlignBehind(request.Rotation);
        }
        var final = session.Player.Position();
        L.Info($"Tới nơi {final} (đích {target}, kéo sàn {corrections} lần)");
        status?.Invoke("Đã dịch chuyển");
        return new(TeleportStatus.Success, final, corrections);
    }

    /// <summary>Warp tối đa 3 lần (xác định lại motor mỗi lần).</summary>
    private bool WarpWithRetry(Vec3 target, TeleportRequest request)
    {
        for (var attempt = 0; attempt < WarpRetries; attempt++)
        {
            session.LocateActor();
            if (session.Player.MotorBody() == 0)
            {
                Thread.Sleep(20);
                continue;
            }
            if (session.Warp.To(target, request.Rotation, 10, StepMs, request.Tolerance)) return true;
            Thread.Sleep(30);
        }
        return false;
    }

    /// <summary>
    /// Kiểm rơi trong 0,2 s: đang tụt mạnh xuống dưới mặt đất = độn thổ; thấp hơn lưới nhưng đứng yên + game báo đứng vững trên đất
    /// thật thì KHÔNG phải (lưới lệch ~0,3 m ở bãi dốc). Thuyền: thấp hơn mặt nước đặt 0,2 m.
    /// </summary>
    private (bool Underground, float P1, float P2) CheckFall(float surface, float warpY, bool boat, bool hasGround)
    {
        Thread.Sleep(SettleMs);
        var p1 = session.Player.Position()?.Y ?? warpY;
        Thread.Sleep(FallWindowMs - SettleMs);
        var p2 = session.Player.Position()?.Y ?? p1;
        if (boat) return (p2 < warpY - 0.2f, p1, p2);
        if (!hasGround) return (false, p1, p2);
        var falling = p2 < p1 - 0.25f;
        var settled = Math.Abs(p2 - p1) < 0.05f;
        if (falling && p2 < surface + 0.05f) return (true, p1, p2);
        if (p2 >= surface - 0.15f) return (false, p1, p2);
        var stable = settled ? session.Player.Grounded() : false;
        return (!(stable == true || (stable == null && settled && p2 >= surface - SettleBelow)), p1, p2);
    }

    /// <summary>
    /// Chờ bản đồ đích tải xong + điều khiển được: tải trước xong (sysMap.m_IsPreLoadAssetComplete), bản đồ hết chờ (IsWaitingForLoad),
    /// nhân vật được đi (_isCanMove), không đang hồi sinh, có motor, vị trí đã đổi khỏi chỗ cũ.
    /// </summary>
    private TeleportStatus WaitMapReady(int mapId, Action<string>? status, CancellationToken cancel)
    {
        var start = DateTime.UtcNow;
        var initial = session.Player.Position();
        var unloaded = false;
        var dead = 0;
        while (DateTime.UtcNow - start < MapTimeout)
        {
            if (cancel.IsCancellationRequested) return TeleportStatus.Cancelled;
            dead = session.Alive() ? 0 : dead + 1;
            if (dead >= 10) return TeleportStatus.ConnectFailed;
            var map = session.Camera.Map();
            if (map?.Id != mapId || (!unloaded && DateTime.UtcNow - start < TimeSpan.FromSeconds(3)))
            {
                unloaded |= map?.Id != mapId;
                Thread.Sleep(Poll);
                continue;
            }
            status?.Invoke($"Đang tải bản đồ {map.Value.Name}...");
            if (session.Optional(() => Ready(initial), "chờ bản đồ")) return TeleportStatus.Success;
            Thread.Sleep(Poll);
        }
        return session.Camera.Map()?.Id == mapId ? TeleportStatus.PlayerNotReady : TeleportStatus.MapLoadTimeout;
    }

    /// <summary>1 lần kiểm bản đồ mới đã sẵn sàng (xem <see cref="WaitMapReady"/>).</summary>
    private bool Ready(Vec3? initial)
    {
        var m = session.Managed;
        var system = session.System("sysMap");
        if (system == 0 || session.Memory.Read(system + session.Il2Cpp.Field(m.ClassOf(system), "m_IsPreLoadAssetComplete"), 1) is not [1]) return false;
        var current = session.Camera.CurrentMap();
        if (!Bin.IsPtr(current) || session.Memory.Read(current + session.Il2Cpp.Field(m.ClassOf(current), "IsWaitingForLoad"), 1) is not [0]) return false;
        if (session.Character() == 0) return false;
        session.NotifySceneChanged();
        session.LocateActor();
        var control = session.Control;
        bool Flag(string field, byte expected) => session.Memory.Read(control + session.Il2Cpp.Field(session.ControlClass, field), 1) is [var b] && b == expected;
        if (!Flag("_isCanMove", 1) || !Flag("_isRespawnFalling", 0) || !Flag("_isRespawnStandUp", 0) || !Flag("_isHideBeforeRespawn", 0)) return false;
        if (session.Player.MotorBody() == 0 || session.Player.Position() is not { } p || p == Vec3.Zero) return false;
        if (initial is { } i && p.FlatDistance(i) < 0.5f) return false;
        Thread.Sleep(SettleMs);
        return true;
    }
}
