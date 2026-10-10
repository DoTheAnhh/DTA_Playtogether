using DTA.Engine.Bots;
using DTA.Engine.Movement;
using DTA.Game.Geometry;

namespace DTA.Engine.Gather;

/// <summary>Phần đi tới vật của <see cref="GatherBot{T}"/>.</summary>
public abstract partial class GatherBot<T>
{
    /// <summary>
    /// Đi tới vật. Trên đường: vật còn / còn đúng loại không (bỏ ngay nếu người khác vừa lấy / người dùng đổi loại); mỗi giây xem có vật
    /// ưu tiên hơn, hoặc vật cùng nhóm mà quãng đi bộ ngắn hơn ≥ 3 m không (có thì bỏ, vòng lặp chọn lại); báo khoảng cách lên giao diện.
    /// </summary>
    protected virtual WalkResult Approach(T target, Vec3 position, List<T> targets)
    {
        if (Teleporting) return TeleportBeside(target);
        var name = Label(target);
        double reported = 0, compared = Now;
        var (reach, solid) = Stand(target);

        bool StillWanted()
        {
            var fresh = Refresh(target);
            var here = Session.Player.Position();
            if (fresh != null && !Wanted(fresh)) return false;
            if (fresh != null && here is { } h && Now - compared >= SwitchEvery)
            {
                compared = Now;
                if (Better(target, fresh, h, reach, solid)) return false;
            }
            if (fresh != null && here is { } p && Now - reported > 0.3)
            {
                reported = Now;
                Gather.Target(NavMap.Dist(fresh.X, fresh.Z, p.X, p.Z), name);
            }
            return fresh != null;
        }

        var distance = NavMap.Dist(target.X, target.Z, position.X, position.Z);
        Gather.Target(distance, name);
        Events.Status($"Đang đi tới {name} cách {distance:F0} m...", Level.Info);
        return GoTo(target, reach, solid, StillWanted, count => Events.Status($"Bị vật cản chặn - đang tìm đường vòng (lần {count})...", Level.Warn), Obstacles(target, targets));
    }

    /// <summary>Có vật đáng làm hơn vật đang nhắm không (ưu tiên hơn, hoặc cùng nhóm mà đi bộ ngắn hơn hẳn).</summary>
    private bool Better(T target, T fresh, Vec3 here, float reach, float solid)
    {
        var left = NavMap.Dist(fresh.X, fresh.Z, here.X, here.Z);
        var everything = Targets();
        var others = everything.Where(o => o.Uid != target.Uid && !Dropped(o) && Wanted(o)).ToList();
        var rank = Priority(target);
        if (others.Any(o => Priority(o) < rank)) return true;
        var rivals = others.Where(o => Priority(o) == rank && NavMap.Dist(o.X, o.Z, here.X, here.Z) - MaxStand(o) < left - SwitchGain).ToList();
        if (rivals.Count == 0) return false;
        var mine = Walk.Cost(here, (fresh.X, fresh.Z), Math.Max(reach, solid), Obstacles(fresh, everything));
        var limit = mine is { } m ? m - SwitchGain : float.PositiveInfinity;
        foreach (var other in rivals.OrderBy(o => NavMap.Dist(o.X, o.Z, here.X, here.Z)))
            if (Walk.Cost(here, (other.X, other.Z), MaxStand(other), Obstacles(other, everything), limit) is { } cost && (mine == null || cost < mine - SwitchGain)) return true;
        return false;
    }

    /// <summary>Cách tâm vật bao nhiêu mét khi dịch chuyển tới cạnh nó.</summary>
    protected virtual float TeleportOffset(T target) => 0.4f;

    /// <summary>Tới được nếu sau dịch chuyển cách vật ≤ ngần này mét.</summary>
    protected virtual float TeleportArrive(T target) => 1.2f;

    /// <summary>
    /// Dịch chuyển tới sát cạnh vật (phía nhân vật đang đứng), mặt quay thẳng vào vật, không xoay camera; pipeline chung có chống độn
    /// thổ. Không có mặt đất an toàn thì bỏ vật 1 lúc.
    /// </summary>
    protected WalkResult TeleportBeside(T target)
    {
        var name = Label(target);
        Events.Status($"Dịch chuyển tới {name}...", Level.Info);
        if (Session.Player.Position() is not { } here) return WalkResult.Lost;
        float dx = here.X - target.X, dz = here.Z - target.Z, length = MathF.Sqrt(dx * dx + dz * dz);
        if (length < 0.1f) (dx, dz, length) = (0, -1, 1);
        var offset = TeleportOffset(target);
        var spot = new Vec3(target.X + dx / length * offset, target.Y, target.Z + dz / length * offset);
        var result = Session.Teleport.Go(new DTA.Game.Actor.TeleportRequest(spot, Rotation: Quat.LookAt(spot, new Vec3(target.X, target.Y, target.Z))), cancel: Cancel);
        if (!result.Ok || result.Position is not { } actual)
        {
            Events.Status($"Dịch chuyển tới {name} không thành công ({result.Status})", Level.Warn);
            return WalkResult.Stuck;
        }
        var distance = NavMap.Dist(actual.X, actual.Z, target.X, target.Z);
        if (distance > TeleportArrive(target)) return WalkResult.Stuck;
        Gather.Target(distance, name);
        return WalkResult.Arrived;
    }

    private float MaxStand(T target) => Stand(target) is var (r, s) ? Math.Max(r, s) : 0;

    /// <summary>Đi tới vật đã chọn; chức năng có cách tiếp cận riêng (rình bọ) thì viết lại.</summary>
    protected virtual WalkResult GoTo(T target, float reach, float solid, Func<bool> stillWanted, Action<int> onEscape, IEnumerable<(float X, float Z, float R)> avoid) =>
        Walk.Go((target.X, target.Z), reach, stillWanted, onEscape, solid, avoid);
}
