using DTA.Engine.Bots;
using DTA.Engine.Movement;
using DTA.Game.Actor;
using DTA.Game.Geometry;

namespace DTA.Features.Insect;

/// <summary>Phần tiếp cận bọ của <see cref="InsectBot"/>.</summary>
public sealed partial class InsectBot
{
    private const float RunSpeed = 4.5f, MinSneak = 0.35f, MaxSneak = 1.5f, SneakSafety = 0.7f, AlertMargin = 0.8f;
    /// <summary>Giơ vợt (giữ nút) khi cách bọ ≤ vòng cảnh giác (tối thiểu 2 m) + 0,8 m + 1 m; </summary>
    private const float HoldLead = 1f, MinRadius = 2f;
    /// <summary>Vùng vợt của game: bán kính 0,48 quanh tâm cách chân 1,86 m (lấy 0,4 cho chắc); dừng xa thêm 0,1 m bù trớn.</summary>
    private const float CatchSure = 0.4f, Overshoot = 0.1f, BackReach = 0.2f;
    private const float ZoneReach = 1.86f, ZoneLow = -0.28f, ZoneHigh = 1.38f, StandClear = 0.5f, StandEdge = 0.9f;
    private const double SenseEvery = 0.15, AlertWait = 2.5, AlertCreep = 1.0, HoldRetry = 1.5;

    protected override WalkResult Approach(Bug bug, Vec3 position, List<Bug> targets) => Teleporting ? TeleportToBug(bug) : Sneak(bug);

    /// <summary>
    /// Đi bộ rình trong 1 lượt giữ cần liền mạch, bám vị trí MỚI NHẤT của con bọ (nó dời quá 0,4 m thì walker đổi đích ngay, ngón không nhấc
    /// - không táp táp cần): ngoài vòng cảnh giác chạy hết tốc; chỉ khi đã sát vòng cảnh giác (+0,8 m, thêm 1 m giơ sẵn) mới chậm lại
    /// (≤ 1,5 m/s) rồi đè giữ nút vợt 1 lần (giơ vợt đi rón rén như người chơi; game bỏ qua lệnh giữ lúc đang chạy nên chỉ đè khi đã chậm,
    /// soát lại cờ, thử lại thưa mỗi 1,5 s), đi dưới 70% ngưỡng an toàn của con đó; con đang nghi thì đứng im (tối đa 2,5 s), vẫn nghi thì
    /// nhích chậm 1 s rồi chờ lại (đang đóng băng thì không chờ: con bọ chớp nghi giữa 2 lượt ép mà không chạy được); nó bỏ chạy / biến mất
    /// thì bỏ 30 s; con đậu cao ngoài vùng vợt thì bỏ luôn (vung chắc hụt); không tới được thì hạ vợt không vung. Chỉ báo tới khi con bọ
    /// thật sự trong tầm vung. Mức cảnh giác soát mỗi 0,15 s (1 lượt đọc nhẹ).
    /// </summary>
    private WalkResult Sneak(Bug bug)
    {
        var name = Label(bug);
        Events.Status($"Đang tiếp cận {name}...", Level.Info);
        var fresh = bug;
        double sensed = 0, alertSince = 0, held = 0;
        var gone = false;

        (float, float)? Pace(Motion motion, float _)
        {
            if (Now - sensed >= SenseEvery)
            {
                sensed = Now;
                var reloaded = _game.Reload(bug);
                if (reloaded == null || reloaded.Sense == SenseState.Escape)
                {
                    gone = true;
                    return null;
                }
                fresh = reloaded;
            }
            var distance = NavMap.Dist(fresh.X, fresh.Z, motion.X, motion.Z);
            Gather.Target(distance, name);
            var close = distance <= Math.Max(fresh.Radius, MinRadius) + AlertMargin + HoldLead;
            if (close && !_netUp && Now - held >= HoldRetry && MathF.Sqrt(motion.Vx * motion.Vx + motion.Vz * motion.Vz) <= MaxSneak + 0.3f)
            {
                held = Now;
                _netUp = Session.Tools.HoldNet() && Session.Tools.NetHeld() == true;
            }
            if (fresh.Sense == SenseState.Alert && !Freeze.Enabled)
            {
                alertSince = alertSince == 0 || Now - alertSince >= AlertWait + AlertCreep ? Now : alertSince;
                return Now - alertSince < AlertWait ? (0f, 0f) : (MinSneak / RunSpeed, MinSneak);
            }
            alertSince = 0;
            var speed = close ? Math.Min(SafeSpeed(fresh, distance), MaxSneak) : SafeSpeed(fresh, distance);
            return (Math.Min(1, speed / RunSpeed), speed);
        }

        var result = Walk.Go((bug.X, bug.Z), ZoneReach + Overshoot, pace: Pace, follow: () => (fresh.X, fresh.Z));
        if (!gone && Running && result is WalkResult.Arrived or WalkResult.Stopped) result = Aim(bug) ? WalkResult.Arrived : WalkResult.Stopped;
        if (result != WalkResult.Arrived) LowerNet();
        if (!gone) return result;
        Events.Status($"{name} đã bay mất - chuyển con khác...", Level.Warn);
        Blacklist(bug);
        return WalkResult.Stopped;
    }

