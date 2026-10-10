using DTA.Game.Actor;
using DTA.Runtime.Core;

namespace DTA.Engine.Movement;

/// <summary>Vòng điều khiển cần của <see cref="Walker"/>.</summary>
public sealed partial class Walker
{
    private const double Tick = 0.01, Settle = 0.2, LookEvery = 0.4, AimEvery = 0.1, JumpWait = 0.35, Nudge = 0.5, GrabWait = 0.25;
    private const double FrozenMax = 4, Watch = 3, LeapTime = 1.3, PinnedTime = 10, NearTime = 0.8, LookMax = 2, LadderJumps = 1.5, LadderMax = 6, FollowEvery = 0.3;
    private const float FineReach = 0.5f, SlowWithin = 0.6f, SlowTilt = 0.5f, RunSpeed = 4.5f, AxisMin = 0.3f, BlockRatio = 0.35f, BlockTime = 0.2f;
    private const float FollowStep = 0.4f, FollowShare = 0.15f, JumpNear = 0.7f, GrabStep = 0.15f, WatchGain = 1, PinnedRoom = 2.5f, MinSpeed = 1.2f, MinTilt = 0.35f;
    private const int Ahead = 12, GrabTries = 12, MaxDetours = 30, LeapFails = 2;

    /// <summary>
    /// Đi tới <paramref name="target"/> tới khi cách ≤ reach rồi thả cần. Mỗi 0,4 s hỏi <paramref name="stillWanted"/>; gặp chỗ chặn phải tính
    /// đường vòng thì báo <paramref name="onEscape"/>(lần). <paramref name="solid"/> = đích có thân chắn: bị chặn trong vòng solid m coi như
    /// tới. <paramref name="avoid"/> = vật chắn tạm (x, z, r). <paramref name="pace"/>(chuyển động, quãng còn) -> (độ nghiêng 0..1, tốc độ
    /// tối đa) hoặc null = thôi đi (rình bọ...). <paramref name="follow"/> = vị trí mới nhất của đích đang di chuyển: dời quá max(0,4 m, 15% quãng còn) thì đổi đích (≤ 1 lần / 0,3 s)
    /// + tính lại đường NGAY TRONG lượt đi, ngón vẫn giữ trên cần (không nhấc lên đè lại kiểu táp táp).
    /// </summary>
    public WalkResult Go((float X, float Z) target, float reach, Func<bool>? stillWanted = null, Action<int>? onEscape = null, float solid = 0,
                         IEnumerable<(float X, float Z, float R)>? avoid = null, Func<Motion, float, (float Tilt, float Top)?>? pace = null,
                         Func<(float X, float Z)?>? follow = null)
    {
        stillWanted ??= () => true;
        onEscape ??= _ => { };
        var avoidList = avoid?.ToList() ?? [];
        Trapped = WalkedIn = false;
        _stick ??= session.Ui.Joystick();
        if (_stick is not var (cx, cy, radius) || !touch.CanHold || session.Open.Top().Addr != 0) return WalkResult.Blocked;
        var forward = session.Camera.Direction();
        var motion = session.Player.Motion();
        if (forward == null || motion is not { } m0) return WalkResult.Lost;
        if (NavMap.Dist(target.X, target.Z, m0.X, m0.Z) <= reach) return WalkResult.Arrived;
        var nav = Nav();
        nav.Reopen();
        var accept = Math.Max(Math.Max(reach, solid), NavMap.CellSize * 0.75f);
        var extra = nav.Around(avoidList, Doors);
        var here = NavMap.Cell(m0.X, m0.Z);
        nav.Visit(null, here, !m0.Grounded, m0.Y);
        if (Plan(nav, here, target, accept, m0.Y, extra) is not var (path, _)) return WalkResult.Stuck;
        var tails = Tails(path);
        var start = (m0.X, m0.Z);
        var deadline = Now + (NavMap.Dist(path[0].X, path[0].Z, start.Item1, start.Item2) + tails[0]) / MinSpeed + 30;
        int detours = 0, index = 0, regrabs = 0;
        var aim = path[0];
        double aimed = 0, touched, settled = 0, leaped = 0, frozen = 0, replanned = 0, climbing = 0, hopped = 0;
        var taken = false;
        var spot = start;
        var held = new Dictionary<int, double>();
        (float X, float Z) heading = (0, 0);
        float tilt = 1, peak = 0;
        ((int, int) From, HashSet<(int, int)> To)? jump = null;
        var tried = new HashSet<Edge>();
        ((int I, int J) From, (int I, int J) To, double At)? leap = null;
        var fails = new Dictionary<Edge, int>();
        var pinned = (X: start.Item1, Z: start.Item2, At: Now);
        var flying = false;
        (double Until, int Side) nudge = (0, 1);
        var watch = (Best: float.PositiveInfinity, At: 0.0);
        double ticked = Now, looked = Now, now = Now, followed = 0;

        bool Replan(bool drifted = false)
        {
            if (Plan(nav, here, target, accept, motion?.Y, extra) is not var (found, _) || nav.Shuts(here, found)) return false;
            (path, tails, index, aim, aimed) = (found, Tails(found), 0, found[0], 0);
            if (!drifted) watch = (float.PositiveInfinity, now);
            return true;
        }

        try
        {
            touch.Hold(cx, cy);
            Thread.Sleep(50);
            touched = Now;
            while (true)
            {
                if (!running()) return WalkResult.Stopped;
                now = Now;
                motion = session.Player.Motion();
                if (motion is not { } mo) return WalkResult.Lost;
                float x = mo.X, z = mo.Z;
                var airborne = !mo.Grounded;
                var cell = NavMap.Cell(x, z);
                if (cell != here)
                {
                    nav.Visit(here, cell, airborne, mo.Y);
                    if (jump is { } j && here == j.From && j.To.Contains(cell)) nav.Jumped(here, cell);
                    here = cell;
                }
                if (flying && !airborne)
                {
                    nav.Visit(null, here, false, mo.Y);
                    (jump, settled) = (null, Math.Max(settled, now + Settle));
                }
                flying = airborne;
                var distance = NavMap.Dist(target.X, target.Z, x, z);
                if (now - followed >= FollowEvery && follow?.Invoke() is { } moved
                    && NavMap.Dist(moved.X, moved.Z, target.X, target.Z) > Math.Max(FollowStep, distance * FollowShare))
                {
                    (target, deadline, followed) = (moved, deadline + 5, now);
                    if (!Replan()) return WalkResult.Stuck;
                    distance = NavMap.Dist(target.X, target.Z, x, z);
                }
                if (distance <= reach) return WalkResult.Arrived;
                if (mo.Ladder)
                {
                    if (climbing == 0)
                    {
                        climbing = now;
                        nav.Trap(x + heading.X * 0.5f, z + heading.Z * 0.5f);
                        onEscape(detours + 1);
                    }
                    if (now - climbing > LadderMax) return WalkResult.Blocked;
                    if (now - hopped > 0.6 && now - climbing <= LadderJumps)
                    {
                        hopped = now;
                        Jump();
                    }
                    touch.Hold(cx, cy + (now - climbing > LadderJumps ? radius : 0));
                    (deadline, ticked) = (deadline + now - ticked, now);
                    Thread.Sleep(50);
                    continue;
                }
                if (climbing != 0)
                {
                    (climbing, detours) = (0, detours + 1);
                    held.Clear();
                    (settled, pinned, extra) = (now + Settle, (x, z, now), nav.Around(avoidList, Doors));
                    if (detours > MaxDetours || !Replan()) return WalkResult.Stuck;
                }
                float limit = 1, top = 0;
                if (pace != null)
                {
                    if (pace(mo, distance) is not var (paceTilt, paceTop)) return WalkResult.Stopped;
                    (limit, top) = (paceTilt <= 0 ? 0 : Math.Clamp(paceTilt, MinTilt, 1), paceTop);
                }
                var paused = limit <= 0;
                if (paused) deadline += now - ticked;
                ticked = now;
                if (now > deadline) return WalkResult.Stuck;
                if (!mo.Free || !taken || paused || NavMap.Dist(x, z, pinned.X, pinned.Z) > PinnedRoom)
                    pinned = mo.Free && taken ? (x, z, now) : (pinned.X, pinned.Z, now);
                else if (now - pinned.At > PinnedTime && !(solid > 0 && distance <= solid && nav.Reaches(here, target))) return WalkResult.Stuck;
                if (leap is { } lp && (here != lp.From || (now - lp.At > LeapTime && !airborne)))
                {
                    if (here == lp.From)
                    {
                        var edge = NavMap.EdgeOf(lp.From, lp.To);
                        fails[edge] = fails.GetValueOrDefault(edge) + 1;
                        if (fails[edge] >= LeapFails)
                        {
                            onEscape(++detours);
                            nav.Block(lp.From, lp.To);
                            leap = null;
                            if (detours > MaxDetours || !Replan()) return WalkResult.Stuck;
                        }
                    }
                    leap = null;
                }
                var near = distance - reach <= NearTime * Math.Max(MathF.Sqrt(mo.Vx * mo.Vx + mo.Vz * mo.Vz), 1);
                if (now - looked >= (near ? LookMax : LookEvery))
                {
                    looked = now;
                    if (session.Open.Top().Addr != 0)
                    {
                        nav.Trap(x + heading.X * 0.5f, z + heading.Z * 0.5f);
                        WalkedIn = true;
                        return WalkResult.Blocked;
                    }
                    if (!stillWanted()) return WalkResult.Stopped;
                    forward = session.Camera.Direction() ?? forward;
                }

                var pushing = mo.Sensed ? MathF.Sqrt(mo.Ix * mo.Ix + mo.Iz * mo.Iz) : tilt;
                if (mo.Sensed) taken = pushing >= 0.2f;
                else if (!taken) taken = NavMap.Dist(x, z, spot.Item1, spot.Item2) >= GrabStep;
                if (!mo.Free)
                {
                    frozen = frozen == 0 ? now : frozen;
                    if (now - frozen > FrozenMax) return WalkResult.Blocked;
                    (settled, watch) = (0, (watch.Best, now));
                    held.Clear();
                }
                else if (paused)
                {
                    (frozen, settled, touched, spot, watch) = (0, 0, now, (x, z), (watch.Best, now));
                    held.Clear();
                }
                else if (!taken)
                {
                    (frozen, settled) = (0, 0);
                    held.Clear();
                    if (now - touched > GrabWait)
                    {
                        if (++regrabs > GrabTries) return WalkResult.Blocked;
                        touch.Release();
                        Thread.Sleep(30);
                        touch.Hold(cx, cy);
                        Thread.Sleep(50);
                        (touched, spot, watch) = (Now, (x, z), (watch.Best, now));
                    }
                }
                else
                {
                    frozen = 0;
                    settled = settled == 0 ? now + Settle : settled;
                }

                if (nudge.Until != 0 && now >= nudge.Until)
                {
                    nudge = (0, -nudge.Side);
                    if (!Replan()) return WalkResult.Stuck;
                }

                peak = Math.Max(peak, MathF.Sqrt(mo.Vx * mo.Vx + mo.Vz * mo.Vz));
                var want = mo.Sensed && pushing > 0.01f ? (X: mo.Ix / pushing, Z: mo.Iz / pushing) : heading;
                if (settled != 0 && now >= settled && !airborne && nudge.Until == 0)
                {
                    var expected = (top > 0 ? top : mo.Speed > 0 ? mo.Speed : Math.Max(peak, RunSpeed)) * Math.Min(pushing, 1);
                    foreach (var (axis, push, speed) in new[] { (0, want.X, mo.Vx), (1, want.Z, mo.Vz) })
                    {
                        if (Math.Abs(push) >= AxisMin && speed * (push > 0 ? 1 : -1) < BlockRatio * Math.Abs(push) * expected) held.TryAdd(axis, now);
                        else held.Remove(axis);
                    }
                }
                else held.Clear();
                var stalled = now - watch.At > Watch && taken && mo.Free && !airborne && nudge.Until == 0 && !paused;
                if (stalled || (held.Count > 0 && now - held.Values.Min() >= BlockTime))
                {
                    if (solid > 0 && distance <= solid && nav.Reaches(here, target)) return WalkResult.Arrived;
                    var axes = held.Count > 0 ? held.Keys.Order().ToList() : [Math.Abs(want.X) >= Math.Abs(want.Z) ? 0 : 1];
                    var sides = axes.Select(a => a == 0 ? (here.I + (want.X > 0 ? 1 : -1), here.J) : (here.I, here.J + (want.Z > 0 ? 1 : -1))).ToList();
                    var edges = sides.Select(s => NavMap.EdgeOf(here, s)).ToList();
                    held.Clear();
                    watch = (watch.Best, now);
                    if (!stalled && !edges.Any(e => tried.Contains(e) || nav.Walls.ContainsKey(e) || nav.Suspects.ContainsKey(e)))
                    {
                        tried.UnionWith(edges);
                        (jump, leaped, settled) = ((here, sides.ToHashSet()), now, now + JumpWait);
                        Jump();
                    }
                    else
                    {
                        (detours, deadline) = (detours + 1, deadline + 3);
                        if (detours > MaxDetours) return WalkResult.Stuck;
                        onEscape(detours);
                        var fresh = sides.Where((s, k) => !nav.Walls.ContainsKey(edges[k])).ToList();
                        foreach (var side in fresh.Count > 0 ? fresh : sides) nav.Block(here, side);
                        if (nav.Enclosed(here, mo.Y))
                        {
                            Trapped = true;
                            return WalkResult.Stuck;
                        }
                        if (fresh.Count == 0) nudge = (now + Nudge, nudge.Side);
                        else if (!Replan()) return WalkResult.Stuck;
                    }
                }

                if (now - aimed >= AimEvery)
                {
                    aimed = now;
                    int? found = null;
                    for (var k = Math.Min(path.Count - 1, index + Ahead); k > index; k--)
                        if (nav.Clear(x, z, path[k].X, path[k].Z, mo.Y, avoid: extra)) { found = k; break; }
                    if (found == null && !nav.Clear(x, z, aim.X, aim.Z, mo.Y, avoid: extra))
                    {
                        var window = Enumerable.Range(index, Math.Min(path.Count, index + Ahead + 1) - index).OrderBy(k => NavMap.Dist(path[k].X, path[k].Z, x, z));
                        found = window.Cast<int?>().FirstOrDefault(k => nav.Clear(x, z, path[k!.Value].X, path[k.Value].Z, mo.Y, false));
                        if (found == null && nudge.Until == 0 && !airborne && now - replanned > 0.5)
                        {
                            replanned = now;
                            if (!Replan(true)) return WalkResult.Stuck;
                            found = 0;
                        }
                    }
                    if (found is { } f) (index, aim) = (f, path[f]);
                }
                float dx = aim.X - x, dz = aim.Z - z;
                var length = MathF.Sqrt(dx * dx + dz * dz);
                if (length < 0.2f && index < path.Count - 1)
                {
                    (index, aim, aimed) = (index + 1, path[index + 1], now);
                    continue;
                }
                if (length + tails[index] < watch.Best - WatchGain * limit) watch = (length + tails[index], now);
                (dx, dz) = length > 1e-6f ? (dx / length, dz / length) : (0, 0);
                var ahead = Math.Abs(dx) >= Math.Abs(dz) ? (here.I + Math.Sign(dx), here.J) : (here.I, here.J + Math.Sign(dz));
                if (nudge.Until != 0) (dx, dz) = (-dz * nudge.Side, dx * nudge.Side);
                else if (now - leaped > 0.8 && !airborne && ahead != here && nav.Jumps.Contains(NavMap.EdgeOf(here, ahead))
                         && NavMap.Cell(x + dx * JumpNear, z + dz * JumpNear) != here)
                {
                    (leaped, settled, leap) = (now, now + JumpWait, (here, ahead, now));
                    Jump();
                }
                if (dx * heading.X + dz * heading.Z < 0.85f)
                {
                    held.Clear();
                    settled = settled != 0 ? Math.Max(settled, now + Settle) : 0;
                }
                heading = (dx, dz);
                var (fx, fz) = forward!.Value;
                float up = dx * fx + dz * fz, right = dx * fz - dz * fx;
                tilt = reach < FineReach && index == path.Count - 1 && distance - reach < SlowWithin ? SlowTilt : 1;
                tilt = Math.Min(tilt, limit);
                touch.Hold((int)(cx + right * radius * tilt), (int)(cy - up * radius * tilt));
                Thread.Sleep(TimeSpan.FromSeconds(Tick));
            }
        }
        finally
        {
            try
            {
                touch.Release();
            }
            catch (DeviceError e)
            {
                L.Swallowed("nhấc ngón", e);
            }
            nav.Save();
        }
    }
}
