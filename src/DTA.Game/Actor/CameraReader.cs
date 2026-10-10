using DTA.Game.Geometry;
using DTA.Game.Session;
using DTA.Runtime.Core;

namespace DTA.Game.Actor;

/// <summary>Camera chính + bản đồ đang chơi (sysMap.m_CurrentMap) - dùng chung theo phiên.</summary>
public sealed class CameraReader(GameSession session)
{
    private static readonly TimeSpan FovRefresh = TimeSpan.FromSeconds(1);
    private static readonly Dictionary<int, string> FriendlyMaps = new() { [1001] = "Plaza", [1301] = "Khu nghỉ dưỡng", [1201] = "Khu cắm trại", [1101] = "Khu trung tâm" };
    private (long Holder, long Current, long Brain)? _camera;
    private (float Fov, DateTime At) _fov;

    /// <summary>Ô sysMap.m_CurrentMap (giữ bản đồ đang chơi); 0 nếu chưa có.</summary>
    public long MapHolder()
    {
        var system = session.System("sysMap");
        return system != 0 ? system + session.Il2Cpp.Field(session.Managed.ClassOf(system), "m_CurrentMap") : 0;
    }

    /// <summary>Bản đồ đang chơi (object IMap); 0 nếu đang chuyển cảnh.</summary>
    public long CurrentMap() => MapHolder() is var h and not 0 ? (long)session.Memory.U64(h) : 0;

    /// <summary>(mã bản đồ, tên) nơi nhân vật đang đứng; null nếu đang chuyển cảnh.</summary>
    public (int Id, string Name)? Map() => session.Optional<(int, string)?>(() =>
    {
        var current = CurrentMap();
        var id = session.Managed.I32(current, "MapSID");
        if (id is not { } mid) return null;
        if (FriendlyMaps.TryGetValue(mid, out var friendly)) return (mid, friendly);
        var scene = session.Managed.Ptr(current, "sceneName");
        var name = scene != 0 ? session.Memory.Strings([scene], 64).GetValueOrDefault(scene, "") : "";
        return (mid, (name.StartsWith("map_") ? name[4..] : name).Replace('_', ' ').Trim());
    }, "bản đồ");

    /// <summary>Tư thế camera chính (cinemachineBrain của bản đồ), theo dõi kèm kiểm bản đồ còn như cũ.</summary>
    public Pose? Pose() => session.Optional<Pose?>(() =>
    {
        if (_camera is var (holder, current, brain) && session.World.Pose(brain, true, (holder, Bin.Pack(current))) is { } known) return known;
        var h = MapHolder();
        var map = h != 0 ? (long)session.Memory.U64(h) : 0;
        var b = session.Managed.Ptr(map, "cinemachineBrain");
        if (b == 0) return null;
        _camera = (h, map, b);
        return session.World.Pose(b, true);
    }, "camera");

    /// <summary>Hướng nhìn camera chiếu xuống đất (x, z) - cần điều khiển của game đi theo hướng này.</summary>
    public (float X, float Z)? Direction() => Pose()?.Rotation.GroundForward();

    /// <summary>(vị trí, xoay, góc nhìn dọc độ) - đủ để chiếu điểm bản đồ lên màn hình. Góc nhìn đọc tối đa 1 lần / giây.</summary>
    public (Vec3 Position, Quat Rotation, float Fov)? View()
    {
        if (Pose() is not { } pose || _camera is not var (_, _, brain)) return null;
        if (DateTime.UtcNow - _fov.At > FovRefresh)
        {
            var raw = session.Memory.Read(brain + session.Il2Cpp.Field(session.Managed.ClassOf(brain), "CurrentCameraState"), 4);
            var fov = raw != null ? Bin.F32(raw, 0) : 0;
            _fov = (fov is >= 5 and <= 170 ? fov : _fov.Fov > 0 ? _fov.Fov : 60, DateTime.UtcNow);
        }
        return (pose.Position, pose.Rotation, _fov.Fov);
    }

    /// <summary>CinemachineFreeLook của bản đồ (cinemachineCtrl / commonCinemachineCtrl -> cinemachineFreeLook); 0 nếu không có.</summary>
    public long FreeLook() => session.Optional(() =>
    {
        var map = CurrentMap();
        foreach (var field in new[] { "cinemachineCtrl", "commonCinemachineCtrl" })
        {
            var look = session.Managed.Ptr(session.Managed.Ptr(map, field), "cinemachineFreeLook");
            if (Bin.IsPtr(look)) return look;
        }
        return 0L;
    }, "FreeLook");

    /// <summary>Xoay camera tức thì ra sau lưng nhân vật (FreeLook.m_XAxis.Value = góc mặt); <paramref name="rotation"/> = hướng vừa đặt.</summary>
    public bool AlignBehind(Quat? rotation = null)
    {
        var forward = rotation?.GroundForward() ?? session.Player.Facing()?.Forward;
        var look = forward != null ? FreeLook() : 0;
        if (look == 0) return false;
        var degrees = MathF.Atan2(forward!.Value.X, forward.Value.Z) * 180 / MathF.PI;
        return session.Memory.Write(look + session.Il2Cpp.Field(session.Managed.ClassOf(look), "m_XAxis"), Bin.Pack(degrees));
    }

    /// <summary>Tắt cờ IsCameraLookAt của object điều khiển để game không tự kéo camera.</summary>
    public bool ClearLookAt()
    {
        if (session.Control == 0) session.LocateActor();
        return session.Control != 0 && session.Memory.Write(session.Control + session.Il2Cpp.Field(session.ControlClass, "IsCameraLookAt"), [0]);
    }

    /// <summary>Số cửa / cổng của bản đồ (MapCity.DoorTriggers); 0 nếu không có.</summary>
    public int DoorCount() => session.Optional(() => session.Managed.ArrayItems(DoorArray()).Count, "số cửa");

    /// <summary>Vùng chạm + điểm dắt vào / ra của mọi cửa, cổng (walker né): [(x, z)], gộp điểm cách nhau ≤ 0,5 m.</summary>
    public List<(float X, float Z)> Doors() => session.Optional(() =>
    {
        var spots = new List<(float X, float Z)>();
        var infos = session.Managed.ArrayItems(DoorArray()).Where(i => i != 0).Take(512);
        foreach (var trigger in infos.Select(i => session.Managed.Ptr(i, "DoorTrigger")).Where(t => t != 0))
        {
            var targets = new List<long> { trigger };
            foreach (var point in new[] { "DoorInPoint", "DoorOutStartPoint", "DoorOutEndPoint" })
                if (session.Il2Cpp.HasField(session.Managed.ClassOf(trigger), point)) targets.Add(session.Managed.Ptr(trigger, point));
            foreach (var target in targets.Where(t => t != 0))
                if (session.World.Position(target) is { } p && spots.All(s => MathF.Sqrt((p.X - s.X) * (p.X - s.X) + (p.Z - s.Z) * (p.Z - s.Z)) > 0.5f))
                    spots.Add((p.X, p.Z));
        }
        return spots;
    }, "cửa") ?? [];

    /// <summary>Mảng MapCity.DoorTriggers; 0 nếu bản đồ không có.</summary>
    private long DoorArray()
    {
        var current = CurrentMap();
        var klass = session.Managed.ClassOf(current);
        return klass != 0 && session.Il2Cpp.HasField(klass, "DoorTriggers") ? session.Managed.Ptr(current, "DoorTriggers") : 0;
    }
}