    /// <summary>
    /// Con bọ nằm trong khoảng cao vùng vợt tính từ mặt đất dưới nó (tra lưới cục bộ, không tốn lượt đọc game); nhớ theo con + chỗ đậu
    /// (làm tròn 0,5 m) để chọn mục tiêu không tra lại mỗi nhịp. Con đậu cao (chim trên mái...) vung chắc hụt nên không chọn.
    /// </summary>
    private bool Reachable(Bug bug)
    {
        var spot = ((int)MathF.Round(bug.X * 2), (int)MathF.Round(bug.Y * 2), (int)MathF.Round(bug.Z * 2));
        if (_perches.TryGetValue(bug.Uid, out var known) && known.Spot == spot) return known.Ok;
        var ok = Session.Ground.Resolve(bug.X, bug.Z, bug.Y).Y is not { } floor || bug.Y - floor is >= ZoneLow and <= ZoneHigh;
        _perches[bug.Uid] = (spot, ok);
        return ok;
    }

    /// <summary>
    /// Ngắm: con bọ đã vào vùng vợt thì xong; quá sát (&lt; 1,46 m) thì lùi ra đúng 1,86 m (1 lần đè cần); đứng đúng khoảng mà lệch hướng thì
    /// quay mặt tại chỗ 1 lần rồi soát lại. Xa quá thì để lượt tiếp cận sau.
    /// </summary>
    private bool Aim(Bug bug)
    {
        if (_game.Reload(bug) is not { } now) return false;
        if (InCatch(now)) return true;
        if (Session.Player.Position() is not { } p) return false;
        var distance = NavMap.Dist(now.X, now.Z, p.X, p.Z);
        if (distance > ZoneReach + CatchSure) return false;
        if (distance < ZoneReach - CatchSure)
        {
            var (ax, az) = distance > 0.05f ? ((p.X - now.X) / distance, (p.Z - now.Z) / distance) : (1f, 0f);
            if (Walk.Go((now.X + ax * ZoneReach, now.Z + az * ZoneReach), BackReach) != WalkResult.Arrived) return false;
        }
        return Walk.Face((now.X, now.Z)) && _game.Reload(bug) is { } after && InCatch(after);
    }

    /// <summary>
    /// Con bọ nằm trong vùng vợt thật của game (InsectSystem._catchCapsule): đoạn thẳng đứng cách chân 1,86 m về phía mặt, cao 0,2..0,9 m,
    /// bán kính 0,48 m -> ngang lấy biên an toàn 0,4 m quanh tâm, dọc -0,28..1,38 m so với chân. Ngoài vùng này vung là hụt chắc.
    /// </summary>
    private bool InCatch(Bug bug)
    {
        if (Session.Player.Facing() is not var (p, f)) return false;
        float cx = p.X + f.X * ZoneReach, cz = p.Z + f.Z * ZoneReach;
        return NavMap.Dist(bug.X, bug.Z, cx, cz) <= CatchSure && bug.Y - p.Y is >= ZoneLow and <= ZoneHigh;
    }

    /// <summary>Hạ vợt đang giữ mà không vung (bỏ con đang rình).</summary>
    private void LowerNet()
    {
        if (!_netUp) return;
        _netUp = false;
        Session.Tools.LowerNet();
    }

    /// <summary>Tốc độ an toàn: không cảnh giác / ngoài vòng = chạy; trong vòng = 70% ngưỡng an toàn (0,35..1,5 m/s).</summary>
    private static float SafeSpeed(Bug bug, float distance)
    {
        if (bug.Radius <= 0 || distance > bug.Radius + AlertMargin) return RunSpeed;
        return bug.Calm > 0 ? Math.Clamp(bug.Calm * SneakSafety, MinSneak, MaxSneak) : MinSneak;
    }

