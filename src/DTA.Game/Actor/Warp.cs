using DTA.Game.Geometry;
using DTA.Game.Session;
using DTA.Runtime.Core;

namespace DTA.Game.Actor;

/// <summary>
/// Dịch chuyển tức thời bằng ghi thẳng KinematicCharacterMotor (không gọi hàm game): tắt va chạm (xuyên tường tới đích), đặt
/// _movePositionTarget (+ _moveRotationTarget), xoá vận tốc; đi bộ thì GIỮ dò đất (game tự đặt lên sàn - tắt dò đất là trọng lực kéo
/// tụt xuống dưới sàn = độn thổ). Đang cưỡi phương tiện thì dời cả xe + người, giữ nguyên chỗ ngồi tương đối. Luôn trả va chạm / dò đất.
/// </summary>
public sealed class Warp(GameSession session)
{
    private static readonly Logger L = Log.For("teleport");
    private static readonly byte[] Zero12 = new byte[12], Zero4 = new byte[4];

    /// <summary>Dời tới <paramref name="target"/> (hướng <paramref name="rotation"/> tuỳ chọn); thử tới khi cách đích ≤ <paramref name="tolerance"/> m.</summary>
    public bool To(Vec3 target, Quat? rotation = null, int attempts = 8, int stepMs = 25, float tolerance = 1.5f)
    {
        var body = session.Player.MotorBody();
        if (!Bin.IsPtr(body)) return false;
        var vehicle = session.Vehicles.Current();
        if (vehicle == null) return Repeat(Walking(body, target, rotation), [body], attempts, stepMs, () => Near(session.Player.Position(), target, tolerance));
        var (writes, playerTarget) = Riding(body, vehicle, target, rotation);
        var motors = vehicle.Motor != 0 ? new[] { body, vehicle.Motor } : [body];
        return Repeat(writes, motors, attempts, stepMs, () =>
            Near(session.Player.Position(), playerTarget, tolerance + 0.5f) || Near(session.World.Position(vehicle.Instance), target, tolerance));
    }

    /// <summary>Lệnh ghi khi đi bộ: vận tốc 0, không ép rời đất, vị trí tạm, khối warp (va chạm 0, dò đất 1), myLastPos.</summary>
    private List<(long, byte[])> Walking(long body, Vec3 target, Quat? rotation)
    {
        var writes = MotorWarp(body, target, rotation ?? default, rotation != null, grounding: true, unground: false);
        if (session.Character() is var character and not 0) writes.Add(LastPos(character, target));
        return writes;
    }

    /// <summary>Lệnh ghi khi cưỡi: tính chỗ người so với xe, đặt xe tới đích (hướng mới nếu có) rồi người theo đúng chỗ đó.</summary>
    private (List<(long, byte[])> Writes, Vec3 Player) Riding(long body, Vehicle vehicle, Vec3 target, Quat? rotation)
    {
        var vehiclePose = session.World.Pose(vehicle.Instance) ?? new Pose(target, Quat.Identity);
        var playerPose = session.Player.Pose() ?? new Pose(session.Player.Position() ?? vehiclePose.Position, Quat.Identity);
        var qv = vehiclePose.Rotation.Normalized();
        var inverse = qv.Inverse();
        var offset = inverse.Rotate(playerPose.Position - vehiclePose.Position);
        var relative = inverse.Multiply(playerPose.Rotation.Normalized());
        var vehicleRotation = rotation?.Normalized() ?? qv;
        var playerTarget = target + vehicleRotation.Rotate(offset);
        var writes = MotorWarp(body, playerTarget, vehicleRotation.Multiply(relative), true, grounding: false, unground: true);
        if (vehicle.Motor != 0) writes.AddRange(MotorWarp(vehicle.Motor, target, vehicleRotation, true, grounding: false, unground: true));
        writes.Add(LastPos(vehicle.Instance, target));
        if (session.Character() is var character and not 0) writes.Add(LastPos(character, playerTarget));
        return (writes, playerTarget);
    }

    /// <summary>Ghi warp lặp tới khi tới nơi (mỗi lượt 1 lệnh shell), cuối cùng luôn trả va chạm / dò đất cho mọi motor đã đụng.</summary>
    private bool Repeat(List<(long, byte[])> writes, long[] motors, int attempts, int stepMs, Func<bool> arrived)
    {
        try
        {
            for (var i = 0; i < attempts; i++)
            {
                session.Memory.WriteMany(writes);
                Thread.Sleep(stepMs);
                if (arrived()) return true;
            }
            L.Debug($"Warp chưa tới đích sau {attempts} lượt");
            return false;
        }
        finally
        {
            session.Memory.WriteMany(motors.SelectMany(Restore).ToList());
        }
    }

    /// <summary>Các ô motor cho 1 lần warp (theo tên field trong dump, không offset cứng).</summary>
    private List<(long, byte[])> MotorWarp(long motor, Vec3 target, Quat rotation, bool rotate, bool grounding, bool unground)
    {
        var f = Fields(motor);
        var position = Bin.Pack(target.X, target.Y, target.Z);
        var writes = new List<(long, byte[])>
        {
            (motor + f("BaseVelocity"), Zero12),
            (motor + f("_mustUnground"), [(byte)(unground ? 1 : 0)]),
            (motor + f("_transientPosition"), position),
            (motor + f("_solveMovementCollisions"), [0, (byte)(grounding ? 1 : 0), 1]),
            (motor + f("_movePositionTarget"), position),
        };
        if (!rotate) return writes;
        writes.Add((motor + f("_moveRotationDirty"), [1]));
        writes.Add((motor + f("_moveRotationTarget"), Bin.Pack(rotation.X, rotation.Y, rotation.Z, rotation.W)));
        return writes;
    }

    /// <summary>Trả va chạm + dò đất, bỏ ép rời đất, xoá bộ đếm + vận tốc.</summary>
    private IEnumerable<(long, byte[])> Restore(long motor)
    {
        var f = Fields(motor);
        yield return (motor + f("_solveMovementCollisions"), [1, 1]);
        yield return (motor + f("_mustUnground"), [0]);
        yield return (motor + f("_mustUngroundTimeCounter"), Zero4);
        yield return (motor + f("BaseVelocity"), Zero12);
    }

    /// <summary>Ô myLastPos (game tự ghi vị trí) của nhân vật / xe.</summary>
    private (long, byte[]) LastPos(long actor, Vec3 at) =>
        (actor + session.Il2Cpp.Field(session.Managed.ClassOf(actor), "myLastPos"), Bin.Pack(at.X, at.Y, at.Z));

    private Func<string, int> Fields(long motor)
    {
        var klass = session.Managed.ClassOf(motor);
        return name => session.Il2Cpp.Field(klass, name);
    }

    private static bool Near(Vec3? at, Vec3 target, float tolerance) => at is { } p && p.FlatDistance(target) <= tolerance;
}
