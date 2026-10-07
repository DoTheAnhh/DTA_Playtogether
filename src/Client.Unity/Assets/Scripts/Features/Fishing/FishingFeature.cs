using DTA.Core.ActionDispatcher;
using DTA.Core.Cache;
using DTA.Core.StateMachine;
using DTA.Features.Base;
using DTA.Shared.Enums;
using DTA.Shared.Models;

namespace DTA.Features.Fishing;

public sealed class FishingOptions
{
    public RewardAction Action { get; set; } = RewardAction.Keep;
    public bool HasPackage { get; set; } = false;
    public bool AutoRepair { get; set; } = true;
    public bool FastBiteEnabled { get; set; } = false;
    public bool LockPovEnabled { get; set; } = false;

    // Filters
    public bool FilterOn { get; set; } = false;
    public HashSet<int> WantedIds { get; set; } = [];
    public HashSet<int> WantedShadows { get; set; } = [];
    public HashSet<int> WantedGrades { get; set; } = [];

    // Keep exceptions during sell
    public bool KeepMutant { get; set; } = true;
    public bool KeepVariant { get; set; } = true;
    public HashSet<int> KeepGrades { get; set; } = [4, 5]; // Purple & VVIP
}

public sealed class FishingStats
{
    public int TotalCaught { get; set; }
    public int TotalKept { get; set; }
    public int TotalSold { get; set; }
    public int TotalRepairs { get; set; }
    public int RodDurability { get; set; } = 100;
    public string LastFishName { get; set; } = string.Empty;
}

public sealed class FishingCatalog
{
    private readonly ICache<int, FishItemDTO> _cache;

    public FishingCatalog(ICache<int, FishItemDTO> cache)
    {
        _cache = cache;
        SeedDefaults();
    }

    private void SeedDefaults()
    {
        // Sample standard fish entries from Play Together catalog
        var defaultFish = new[]
        {
            new FishItemDTO { Id = 101, Name = "Cá chép", ShadowSize = 3, Grade = 1, MapId = MapConstants.MapPlaza },
            new FishItemDTO { Id = 102, Name = "Cá mè", ShadowSize = 3, Grade = 1, MapId = MapConstants.MapPlaza },
            new FishItemDTO { Id = 201, Name = "Cá rô phi", ShadowSize = 2, Grade = 2, MapId = MapConstants.MapCamp },
            new FishItemDTO { Id = 301, Name = "Cá voi sát thủ", ShadowSize = 6, Grade = 4, MapId = MapConstants.MapResort },
            new FishItemDTO { Id = 302, Name = "Cá voi xanh khổng lồ", ShadowSize = 7, Grade = 5, MapId = MapConstants.MapResort }
        };

        foreach (var f in defaultFish)
        {
            _cache.Set(f.Id, f, TimeSpan.FromHours(24), ["fish_catalog"]);
        }
    }

    public FishItemDTO? GetFish(int id) =>
        _cache.TryGet(id, out var item) ? item : null;

    public bool MatchesFilter(FishItemDTO fish, FishingOptions options)
    {
        if (!options.FilterOn) return true;

        if (options.WantedIds.Count > 0 && !options.WantedIds.Contains(fish.Id))
            return false;
        if (options.WantedShadows.Count > 0 && !options.WantedShadows.Contains(fish.ShadowSize))
            return false;
        if (options.WantedGrades.Count > 0 && !options.WantedGrades.Contains(fish.Grade))
            return false;

        return true;
    }
}

public interface IFishingService : IFeatureService
{
    FishingOptions Options { get; }
    FishingStats Stats { get; }
    FishingSessionState CurrentSessionState { get; }
    ValueTask<FeatureResult> CastRodAsync(CancellationToken ct = default);
    ValueTask<FeatureResult> ReelInAsync(CancellationToken ct = default);
}

public sealed class FishingService : BaseFeatureService<FishingOptions, FishingStats>, IFishingService
{
    public override FeatureId Id => FeatureId.Fishing;
    public override string Name => "Câu cá";

    public FishingCatalog Catalog { get; }
    public FishingSessionState CurrentSessionState { get; private set; } = FishingSessionState.Idle;

    public FishingService(
        IGameActionDispatcher dispatcher,
        FishingCatalog catalog) : base(dispatcher)
    {
        Catalog = catalog;
    }

    public override ValueTask<FeatureResult> StartAsync(CancellationToken ct = default)
    {
        IsRunning = true;
        CurrentSessionState = FishingSessionState.Idle;
        return ValueTask.FromResult(FeatureResult.Ok("Fishing bot started"));
    }

    public override ValueTask<FeatureResult> StopAsync(CancellationToken ct = default)
    {
        IsRunning = false;
        CurrentSessionState = FishingSessionState.Idle;
        return ValueTask.FromResult(FeatureResult.Ok("Fishing bot stopped"));
    }

    public async ValueTask<FeatureResult> CastRodAsync(CancellationToken ct = default)
    {
        if (!IsRunning) return FeatureResult.Fail("Service is not running");

        // Auto repair check
        if (Stats.RodDurability <= 0)
        {
            if (Options.AutoRepair)
            {
                var repAction = new GameAction { Type = GameActionType.Repair, Priority = ActionPriority.High };
                await Dispatcher.EnqueueAsync(repAction, ct);
                Stats.RodDurability = 100;
                Stats.TotalRepairs++;
            }
            else
            {
                return FeatureResult.Fail("Cần câu đã hỏng");
            }
        }

        var action = new GameAction
        {
            Type = GameActionType.ClickFishing,
            Priority = ActionPriority.High,
            DeduplicationKey = "cast_rod"
        };

        var res = await Dispatcher.EnqueueAsync(action, ct);
        if (res.Success)
        {
            CurrentSessionState = FishingSessionState.WaitingBite;
            Stats.RodDurability = Math.Max(0, Stats.RodDurability - 1);
            return FeatureResult.Ok("Đã thả cần câu");
        }
        return FeatureResult.Fail(res.Message);
    }

    public async ValueTask<FeatureResult> ReelInAsync(CancellationToken ct = default)
    {
        var action = new GameAction
        {
            Type = GameActionType.JumpPress,
            Priority = ActionPriority.High,
            DeduplicationKey = "reel_in"
        };
        var res = await Dispatcher.EnqueueAsync(action, ct);
        if (res.Success)
        {
            CurrentSessionState = FishingSessionState.CatchResult;
            Stats.TotalCaught++;
            return FeatureResult.Ok("Đã giật cá");
        }
        return FeatureResult.Fail(res.Message);
    }
}