    /// <summary>
    /// Dịch chuyển tới chỗ đứng vung trúng (xem <see cref="StandSpot"/>), mặt quay vào con bọ, không xoay camera; độ cao lấy từ mặt đất thật
    /// (chim / thẻ bay lơ lửng - lấy độ cao con bọ là đặt nhân vật giữa không trung). Bật đóng băng thì đóng con này trước khi đáp.
    /// </summary>
    private WalkResult TeleportToBug(Bug bug)
    {
        var name = Label(bug);
        Events.Status($"Dịch chuyển tới {name}...", Level.Info);
        if (Freeze.Enabled) Freeze.Freeze(bug.Ref);
        var fresh = _game.Reload(bug) ?? bug;
        if (Session.Player.Position() is not { } here) return WalkResult.Lost;
        if (StandSpot(fresh, here) is not { } spot)
        {
            Events.Status($"Không tìm được chỗ đứng có mặt đất cạnh {name}", Level.Warn);
            Blacklist(bug);
            return WalkResult.Stuck;
        }
        var result = Session.Teleport.Go(new TeleportRequest(spot, Rotation: Quat.LookAt(spot, new Vec3(fresh.X, fresh.Y, fresh.Z)), Tolerance: 1), cancel: Cancel);
        if (Freeze.Enabled) Freeze.Refreeze(bug.Ref);
        if (!result.Ok || result.Position is not { } actual)
        {
            Events.Status($"Dịch chuyển tới {name} không thành công ({result.Status})", Level.Warn);
            Blacklist(bug);
            return WalkResult.Stuck;
        }
        var distance = NavMap.Dist(actual.X, actual.Z, fresh.X, fresh.Z);
        Gather.Target(distance, name);
        return distance <= SwingMax + 0.35f ? WalkResult.Arrived : WalkResult.Stuck;
    }

    /// <summary>
    /// Chỗ đứng vung trúng: 24 hướng × 3 khoảng cách quanh con bọ (bọ phải rơi vào vùng vợt: cách chân 1,86 m), chấm điểm: mặt đất thật +
    /// bọ trong khoảng cao vùng vợt; thoáng quanh thân (8 điểm 0,5 m cùng tầng - không dính tường); không sát mép nước / vực (8 điểm 0,9 m có
    /// lưới); không bị chắn giữa chỗ đứng và bọ; ưu tiên hướng phía người chơi. Tra cục bộ trên lưới đã nạp, không tốn lượt đọc game.
    /// </summary>
    private Vec3? StandSpot(Bug bug, Vec3 player)
    {
        var standTele = ZoneReach - 0.15f;
        float dx = player.X - bug.X, dz = player.Z - bug.Z;
        var baseAngle = MathF.Sqrt(dx * dx + dz * dz) > 0.1f ? MathF.Atan2(dz, dx) : 0;
        var cache = new Dictionary<(int, int), DTA.Game.World.Surface>();
        DTA.Game.World.Surface Ground(float x, float z, float near)
        {
            var key = ((int)MathF.Round(x * 100), (int)MathF.Round(z * 100));
            if (!cache.TryGetValue(key, out var s)) cache[key] = s = Session.Ground.Resolve(x, z, near);
            return s;
        }
        bool SameFloor(float x, float z, float floor, float step) => Ground(x, z, floor) is { Y: { } y, Kind: "navmesh_detail" } && Math.Abs(y - floor) <= step;
        Vec3? best = null;
        var bestScore = float.PositiveInfinity;
        for (var k = 0; k < 24 && bestScore >= 0.05f; k++)
        {
            var turn = 15 * ((k + 1) / 2) * (k % 2 == 1 ? 1 : -1);
            var angle = baseAngle + turn * MathF.PI / 180;
            foreach (var distance in new[] { standTele, standTele - 0.21f, standTele + 0.24f })
            {
                float tx = bug.X + distance * MathF.Cos(angle), tz = bug.Z + distance * MathF.Sin(angle);
                var surface = Ground(tx, tz, bug.Y);
                if (surface.Y is not { } floor) continue;
                var rel = bug.Y - floor;
                var score = 3 * Math.Max(0, Math.Max(ZoneLow + 0.05f - rel, rel - (ZoneHigh - 0.1f)));
                if (surface.Kind != "navmesh_detail") score += 2;
                score += 0.01f * Math.Abs(turn) + 0.4f * Math.Abs(distance - standTele);
                if (score >= bestScore) continue;
                for (var i = 0; i < 8; i++) if (!SameFloor(tx + StandClear * MathF.Cos(i * MathF.PI / 4), tz + StandClear * MathF.Sin(i * MathF.PI / 4), floor, 0.35f)) score += 0.35f;
                for (var i = 0; i < 8; i++) if (!SameFloor(tx + StandEdge * MathF.Cos(i * MathF.PI / 4 + MathF.PI / 8), tz + StandEdge * MathF.Sin(i * MathF.PI / 4 + MathF.PI / 8), floor, 0.5f)) score += 0.15f;
                if (score >= bestScore) continue;
                foreach (var f in new[] { 0.3f, 0.55f, 0.8f }) if (!SameFloor(tx + (bug.X - tx) * f, tz + (bug.Z - tz) * f, floor, 0.6f)) score += 0.6f;
                if (score < bestScore) (best, bestScore) = (new Vec3(tx, floor, tz), score);
            }
        }
        return best;
    }

