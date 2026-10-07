using DTA.Core.ActionDispatcher;
using DTA.Features.Base;
using DTA.Shared.Enums;
using DTA.Shared.Models;

namespace DTA.Features.Mining;

public sealed class RockEntity
{
    public uint Uid { get; init; }
    public Vector3 Position { get; init; }
    public string Kind { get; init; } = "rock"; // "rock", "event"
    public int Hp { get; set; } = 3;
    public bool IsBroken => Hp <= 0;
}

public sealed class MiningOptions
{
    public string Kind { get; set; } = "all"; // "all", "rock", "event"
    public MoveMode MoveMode { get; set; } = MoveMode.Walk;
    public float Radius { get; set; } = 50f;
    public bool AutoRepair { get; set; } = true;
}

public sealed class MiningStats
{
    public int TotalRocksMined { get; set; }
    public int TotalHits { get; set; }
    public int TotalRepairs { get; set; }
    public int PickaxeDurability { get; set; } = 100;
}

public interface IMiningService : IFeatureService
{
    MiningOptions Options { get; }
    MiningStats Stats { get; }
    ValueTask<FeatureResult> MineRockAsync(RockEntity rock, CancellationToken ct = default);
}

public sealed class MiningService : BaseFeatureService<MiningOptions, MiningStats>, IMiningService
{
    public override FeatureId Id => FeatureId.Mining;
    public override string Name => "Đập đá";

    public MiningService(IGameActionDispatcher dispatcher) : base(dispatcher) { }

    public override ValueTask<FeatureResult> StartAsync(CancellationToken ct = default)
    {
        IsRunning = true;
        return ValueTask.FromResult(FeatureResult.Ok("Mining bot started"));
    }

    public override ValueTask<FeatureResult> StopAsync(CancellationToken ct = default)
    {
        IsRunning = false;
        return ValueTask.FromResult(FeatureResult.Ok("Mining bot stopped"));
    }

    public async ValueTask<FeatureResult> MineRockAsync(RockEntity rock, CancellationToken ct = default)
    {
        if (!IsRunning) return FeatureResult.Fail("Service not running");

        // Tool durability check
        if (Stats.PickaxeDurability <= 0)
        {
            if (Options.AutoRepair)
            {
                var rep = new GameAction { Type = GameActionType.Repair, Priority = ActionPriority.High };
                await Dispatcher.EnqueueAsync(rep, ct);
                Stats.PickaxeDurability = 100;
                Stats.TotalRepairs++;
            }
            else
            {
                return FeatureResult.Fail("Cuốc đã hỏng");
            }
        }

        var swingAction = new GameAction
        {
            Type = GameActionType.ClickPickaxe,
            VectorParam = rock.Position,
            Priority = ActionPriority.High,
            DeduplicationKey = $"mine_{rock.Uid}"
        };

        var res = await Dispatcher.EnqueueAsync(swingAction, ct);
        if (res.Success)
        {
            Stats.TotalHits++;
            Stats.PickaxeDurability = Math.Max(0, Stats.PickaxeDurability - 1);
            rock.Hp--;
            if (rock.IsBroken)
            {
                Stats.TotalRocksMined++;
            }
            return FeatureResult.Ok($"Đã đập mạch đá {rock.Uid}");
        }
        return FeatureResult.Fail(res.Message);
    }
}
