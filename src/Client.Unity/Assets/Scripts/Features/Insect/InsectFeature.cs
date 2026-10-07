using DTA.Core.ActionDispatcher;
using DTA.Features.Base;
using DTA.Shared.Enums;
using DTA.Shared.Models;

namespace DTA.Features.Insect;

public sealed class BugEntity
{
    public uint Uid { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Kind { get; init; } = "butterfly";
    public int Grade { get; init; } = 1;
    public Vector3 Position { get; set; }
    public Vector3 Velocity { get; set; }
    public float AlertRadius { get; init; } = 3.5f;
    public bool IsAlerted { get; set; }
}

public sealed class InsectOptions
{
    public HashSet<string> Kinds { get; set; } = ["all"];
    public HashSet<int> Grades { get; set; } = [1, 2, 3, 4, 5];
    public HashSet<int> KeepGrades { get; set; } = [4, 5];
    public RewardAction Action { get; set; } = RewardAction.Keep;
    public MoveMode MoveMode { get; set; } = MoveMode.Walk;
    public float Radius { get; set; } = 60f;
    public bool AutoRepair { get; set; } = true;
}

public sealed class InsectStats
{
    public int TotalCaught { get; set; }
    public int TotalKept { get; set; }
    public int TotalSold { get; set; }
    public int NetDurability { get; set; } = 100;
}

public static class InsectAwareness
{
    public static float GetSafeApproachSpeed(BugEntity bug, Vector3 playerPos)
    {
        float dist = Vector3.Distance(bug.Position, playerPos);
        if (dist <= bug.AlertRadius)
            return 1.2f; // Sneak speed
        if (dist <= bug.AlertRadius + 2.0f)
            return 2.5f; // Walk speed
        return 6.0f;     // Run speed
    }

    public static Vector3 PredictIntercept(BugEntity bug, float leadTimeSec = 0.25f)
    {
        return bug.Position + (bug.Velocity * leadTimeSec);
    }
}

public interface IInsectService : IFeatureService
{
    InsectOptions Options { get; }
    InsectStats Stats { get; }
    ValueTask<FeatureResult> CatchBugAsync(BugEntity bug, Vector3 playerPos, CancellationToken ct = default);
}

public sealed class InsectService : BaseFeatureService<InsectOptions, InsectStats>, IInsectService
{
    public override FeatureId Id => FeatureId.Insect;
    public override string Name => "Bắt bọ";

    public InsectService(IGameActionDispatcher dispatcher) : base(dispatcher) { }

    public override ValueTask<FeatureResult> StartAsync(CancellationToken ct = default)
    {
        IsRunning = true;
        return ValueTask.FromResult(FeatureResult.Ok("Insect bot started"));
    }

    public override ValueTask<FeatureResult> StopAsync(CancellationToken ct = default)
    {
        IsRunning = false;
        return ValueTask.FromResult(FeatureResult.Ok("Insect bot stopped"));
    }

    public async ValueTask<FeatureResult> CatchBugAsync(BugEntity bug, Vector3 playerPos, CancellationToken ct = default)
    {
        if (!IsRunning) return FeatureResult.Fail("Service not running");

        var targetPos = InsectAwareness.PredictIntercept(bug);
        float dist = Vector3.Distance(targetPos, playerPos);

        if (dist > 2.8f)
        {
            return FeatureResult.Fail($"Ngoài tầm vợt ({dist:F2}m > 2.8m)");
        }

        var swingAction = new GameAction
        {
            Type = GameActionType.ClickInsect,
            VectorParam = targetPos,
            Priority = ActionPriority.High,
            DeduplicationKey = $"catch_{bug.Uid}"
        };

        var res = await Dispatcher.EnqueueAsync(swingAction, ct);
        if (res.Success)
        {
            Stats.TotalCaught++;
            if (Options.Action == RewardAction.Sell && !Options.KeepGrades.Contains(bug.Grade))
                Stats.TotalSold++;
            else
                Stats.TotalKept++;

            Stats.NetDurability = Math.Max(0, Stats.NetDurability - 1);
            return FeatureResult.Ok($"Đã vung vợt bắt {bug.Name}");
        }
        return FeatureResult.Fail(res.Message);
    }
}
