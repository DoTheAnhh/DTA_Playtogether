using DTA.Game.Geometry;
using DTA.Game.Session;
using DTA.Runtime.Core;

namespace DTA.Game.Actor;

/// <summary>
/// Chuyển động nhân vật lúc này: vị trí, vận tốc (đã trừ phần bị chặn), hướng cần điều khiển, tốc độ, đứng trên đất, đang nhảy,
/// được đi, Sensed = số liệu do motor của game báo (false = tự suy từ quãng vừa đi), đang leo thang.
/// </summary>
public readonly record struct Motion(float X, float Y, float Z, float Vx, float Vz, float Ix, float Iz, float Speed,
                                     bool Grounded, bool Jumping, bool Free, bool Sensed, bool Ladder = false);

/// <summary>Nơi đọc motor: object điều khiển, 2 class phải thấy, 3 vùng nhớ, offset từng số liệu trong vùng.</summary>
internal sealed record MotorPlan(long Control, long BodyClass, long UnitClass, (long Addr, int Size)[] Spans,
                                 int Position, int Velocity, int Ground, int Pushed, int Stable, int Rate, int Free, int Jumping, int Ladder)
{
    public long Body => Spans[0].Addr;
}

/// <summary>Vị trí / hướng / chuyển động nhân vật của người chơi - dùng chung theo phiên.</summary>
public sealed class PlayerReader(GameSession session)
{
    private static readonly TimeSpan TrailWindow = TimeSpan.FromSeconds(0.25);
    private readonly List<(DateTime At, float X, float Z)> _trail = [];
    private MotorPlan? _motor;
    private long _motorControl = -1;
    private (long Holder, long Character)? _me;

    /// <summary>Tư thế nhân vật từ Transform; đã biết nhân vật thì 1 lượt lệnh (kèm kiểm ô giữ con trỏ còn như cũ).</summary>
    public Pose? Pose() => session.Optional<Pose?>(() =>
    {
        if (_me is var (holder, character) && session.World.Pose(character, true, (holder, Bin.Pack(character))) is { } known) return known;
        var slot = session.CharacterSlot();
        var current = slot != 0 ? (long)session.Memory.U64(slot) : 0;
        _me = current != 0 ? (slot, current) : null;
        return current != 0 ? session.World.Pose(current, true) : null;
    }, "tư thế nhân vật");

    /// <summary>Vị trí nhân vật; Unity chưa đọc được Transform thì lấy myLastPos game tự ghi.</summary>
    public Vec3? Position()
    {
        if (Pose() is { } pose) return pose.Position;
        if (_me is not var (_, character)) return null;
        var raw = session.Memory.Read(character + session.Il2Cpp.Field(session.Managed.ClassOf(character), "myLastPos"), 12);
        return raw != null ? new Vec3(Bin.F32(raw, 0), Bin.F32(raw, 4), Bin.F32(raw, 8)) : null;
    }

    /// <summary>(vị trí, hướng mặt x/z đơn vị); null nếu chưa đọc được.</summary>
    public (Vec3 Position, (float X, float Z) Forward)? Facing() =>
        Pose() is { } p && p.Rotation.GroundForward() is { } f ? (p.Position, f) : null;

