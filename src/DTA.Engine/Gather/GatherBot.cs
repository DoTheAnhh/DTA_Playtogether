using DTA.Engine.Bots;
using DTA.Engine.Core;
using DTA.Engine.Movement;
using DTA.Game.Geometry;
using DTA.Runtime.Core;
using DTA.Runtime.Device;

namespace DTA.Engine.Gather;

/// <summary>
/// Khung chung "tự đi tới từng vật trên bản đồ rồi thu hoạch" (đào cổ vật, đập đá, nhặt đồ, bắt bọ). Lớp con khai báo vật đang có,
/// vật nào đáng tới, đứng thế nào, thu hoạch 1 vật; phần chung: chọn vật theo quãng ĐI BỘ ngắn nhất - đi tới - thu hoạch - xử lý bảng
/// kết quả - ghi lịch sử - sửa dụng cụ - đổi bản đồ khi hết vật.
/// </summary>
public abstract partial class GatherBot<T>(EmulatorDevice device, BotEvents events, GatherEvents gather, GatherOptions options)
    : Bot(device, events) where T : class, IGatherTarget
{
    private const double RetryAfter = 180, RetryMax = 1800, SwitchEvery = 1, HopBack = 150;
    private const float SwitchGain = 3, Detour = 2.5f, DetourExtra = 30;
    private const int StuckLimit = 3, MissLimit = 4;

    protected GatherOptions Options { get; } = options;
    protected GatherEvents Gather { get; } = gather;
    protected Walker Walk { get; private set; } = null!;
    /// <summary>Tâm phạm vi: chỗ đứng lúc Bật (hoặc lúc vừa đổi phạm vi).</summary>
    protected Vec3 Origin { get; private set; }
    /// <summary>Lúc vòng lặp vừa xác nhận đứng sát + quay mặt vào vật (thu hoạch nhát đầu ngay, khỏi soát lại).</summary>
    protected double ReadyAt { get; set; }
    /// <summary>Lúc bấm cú cuối mà game còn có thể đang trả kết quả.</summary>
    protected double LastAction { get; set; }
    /// <summary>Tên vật vừa thu hoạch (chờ ghi cùng món nhận được).</summary>
    protected string Source { get; set; } = "";
    private bool _walkedIn;
    /// <summary>Vật đang tiếp cận dở + số lượt liền không tới được (dừng / bị chặn) - quá <see cref="MissLimit"/> thì bỏ, tránh lặp mãi 1 vật.</summary>
    private (int Uid, int Count) _misses;
    private readonly Dictionary<int, (double Until, int Count)> _dropped = [];

    /// <summary>Câu báo khi dụng cụ hỏng mà không bật tự sửa.</summary>
    protected abstract string Broken { get; }
    /// <summary>Cách gọi 1 vật ("điểm đào", "mạch đá").</summary>
    protected virtual string Place => "vật";

    protected static double Now => Environment.TickCount64 / 1000.0;

    /// <summary>Các vật đang có trên bản đồ.</summary>
    protected abstract List<T> Targets();
    /// <summary>Đọc lại vật đang trên đường tới; null nếu hết đáng tới (biến mất, người khác lấy).</summary>
    protected abstract T? Refresh(T target);
    /// <summary>Tên vật cho người dùng.</summary>
    protected abstract string Label(T target);
    /// <summary>(đi tới khi cách tâm vật bao nhiêu m thì dừng, bị chính vật chặn trong vòng bao nhiêu m cũng coi là tới).</summary>
    protected abstract (float Reach, float Solid) Stand(T target);
    /// <summary>Thu hoạch vật đang đứng cạnh; false = bỏ vật này.</summary>
    protected abstract bool Harvest(T target);
    /// <summary>Bước thu hoạch hiện tại (đọc game).</summary>
    protected abstract Phase CurrentPhase();

    /// <summary>Có nên tới vật này (mặc định: trong phạm vi).</summary>
    protected virtual bool Wanted(T target) => InRange(target);
    /// <summary>Số nhỏ tới trước; bằng thì gần trước.</summary>
    protected virtual int Priority(T target) => 0;
    /// <summary>Tới vật này có cần dụng cụ không.</summary>
    protected virtual bool NeedsTool(T target) => true;
    /// <summary>Đứng ngay đây là thu hoạch được rồi (khỏi nhích lại gần).</summary>
    protected virtual bool Ready(T target) => false;
    /// <summary>Vật có thân chắn nên đi vòng trên đường tới <paramref name="target"/>: (x, z, bán kính).</summary>
    protected virtual IEnumerable<(float X, float Z, float R)> Obstacles(T target, List<T> all) => [];
    /// <summary>Việc với dụng cụ trước khi đi thu hoạch (căn vợt...); chuỗi = lỗi dừng hẳn.</summary>
    protected virtual string? ReadyTool() => null;
    /// <summary>Món trong bảng kết quả: true = bán nhanh, false = bảo quản.</summary>
    protected virtual bool SellResult(long dialog, DTA.Game.Data.CatchInfo? info) => false;

    /// <summary>Chọn vật gần nhất theo đường thẳng (dịch chuyển / bản đồ nhỏ) thay vì tính quãng đi bộ.</summary>
    protected virtual bool StraightChoice => Teleporting;

    /// <summary>Đang dùng chế độ dịch chuyển (đã chọn + đã xác nhận rủi ro).</summary>
    protected bool Teleporting => Options.Move == MoveMode.Teleport && Risk.IsAccepted(RiskFeature.Teleport);

    protected bool InRange(T target) => Options.Radius == 0 || NavMap.Dist(target.X, target.Z, Origin.X, Origin.Z) <= Options.Radius;

    /// <summary>Địa hình nạp sẵn lúc kết nối ở nền.</summary>
    protected override void Preload() => MapStore.Preload(Session);

    /// <summary>Thu hoạch hết vật này tới vật khác tới khi bị tắt.</summary>
    protected override string? Work()
    {
        if (!Touch.CanHold) return "Giả lập này không cho giữ / kéo trên màn hình nên nhân vật không tự đi được (màn hình đang xoay dọc?)";
        Walk = new Walker(Session, Touch, () => Running);
        int trapped = 0, radius = -1;
        int? home = null;
        var waiting = false;
        double idle = 0, checkedAt = 0;
        var left = new Dictionary<int, double>();
        (int Remaining, int Limit)? tool = null;
        (int Id, string Name)? here = null;
        T? current = null;
        Source = "";
        _dropped.Clear();
        while (Running)
        {
            if (!AuthSession.Active) return null;
            var slow = Now - checkedAt >= 1;
            if (slow) Session.LocateActor();
            var collectError = Collect();
            if (collectError != null || !Running) return collectError;
            if (slow)
            {
                checkedAt = Now;
                var held = ToolClass.Length > 0 ? Session.Held.Read(ToolClass) : new DTA.Game.Data.HeldState(true, null);
                if (held.Holding == false) return NotEquipped();
                tool = held.Uses;
                Events.Tool(tool);
                here = Session.Camera.Map();
            }
            if (ReadyTool() is { } toolError) return toolError;
            if (!Running) return null;
            var position = Session.Player.Position() ?? throw new GameError("Không đọc được vị trí nhân vật", true);
            if (Options.Radius != radius) (Origin, radius, waiting) = (position, Options.Radius, false);
            home ??= here?.Id;
            if (here is { } h && home is { } hm && h.Id != hm) return $"Nhân vật đã bị chuyển sang bản đồ khác ({h.Name}) - hãy quay lại rồi bật lại";
            var targets = Targets();
            var target = Stay(current, targets);
            var stayed = target != null;
            target ??= Choose(targets, position);
            if (tool is { Remaining: 0 } && !Options.AutoRepair && (target == null || NeedsTool(target))) return Broken;
            if (target == null)
            {
                Gather.Target(null, "");
                idle = idle == 0 ? Now : idle;
                var choices = Options.Hop && here != null && Now - idle >= AppSettings.Current.HopWaitSeconds
                    ? Travel.Reachable(here.Value.Id, Options.Maps).Where(m => Now - left.GetValueOrDefault(m) >= HopBack).OrderBy(m => left.GetValueOrDefault(m)).ToList()
                    : [];
                if (choices.Count > 0)
                {
                    var error = Travel.Go(Session, choices[0], s => Events.Status(s, Level.Info), Cancel);
                    if (!Running) return null;
                    if (error != null)
                    {
                        Events.Status($"{error} - ở lại bản đồ này", Level.Warn);
                        left[choices[0]] = Now;
                    }
                    else
                    {
                        left[here!.Value.Id] = Now;
                        here = Session.Camera.Map();
                        (home, radius, idle, waiting, checkedAt, current) = (here?.Id, -1, 0, false, Now, null);
                        _dropped.Clear();
                        Events.Status($"Đã sang {Travel.Label(home ?? 0)}", Level.Ok);
                    }
                    continue;
                }
                if (!waiting)
                {
                    waiting = true;
                    Events.Status($"Chưa có {Place} nào phù hợp trong phạm vi - chờ...", Level.Quiet);
                }
                Sleep(1);
                continue;
            }
            (waiting, idle, current) = (false, 0, target);
            var ready = Ready(target);
            WalkResult arrived;
            if (stayed || ready)
            {
                arrived = WalkResult.Arrived;
                if (ready) ReadyAt = Now;
            }
            else
            {
                arrived = Approach(target, position, targets);
                _walkedIn = Walk.WalkedIn;
            }
            if (!Running) return null;
            switch (arrived)
            {
                case WalkResult.Stuck:
                    Drop(target);
                    trapped = Walk.Trapped ? trapped + 1 : 0;
                    if (trapped >= StuckLimit) return "Nhân vật bị vây kín, lối ra nào cũng bị chặn - hãy đưa nhân vật ra chỗ trống rồi bật lại";
                    Events.Status($"Không tới được {Place} này - chuyển sang {Place} khác...", Level.Warn);
                    continue;
                case WalkResult.Lost:
                    throw new GameError("Không đọc được vị trí nhân vật hoặc hướng camera", true);
                case not WalkResult.Arrived:
                    _misses = (target.Uid, _misses.Uid == target.Uid ? _misses.Count + 1 : 1);
                    if (_misses.Count >= MissLimit) Drop(target);
                    continue;
            }
            (trapped, _misses) = (0, default);
            if (!Harvest(target)) Drop(target);
        }
        return null;
    }

    /// <summary>Vật vừa thu hoạch 1 nhát mà vẫn còn, vẫn đúng loại, vẫn đứng sát: làm tiếp ngay, khỏi chọn lại.</summary>
    protected virtual T? Stay(T? current, List<T> targets)
    {
        if (current == null || Now < _dropped.GetValueOrDefault(current.Uid).Until) return null;
        var fresh = targets.FirstOrDefault(t => t.Uid == current.Uid);
        return fresh != null && Wanted(fresh) && Ready(fresh) ? fresh : null;
    }

    /// <summary>Bỏ vật 1 thời gian (180 s, mỗi lần hỏng gấp đôi, tối đa 30 phút).</summary>
    private void Drop(T target)
    {
        var count = _dropped.GetValueOrDefault(target.Uid).Count + 1;
        _dropped[target.Uid] = (Now + Math.Min(RetryAfter * Math.Pow(2, count - 1), RetryMax), count);
    }

    protected bool Dropped(T target) => Now < _dropped.GetValueOrDefault(target.Uid).Until;

    /// <summary>
    /// Vật kế tiếp: nhóm ưu tiên nhất, rồi quãng ĐI BỘ ngắn nhất (vòng qua nhà, tường, nước). Lượt đầu chỉ tìm đường vòng vừa phải
    /// (≤ 2,5 lần chim bay + 30 m); vật bị bỏ dở thì tìm tiếp nếu còn có thể ngắn hơn vật tốt nhất.
    /// </summary>
    protected virtual T? Choose(List<T> targets, Vec3 position)
    {
        var candidates = targets.Where(t => !Dropped(t) && Wanted(t)).ToList();
        Gather.Targets(candidates.Count, targets.Count);
        if (candidates.Count == 0) return null;
        if (StraightChoice) return candidates.OrderBy(Priority).ThenBy(t => NavMap.Dist(t.X, t.Z, position.X, position.Z)).First();
        var first = candidates.Min(Priority);
        var ranked = candidates.Where(t => Priority(t) == first).Select(t => (Straight: NavMap.Dist(t.X, t.Z, position.X, position.Z), Target: t)).OrderBy(p => p.Straight).ToList();
        T? best = null;
        var shortest = float.PositiveInfinity;
        var skipped = new List<(float Limit, T Target, float Accept)>();
        foreach (var (straight, target) in ranked)
        {
            var (reach, solid) = Stand(target);
            var accept = Math.Max(reach, solid);
            if (straight - accept >= shortest) break;
            var limit = Math.Min(shortest, straight * Detour + DetourExtra);
            var cost = Walk.Cost(position, (target.X, target.Z), accept, Obstacles(target, targets), limit);
            if (cost is { } c && c < shortest) (best, shortest) = (target, c);
            else if (cost == null && limit < shortest) skipped.Add((limit, target, accept));
        }
        foreach (var (limit, target, accept) in skipped.Where(s => s.Limit < shortest))
            if (Walk.Cost(position, (target.X, target.Z), accept, Obstacles(target, targets), shortest) is { } c && c < shortest) (best, shortest) = (target, c);
        return best ?? ranked[0].Target;
    }
}
