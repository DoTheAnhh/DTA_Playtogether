using DTA.Engine.Bots;
using DTA.Engine.Gather;
using DTA.Engine.Movement;
using DTA.Game.Data;
using DTA.Game.Geometry;
using DTA.Runtime.Device;

namespace DTA.Features.Insect;

/// <summary>
/// Bắt bọ tự động (100% gọi hàm game OnClickInsectCollecting): chọn con theo điểm (loài × cấp nền / khoảng cách, phạt con đang nghi) có
/// chống dao động; đi bộ thì rình theo ngưỡng cảnh giác của từng con, dịch chuyển thì tới chỗ đứng thoáng có mặt đất thật mà con bọ nằm
/// trong vùng vợt quét. Bảng kết quả xử lý ĐÚNG 1 LẦN ở vòng chung.
/// </summary>
public sealed partial class InsectBot(EmulatorDevice device, BotEvents events, GatherEvents gather, InsectOptions options)
    : GatherBot<Bug>(device, events, gather, options)
{
    protected override string Channel => "insect";
    private const float SwingMax = 1.85f, SwingOptimal = 1.45f;
    private const double BlacklistTtl = 30, SwingTimeout = 2.5;
    private const int MaxMisses = 3;
    private static readonly Dictionary<string, float> SpeciesWeights = new()
    {
        [BugKinds.GemBox] = 5, [BugKinds.StarBox] = 4, [BugKinds.Card] = 3, [BugKinds.Bird] = 2, [BugKinds.Insect] = 1.5f, [BugKinds.Other] = 1,
    };
    private InsectGame _game = null!;
    private int? _current;
    private readonly Dictionary<int, double> _blacklist = [];
    private readonly Dictionary<int, int> _misses = [];
    /// <summary>Chỗ đậu đã tra độ cao: uid -> (ô 0,5 m, trong tầm vợt).</summary>
    private readonly Dictionary<int, ((int, int, int) Spot, bool Ok)> _perches = [];
    /// <summary>Đang giữ nút vợt (giơ vợt rình).</summary>
    private bool _netUp;
    private InsectOptions Bugs => (InsectOptions)Options;
    private FreezeService Freeze => FreezeService.For(Device.Serial);

    protected override string ToolName => "vợt";
    protected override string ToolClass => Tools.Net;
    protected override string Broken => "Vợt đã hỏng và không bật tự sửa";
    protected override string Place => "côn trùng";

    protected override void Prepare()
    {
        _game = new InsectGame(Session);
        Session.SceneChanged -= _game.SceneChanged;
        Session.SceneChanged += _game.SceneChanged;
        _game.State();
        if (Session.Player.Facing() == null) return;
        _game.Bugs();
        Session.Camera.Direction();
    }

    /// <summary>Vòng làm việc chung; dừng giữa lúc đang giơ vợt thì hạ vợt (không vung).</summary>
    protected override string? Work()
    {
        _netUp = false;
        try
        {
            return base.Work();
        }
        finally
        {
            Session.Optional(() => { LowerNet(); return true; }, "hạ vợt");
        }
    }

    protected override Phase CurrentPhase() => _game.State() switch
    {
        NetState.None => Phase.Idle,
        NetState.Boast => Phase.Reward,
        _ => Phase.Busy,
    };

    protected override List<Bug> Targets()
    {
        foreach (var uid in _blacklist.Where(p => Now >= p.Value).Select(p => p.Key).ToList()) _blacklist.Remove(uid);
        var bugs = _game.Bugs();
        if (_perches.Count > bugs.Count * 2 + 16)
        {
            var alive = bugs.Select(b => b.Uid).ToHashSet();
            foreach (var uid in _perches.Keys.Where(u => !alive.Contains(u)).ToList()) _perches.Remove(uid);
        }
        return bugs;
    }

    protected override bool Wanted(Bug bug) => (Bugs.Kinds.Count == 0 || Bugs.Kinds.Contains(bug.Kind)) && (bug.Grade == 0 || Bugs.Grades.Count == 0 || Bugs.Grades.Contains(bug.Grade)) && InRange(bug)
                                               && (Teleporting || Reachable(bug));
    protected override string Label(Bug bug) => bug.Grade > 0 ? $"{bug.Name} [{bug.Grade}★]" : bug.Name;
    protected override Bug? Refresh(Bug bug) => _game.Reload(bug);
    protected override (float Reach, float Solid) Stand(Bug bug) => (SwingOptimal, 0);

    /// <summary>Bán nhanh trừ con có cấp nền được giữ lại.</summary>
    protected override bool SellResult(long dialog, CatchInfo? info) => Bugs.Sell && !(info is { Grade: > 0 } i && Bugs.KeepGrades.Contains(i.Grade));

    /// <summary>
    /// Điểm = (trọng số loài × cấp nền × phạt nghi) / (khoảng cách + 1); loài người dùng chọn ×1,2. Chỉ đổi khỏi con đang nhắm khi con mới
    /// hơn ≥ 25% (không lắc qua lại giữa 2 con gần nhau).
    /// </summary>
    protected override Bug? Choose(List<Bug> targets, Vec3 position)
    {
        float Score(Bug b) => SpeciesWeights.GetValueOrDefault(b.Kind, 1) * (Bugs.Kinds.Contains(b.Kind) ? 1.2f : 1) * Math.Max(1, b.Grade)
                              * (b.Sense <= SenseState.Sense ? 1 : 0.15f) / (NavMap.Dist(b.X, b.Z, position.X, position.Z) + 1);
        var ranked = targets.Where(b => !_blacklist.ContainsKey(b.Uid) && !Dropped(b) && Wanted(b) && b.State < Bug.Leaving && b.Sense != SenseState.Escape)
            .Select(b => (Score: Score(b), Bug: b)).OrderByDescending(p => p.Score).ToList();
        Gather.Targets(ranked.Count > 0 ? 1 : 0, targets.Count);
        if (ranked.Count == 0) return Pick(null);
        var current = ranked.FirstOrDefault(p => p.Bug.Uid == _current);
        return Pick(current.Bug != null && ranked[0].Score < current.Score * 1.25f ? current.Bug : ranked[0].Bug);
    }

    private Bug? Pick(Bug? bug)
    {
        _current = bug?.Uid;
        return bug;
    }

    /// <summary>Bỏ con này 30 s (bay mất / hụt nhiều).</summary>
    private void Blacklist(Bug bug) => _blacklist[bug.Uid] = Now + BlacklistTtl;
}