    /// <summary>
    /// Chuyển động do motor của game báo (1 lượt đọc ~7 ms). Object điều khiển vừa đổi thì xác định lại 1 lần. Bản game không đọc
    /// được motor thì chỉ còn vị trí + vận tốc suy từ ~0,25 s gần nhất.
    /// </summary>
    public Motion? Motion()
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            if (_motorControl != session.Control)
            {
                _motor = session.Optional(() => LocateMotor(), "motor");
                _motorControl = session.Control;
            }
            if (_motor is not { } m) break;
            var data = session.Memory.ReadMany(m.Spans);
            if (data is [{ } body, { } unit, { } flags] && Bin.U64(body, 0) == m.BodyClass && Bin.U64(unit, 0) == m.UnitClass)
                return new Motion(Bin.F32(body, m.Position), Bin.F32(body, m.Position + 4), Bin.F32(body, m.Position + 8),
                    Bin.F32(body, m.Velocity), Bin.F32(body, m.Velocity + 8), Bin.F32(unit, m.Pushed), Bin.F32(unit, m.Pushed + 8),
                    Bin.F32(unit, m.Stable) * Bin.F32(unit, m.Rate), body[m.Ground + 1] != 0, flags[m.Jumping] != 0, flags[m.Free] != 0, true,
                    m.Ladder >= 0 && Bin.I32(flags, m.Ladder) != 0);
            _motorControl = -1;
            session.LocateActor();
        }
        return Estimated();
    }

    /// <summary>Motor đứng vững trên đất thật (GroundingStatus.FoundAnyGround + IsStableOnGround); null nếu không đọc được.</summary>
    public bool? Grounded()
    {
        var body = MotorBody();
        var raw = body != 0 ? session.Memory.Read(body + session.Il2Cpp.Field(session.Managed.ClassOf(body), "GroundingStatus"), 2) : null;
        return raw != null ? raw[0] != 0 && raw[1] != 0 : null;
    }

    /// <summary>KinematicCharacterMotor của nhân vật (0 nếu không ở kiểu đi bộ).</summary>
    public long MotorBody()
    {
        if (_motorControl != session.Control || _motor == null)
        {
            _motor = session.Optional(() => LocateMotor(), "motor");
            _motorControl = session.Control;
        }
        return _motor?.Body ?? 0;
    }

    /// <summary>Vị trí + vận tốc tự suy (motor không đọc được).</summary>
    private Motion? Estimated()
    {
        if (Position() is not { } place) return null;
        var now = DateTime.UtcNow;
        _trail.Add((now, place.X, place.Z));
        while (_trail.Count > 2 && now - _trail[1].At >= TrailWindow) _trail.RemoveAt(0);
        var (since, x, z) = _trail[0];
        var dt = (float)(now - since).TotalSeconds;
        var (vx, vz) = dt is > 0.05f and < 1 ? ((place.X - x) / dt, (place.Z - z) / dt) : (0f, 0f);
        return new Motion(place.X, place.Y, place.Z, vx, vz, 0, 0, 0, true, false, true, false);
    }

    /// <summary>control -> kinematicControllerDefault (hoặc TreasureHunt) -> Motor; null nếu không ở kiểu đi bộ / chưa sẵn sàng.</summary>
    private MotorPlan? LocateMotor()
    {
        var (control, klass) = (session.Control, session.ControlClass);
        if (!Bin.IsPtr(control) || !Bin.IsPtr(klass)) return null;
        var il2cpp = session.Il2Cpp;
        var fields = il2cpp.AllFields(klass);
        var unitAt = fields.GetValueOrDefault("kinematicControllerDefault", fields.GetValueOrDefault("kinematicControllerTreasureHunt"));
        var unit = unitAt != 0 ? (long)session.Memory.U64(control + unitAt) : 0;
        var unitClass = session.Managed.ClassOf(unit);
        if (!Bin.IsPtr(unit) || !Bin.IsPtr(unitClass)) return null;
        var body = (long)session.Memory.U64(unit + il2cpp.Field(unitClass, "Motor"));
        var bodyClass = session.Managed.ClassOf(body);
        if (!Bin.IsPtr(body) || !Bin.IsPtr(bodyClass)) return null;
        int position = il2cpp.Field(bodyClass, "_transientPosition"), velocity = il2cpp.Field(bodyClass, "BaseVelocity"), ground = il2cpp.Field(bodyClass, "GroundingStatus");
        int pushed = il2cpp.Field(unitClass, "moveInputVector"), stable = il2cpp.Field(unitClass, "_stableMoveSpd"), rate = il2cpp.Field(unitClass, "_moveSpeedRate");
        if (!fields.TryGetValue("_isCanMove", out var free) && !fields.TryGetValue("<IsCanMove>k__BackingField", out free)) return null;
        if (!fields.TryGetValue("<IsJumping>k__BackingField", out var jumping)) return null;
        var ladder = fields.GetValueOrDefault("attachLadderHash", -1);
        var low = new[] { free, jumping, ladder }.Where(o => o >= 0).Min();
        (long, int)[] spans =
        [
            (body, new[] { position, velocity, ground }.Max() + 12),
            (unit, new[] { pushed + 12, stable + 4, rate + 4 }.Max()),
            (control + low, new[] { free + 1, jumping + 1, Math.Max(ladder, 0) + 4 }.Max() - low),
        ];
        return new MotorPlan(control, bodyClass, unitClass, spans, position, velocity, ground, pushed, stable, rate, free - low, jumping - low, ladder < 0 ? -1 : ladder - low);
    }
}
