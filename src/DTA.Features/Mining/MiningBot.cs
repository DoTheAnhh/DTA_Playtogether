using DTA.Engine.Bots;
using DTA.Engine.Gather;
using DTA.Engine.Movement;
using DTA.Game.Data;
using DTA.Runtime.Core;
using DTA.Runtime.Device;

namespace DTA.Features.Mining;

/// <summary>
/// Đập đá tự động 100% gọi hàm game (OnClickPickax): quặng quý trước, đập tới HẾT quặng của mạch đang đập (mạch còn bậc thì ở lại, chờ
/// tối đa 1,2 s cho nó hiện lại khi đang chuyển bậc), đập hụt / không quay mặt vào đá thì lùi ra căn góc rồi tiến lại.
/// </summary>
public sealed class MiningBot(EmulatorDevice device, BotEvents events, GatherEvents gather, MiningOptions options)
    : GatherBot<Rock>(device, events, gather, options)
{
    protected override string Channel => "mining";
    private const float StandNear = 0.95f, StandLarge = 1.8f, Solid = 3f, LargeSolid = 6.5f, Back = 2.2f, LargeBack = 8f, Far = 1.8f, Body = 1.2f, ReadyRange = 1.3f;
    private const double StayWait = 1.2, HitTimeout = 0.6, Unreachable = 60;
    private const int MissLimit = 2, Retries = 3;
    private readonly Dictionary<int, double> _ignored = [];
    private MiningOptions Mining => (MiningOptions)Options;

    protected override string ToolName => "cuốc";
    protected override string ToolClass => Tools.Pickaxe;
    protected override string Broken => "Cuốc đã hỏng - bật Tự sửa cuốc hoặc sửa trong game";
    protected override string Place => "mạch đá";

    protected override void Prepare()
    {
        Session.Held.Read(Tools.Pickaxe);
        MiningReader.State(Session);
        if (Session.Player.Facing() == null) return;
        MiningReader.Rocks(Session);
        Session.Camera.Direction();
    }

    protected override Phase CurrentPhase() => MiningReader.State(Session) switch
    {
        PickaxState.None => Phase.Idle,
        <= PickaxState.Miss => Phase.Busy,
        _ => Phase.Reward,
    };

    /// <summary>Mạch đá trên bản đồ, bỏ các mạch đang bị tạm bỏ qua.</summary>
    protected override List<Rock> Targets()
    {
        foreach (var uid in _ignored.Where(p => Now >= p.Value).Select(p => p.Key).ToList()) _ignored.Remove(uid);
        return MiningReader.Rocks(Session).Where(r => !_ignored.ContainsKey(r.Uid)).ToList();
    }

    protected override bool Wanted(Rock rock) => Mining.Kind switch
    {
        RockKind.Rock when rock.Event => false,
        RockKind.Event when !rock.Event => false,
        _ => InRange(rock),
    };

    protected override int Priority(Rock rock) => rock.Prized ? 0 : 1;
    protected override Rock? Refresh(Rock rock) => MiningReader.Rocks(Session).FirstOrDefault(r => r.Uid == rock.Uid);
    protected override string Label(Rock rock) => rock.Step > 1 ? $"{rock.Name} ({rock.Step}★)" : rock.Name;
    protected override (float Reach, float Solid) Stand(Rock rock) => rock.Large ? (StandLarge, LargeSolid) : (StandNear, Solid);
    protected override IEnumerable<(float X, float Z, float R)> Obstacles(Rock rock, List<Rock> all) => all.Where(r => r.Uid != rock.Uid).Select(r => (r.X, r.Z, Body));

    /// <summary>Đứng sát (≤ 1,3 m) và mặt quay vào mạch đá: đập tiếp tại chỗ.</summary>
    protected override bool Ready(Rock rock)
    {
        if (Session.Player.Facing() is not var (p, f)) return false;
        float dx = rock.X - p.X, dz = rock.Z - p.Z, dist = MathF.Sqrt(dx * dx + dz * dz);
        return dist <= ReadyRange && (dx * f.X + dz * f.Z) / (dist > 0 ? dist : 1) >= 0.5f;
    }

    /// <summary>
    /// Đập 1 lượt tới khi nhả quà: rảnh tay thì vung (lần đầu vung ngay nếu vòng lặp vừa xác nhận đứng đúng); quá 0,6 s không thấy phản hồi
    /// thì soát lại (bảng mở, mạch còn không, có quay mặt vào không). Hụt 2 lần / lệch hướng / xa quá: lùi ra căn góc (tối đa 3 lần rồi bỏ 60 s).
    /// </summary>
    protected override bool Harvest(Rock rock)
    {
        var name = Label(rock);
        Events.Status($"Đang đập {name}...", Level.Info);
        LastAction = Now;
        double pending = 0;
        PickaxState? previous = null;
        int misses = 0, retries = 0;
        var (solid, back) = rock.Large ? (LargeSolid, LargeBack) : (Solid, Back);
        var verified = Now - ReadyAt < 0.3;
        ReadyAt = 0;
        while (Running)
        {
            var now = Now;
            var state = MiningReader.State(Session);
            if (state > PickaxState.Miss)
            {
                LastAction = now;
                L.Info($"Đập trúng {name} ({state})");
                return true;
            }
            if (state != previous && state == PickaxState.Miss) L.Debug($"Đập hụt {name} ({++misses}/{MissLimit})");
            else if (state != previous && state == PickaxState.Pickaxing) misses = 0;
            previous = state;
            if (state == PickaxState.None && verified)
            {
                verified = false;
                Session.Tools.SwingPickaxe();
                pending = LastAction = Now;
            }
            else if (state == PickaxState.None && (pending == 0 || now - pending > HitTimeout))
            {
                if (Session.Open.Top().Addr != 0) return true;
                var fresh = Refresh(rock);
                if (fresh == null || !Wanted(fresh)) return true;
                var (p, f) = Session.Player.Facing() ?? throw new GameError("Không đọc được vị trí nhân vật", true);
                float dx = fresh.X - p.X, dz = fresh.Z - p.Z, distance = MathF.Sqrt(dx * dx + dz * dz);
                Gather.Target(distance, fresh.Name);
                var facing = distance < 0.05f || (dx * f.X + dz * f.Z) / distance >= 0.5f;
                if (misses >= MissLimit || !facing || distance > solid)
                {
                    if (misses >= MissLimit && distance > (fresh.Large ? LargeSolid : Far)) Walk.Obstructed((p.X, p.Z), (fresh.X, fresh.Z));
                    misses = 0;
                    if (++retries > Retries)
                    {
                        LastAction = 0;
                        Events.Status($"Không đập trúng được {fresh.Name} - tạm bỏ qua...", Level.Warn);
                        _ignored[fresh.Uid] = Now + Unreachable;
                        return false;
                    }
                    var result = Realign(fresh, dx, dz, p.Y, back, solid, retries);
                    if (result == WalkResult.Lost) throw new GameError("Không đọc được vị trí nhân vật hoặc hướng camera", true);
                    if (result == WalkResult.Stuck)
                    {
                        LastAction = 0;
                        return false;
                    }
                    if (result != WalkResult.Arrived) return true;
                    continue;
                }
                Session.Tools.SwingPickaxe();
                pending = LastAction = Now;
            }
            Thread.Sleep(5);
        }
        return true;
    }

    /// <summary>Lùi ra 1 điểm trống quanh mạch (mỗi lần xoay thêm 1,3 rad) rồi tiến lại sát mạch.</summary>
    private WalkResult Realign(Rock rock, float dx, float dz, float height, float back, float solid, int retries)
    {
        var angle = MathF.Atan2(-dz, -dx) + (retries - 1) * 1.3f;
        var spot = Walk.OpenSpot((rock.X, rock.Z), back, angle, height);
        var result = Walk.Go(spot, 0.4f);
        return result is WalkResult.Arrived or WalkResult.Stuck ? Walk.Go((rock.X, rock.Z), StandNear, solid: solid) : result;
    }

    /// <summary>
    /// Đập tới HẾT quặng: mạch còn bậc thì ở lại (kể cả khi hoạt ảnh nhận quà đẩy lùi / xoay người); đang chuyển bậc có thể tạm biến khỏi
    /// danh sách - chờ tối đa 1,2 s cho nó hiện lại.
    /// </summary>
    protected override Rock? Stay(Rock? current, List<Rock> targets)
    {
        if (current == null || Dropped(current)) return null;
        var fresh = targets.FirstOrDefault(r => r.Uid == current.Uid);
        var deadline = Now + (current.Step > 1 ? StayWait : 0);
        while (fresh == null && Running && Now < deadline)
        {
            Thread.Sleep(100);
            fresh = Targets().FirstOrDefault(r => r.Uid == current.Uid);
        }
        if (fresh == null || !Wanted(fresh) || Session.Player.Position() is not { } place) return null;
        return NavMap.Dist(fresh.X, fresh.Z, place.X, place.Z) <= (fresh.Large ? LargeSolid : Solid) + 0.5f ? fresh : null;
    }
}
