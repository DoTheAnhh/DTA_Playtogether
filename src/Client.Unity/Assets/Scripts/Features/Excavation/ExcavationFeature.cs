using DTA.Core.ActionDispatcher;
using DTA.Features.Base;
using DTA.Shared.Enums;
using DTA.Shared.Models;

namespace DTA.Features.Excavation;

public sealed class ExcavationSpot
{
    public uint Uid { get; init; }
    public int RelicId { get; init; }
    public string AssetName { get; init; } = string.Empty;
    public Vector3 Position { get; init; }
    public int Hp { get; set; } = 1500;
    public float Range { get; init; } = 1.5f;
}

public sealed class ExcavationOptions
{
    public MoveMode MoveMode { get; set; } = MoveMode.Walk;
    public float Radius { get; set; } = 80f;
    public bool AutoRepair { get; set; } = true;
    public HashSet<int> WantedRelicIds { get; set; } = [];
}

public sealed class ExcavationStats
{
    public int TotalExcavated { get; set; }
    public int TotalDigs { get; set; }
    public int ShovelDurability { get; set; } = 100;
}

public interface IExcavationService : IFeatureService
{
    ExcavationOptions Options { get; }
    ExcavationStats Stats { get; }
    ValueTask<FeatureResult> DigSpotAsync(ExcavationSpot spot, CancellationToken ct = default);
}

public sealed class ExcavationService : BaseFeatureService<ExcavationOptions, ExcavationStats>, IExcavationService
{
    public override FeatureId Id => FeatureId.Excavation;
    public override string Name => "Đào cổ vật";

    public ExcavationService(IGameActionDispatcher dispatcher) : base(dispatcher) { }

    public override ValueTask<FeatureResult> StartAsync(CancellationToken ct = default)
    {
        IsRunning = true;
        return ValueTask.FromResult(FeatureResult.Ok("Excavation bot started"));
    }

    public override ValueTask<FeatureResult> StopAsync(CancellationToken ct = default)
    {
        IsRunning = false;
        return ValueTask.FromResult(FeatureResult.Ok("Excavation bot stopped"));
    }

    public async ValueTask<FeatureResult> DigSpotAsync(ExcavationSpot spot, CancellationToken ct = default)
    {
        if (!IsRunning) return FeatureResult.Fail("Service not running");

        var digAction = new GameAction
        {
            Type = GameActionType.ClickExcavate,
            VectorParam = spot.Position,
            Priority = ActionPriority.High,
            DeduplicationKey = $"dig_{spot.Uid}"
        };

        var res = await Dispatcher.EnqueueAsync(digAction, ct);
        if (res.Success)
        {
            Stats.TotalDigs++;
            Stats.ShovelDurability = Math.Max(0, Stats.ShovelDurability - 1);
            spot.Hp -= 200;
            if (spot.Hp <= 0)
            {
                Stats.TotalExcavated++;
            }
            return FeatureResult.Ok($"Đang đào cổ vật {spot.AssetName}");
        }
        return FeatureResult.Fail(res.Message);
    }
}
