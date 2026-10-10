using DTA.Engine.Bots;
using DTA.Engine.Gather;
using DTA.Engine.Movement;
using DTA.Runtime.Device;

namespace DTA.Features.Collect;

/// <summary>
/// Nhặt đồ tự động: bấm bong bóng bàn tay của vật (như chạm tay) hoặc OnPickFieldObject(uid); bỏ điểm kẹt tường vĩnh viễn, vật không phản
/// hồi / bị người khác nhặt bỏ 60 s; chỉ tới vật có mặt đất NavMesh đứng được quanh nó (đi bộ).
/// </summary>
public sealed class CollectBot(EmulatorDevice device, BotEvents events, GatherEvents gather, CollectOptions options)
    : GatherBot<Item>(device, events, gather, options)
{
    protected override string Channel => "collect";
    private const float PickDistance = 0.7f, Closer = 0.35f, Footing = 1.2f, LevelMax = 1f;
    private const double ButtonWait = 2, PickWait = 1, PickTime = 5, Unreachable = 60;
    private const int PickTries = 3;
    private readonly Dictionary<int, double> _ignored = [];
    private CollectOptions Collect => (CollectOptions)Options;

    protected override string Broken => "";
    protected override string Place => "vật";

    protected override void Prepare()
    {
        CollectReader.State(Session);
        if (Session.Player.Facing() == null) return;
        CollectReader.Items(Session);
        Session.Camera.Direction();
        Session.Ui.Joystick();
    }

    /// <summary>Nhặt xong là bảng kết quả mở ngay: từ Boasting trở đi coi như rảnh tay.</summary>
    protected override Phase CurrentPhase() => CollectReader.State(Session) is > CollectState.Idle and < CollectState.Boasting ? Phase.Busy : Phase.Idle;

    /// <summary>Vật trên bản đồ, bỏ điểm kẹt vĩnh viễn + vật đang tạm bỏ.</summary>
    protected override List<Item> Targets()
    {
        foreach (var uid in _ignored.Where(p => Now >= p.Value).Select(p => p.Key).ToList()) _ignored.Remove(uid);
        var mapId = Session.Camera.Map()?.Id ?? 0;
        return CollectReader.Items(Session).Where(i => !_ignored.ContainsKey(i.Uid) && !CollectReader.Ignored.Any(s => s.Matches(mapId, i))).ToList();
    }

    protected override bool NeedsTool(Item item) => false;
    protected override bool Wanted(Item item) => (Collect.Kinds.Count == 0 || Collect.Kinds.Contains(item.Kind)) && InRange(item) && (Teleporting || HasFooting(item));
    protected override string Label(Item item) => item.Name;
    protected override Item? Refresh(Item item) => CollectReader.Items(Session).FirstOrDefault(i => i.Uid == item.Uid);
    protected override (float Reach, float Solid) Stand(Item item) => (PickDistance, 0);

    /// <summary>Có mặt đất game đứng được (lệch cao ≤ 1 m) trong vòng 1,2 m quanh vật không (không có địa hình thì coi như có).</summary>
    private bool HasFooting(Item item)
    {
        if (Walk?.Nav().Terrain is not { } terrain) return true;
        var (low, high) = (NavMap.Cell(item.X - Footing, item.Z - Footing), NavMap.Cell(item.X + Footing, item.Z + Footing));
        for (var i = low.I; i <= high.I; i++)
        for (var j = low.J; j <= high.J; j++)
        {
            var (cx, cz) = NavMap.Center((i, j));
            if (NavMap.Dist(cx, cz, item.X, item.Z) <= Footing && terrain.Ground.TryGetValue((i, j), out var levels) && levels.Any(l => Math.Abs(l - item.Y) <= LevelMax)) return true;
        }
        return false;
    }

    /// <summary>
    /// Nhặt: gọi nhặt, theo dõi _collectActionState. Thành công / vật biến mất = xong (bảng nhận đồ do vòng chính xử lý); RewardFail = bị người
    /// khác nhặt. Chưa cúi nhặt thì gọi lại (tối đa 3 lần), quá giờ thì nhích lại gần 1 lần.
    /// </summary>
    protected override bool Harvest(Item item)
    {
        Source = item.Name;
        Events.Status($"Đang nhặt {item.Name}...", Level.Ok);
        Session.Tools.Pick(item.Uid, item.Ref);
        int taps = 1;
        double tapped = Now, deadline = Now + ButtonWait, checkedAt = tapped;
        var closer = false;
        while (Running)
        {
            var now = Now;
            var state = CollectReader.State(Session);
            var gone = false;
            if (state == CollectState.Idle && now - checkedAt >= 0.3)
            {
                checkedAt = now;
                gone = Refresh(item) == null;
            }
            if (state is CollectState.RewardSuccess or CollectState.Boasting || gone)
            {
                LastAction = Now;
                L.Info($"Nhặt được {item.Name}");
                return true;
            }
            if (state == CollectState.RewardFail)
            {
                _ignored[item.Uid] = now + Unreachable;
                Events.Status($"{item.Name} đã bị người khác nhặt mất", Level.Warn);
                return false;
            }
            if (state != CollectState.Idle) deadline = Math.Max(deadline, tapped + PickTime);
            else if (now - tapped >= (Teleporting ? 0.35 : PickWait))
            {
                if (taps >= PickTries)
                {
                    _ignored[item.Uid] = now + Unreachable;
                    break;
                }
                Session.Tools.Pick(item.Uid, item.Ref);
                (taps, tapped, deadline) = (taps + 1, now, now + ButtonWait);
            }
            if (now > deadline)
            {
                if (closer || taps >= PickTries) break;
                closer = true;
                var result = Teleporting ? TeleportBeside(item) : Walk.Go((item.X, item.Z), Closer);
                if (Teleporting && result != WalkResult.Arrived) return false;
                if (!Teleporting && result is not (WalkResult.Arrived or WalkResult.Stuck)) return true;
                deadline = Now + ButtonWait;
            }
            Thread.Sleep(10);
        }
        if (!Running) return true;
        (Source, LastAction) = ("", 0);
        Events.Status($"Không nhặt được {item.Name} - chuyển sang {Place} khác...", Level.Warn);
        return false;
    }
}