    /// <summary>
    /// Chỉ vung khi con bọ đã trong vùng vợt thật của game (chưa thì ngắm lại, 3 lần không vào thì bỏ). Vung vợt (đang giữ nút thì thả = vung, như người chơi; chưa giữ thì hàm vung thường - không đè-thả chớp nhoáng) rồi theo dõi _insectState tới khi biết kết quả. Bắt được thì trả ngay (bảng kết quả do vòng chung xử lý 1 lần). Hụt: bọ chạy
    /// mất / hụt 3 lần thì bỏ 30 s, không thì ngắm lại.
    /// </summary>
    protected override bool Harvest(Bug bug)
    {
        var name = Label(bug);
        Source = bug.Name;
        if (!(_game.Reload(bug) is { } aimed && InCatch(aimed)) && !Aim(bug))
        {
            var tries = _misses[bug.Uid] = _misses.GetValueOrDefault(bug.Uid) + 1;
            if (tries < MaxMisses) return true;
            Events.Status($"{name} không vào được tầm vung chắc trúng - tạm bỏ qua...", Level.Warn);
            LowerNet();
            Blacklist(bug);
            return false;
        }
        Events.Status($"Đang vung vợt bắt {name}...", Level.Ok);
        if (Freeze.Enabled) Freeze.Spare(bug.Ref, TimeSpan.FromSeconds(SwingTimeout + 1.5));
        if (_game.Reload(bug) is not { Sense: not SenseState.Escape })
        {
            Events.Status($"{name} đã bay mất trước khi vung vợt!", Level.Warn);
            LowerNet();
            Blacklist(bug);
            return false;
        }
        if (_netUp && Session.Tools.NetHeld() == true) Session.Tools.ReleaseNet();
        else Session.Tools.SwingNet();
        _netUp = false;
        LastAction = Now;
        var deadline = LastAction + SwingTimeout;
        bool swung = false, caught = false;
        while (Running && Now < deadline)
        {
            var state = _game.State();
            if (state is NetState.CatchSuccess or NetState.Boast)
            {
                caught = true;
                break;
            }
            if (state is NetState.SwingFail or NetState.CatchFail) break;
            if (state != NetState.None) swung = true;
            else if (swung)
            {
                caught = Session.Open.Top().Addr != 0;
                break;
            }
            Thread.Sleep(5);
        }
        if (caught)
        {
            _misses.Remove(bug.Uid);
            LastAction = Now;
            Freeze.Release(bug.Ref);
            L.Info($"Bắt được {name}");
            return true;
        }
        var misses = _misses[bug.Uid] = _misses.GetValueOrDefault(bug.Uid) + 1;
        if (_game.Reload(bug) is not { Sense: not SenseState.Escape })
        {
            Events.Status($"{name} đã trốn thoát - chuyển sang con khác...", Level.Warn);
            Blacklist(bug);
            return false;
        }
        if (misses >= MaxMisses)
        {
            Events.Status($"Vung hụt {name} {misses} lần - tạm bỏ qua...", Level.Warn);
            Blacklist(bug);
            return false;
        }
        Events.Status($"Vung hụt {name} (lần {misses}) - đang ngắm lại...", Level.Warn);
        return true;
    }
}
