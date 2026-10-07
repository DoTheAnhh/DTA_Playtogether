using DTA.Core.ActionDispatcher;
using DTA.Features.Base;
using DTA.Shared.Enums;
using DTA.Shared.Models;

namespace DTA.Features.Farm;

public sealed class FarmOptions
{
    public bool AutoHarvest { get; set; } = true;
    public bool AutoWater { get; set; } = true;
    public bool AutoReplant { get; set; } = true;
    public int TargetSeedId { get; set; } = 1;
}

public sealed class FarmStats
{
    public int TotalHarvested { get; set; }
    public int TotalPlanted { get; set; }
    public int TotalWatered { get; set; }
}

public interface IFarmService : IFeatureService
{
    FarmOptions Options { get; }
    FarmStats Stats { get; }
    ValueTask<FeatureResult> HarvestCropAsync(uint cropSuid, Vector3 position, CancellationToken ct = default);
}

public sealed class FarmService : BaseFeatureService<FarmOptions, FarmStats>, IFarmService
{
    public override FeatureId Id => FeatureId.Farm;
    public override string Name => "Nông trại";

    public FarmService(IGameActionDispatcher dispatcher) : base(dispatcher) { }

    public override ValueTask<FeatureResult> StartAsync(CancellationToken ct = default)
    {
        IsRunning = true;
        return ValueTask.FromResult(FeatureResult.Ok("Farm bot started"));
    }

    public override ValueTask<FeatureResult> StopAsync(CancellationToken ct = default)
    {
        IsRunning = false;
        return ValueTask.FromResult(FeatureResult.Ok("Farm bot stopped"));
    }

    public async ValueTask<FeatureResult> HarvestCropAsync(uint cropSuid, Vector3 position, CancellationToken ct = default)
    {
        var action = new GameAction
        {
            Type = GameActionType.ActionTool,
            VectorParam = position,
            IntParam = (int)cropSuid,
            Priority = ActionPriority.High,
            DeduplicationKey = $"harvest_{cropSuid}"
        };

        var res = await Dispatcher.EnqueueAsync(action, ct);
        if (res.Success)
        {
            Stats.TotalHarvested++;
            return FeatureResult.Ok($"Đã thu hoạch cây trồng #{cropSuid}");
        }
        return FeatureResult.Fail(res.Message);
    }
}
