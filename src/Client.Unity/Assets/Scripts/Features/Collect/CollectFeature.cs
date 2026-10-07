using DTA.Core.ActionDispatcher;
using DTA.Features.Base;
using DTA.Shared.Enums;
using DTA.Shared.Models;

namespace DTA.Features.Collect;

public sealed class FieldObject
{
    public uint Uid { get; init; }
    public string Name { get; init; } = string.Empty;
    public Vector3 Position { get; init; }
}

public sealed class CollectOptions
{
    public float Radius { get; set; } = 40f;
    public MoveMode MoveMode { get; set; } = MoveMode.Walk;
}

public sealed class CollectStats
{
    public int TotalCollected { get; set; }
}

public interface ICollectService : IFeatureService
{
    CollectOptions Options { get; }
    CollectStats Stats { get; }
    ValueTask<FeatureResult> PickObjectAsync(FieldObject obj, CancellationToken ct = default);
}

public sealed class CollectService : BaseFeatureService<CollectOptions, CollectStats>, ICollectService
{
    public override FeatureId Id => FeatureId.Collect;
    public override string Name => "Thu thập";

    public CollectService(IGameActionDispatcher dispatcher) : base(dispatcher) { }

    public override ValueTask<FeatureResult> StartAsync(CancellationToken ct = default)
    {
        IsRunning = true;
        return ValueTask.FromResult(FeatureResult.Ok("Collect bot started"));
    }

    public override ValueTask<FeatureResult> StopAsync(CancellationToken ct = default)
    {
        IsRunning = false;
        return ValueTask.FromResult(FeatureResult.Ok("Collect bot stopped"));
    }

    public async ValueTask<FeatureResult> PickObjectAsync(FieldObject obj, CancellationToken ct = default)
    {
        var action = new GameAction
        {
            Type = GameActionType.PickObject,
            VectorParam = obj.Position,
            IntParam = (int)obj.Uid,
            Priority = ActionPriority.High,
            DeduplicationKey = $"pick_{obj.Uid}"
        };

        var res = await Dispatcher.EnqueueAsync(action, ct);
        if (res.Success)
        {
            Stats.TotalCollected++;
            return FeatureResult.Ok($"Đã nhặt vật phẩm {obj.Name}");
        }
        return FeatureResult.Fail(res.Message);
    }
}
