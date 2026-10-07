using DTA.Core.ActionDispatcher;
using DTA.Core.Cache;
using DTA.Core.Events;
using DTA.Features.Excavation;
using DTA.Features.Fishing;
using DTA.Features.Insect;
using DTA.Features.Mining;
using DTA.Features.Teleport;
using DTA.Shared.Enums;
using DTA.Shared.Models;
using Xunit;

namespace DTA.Features.Tests;

public sealed class FeaturesTests
{
    [Fact]
    public async Task Teleport_AlwaysIncludes_CharacterRotation_AndViewpoint()
    {
        using var dispatcher = new GameActionDispatcher();
        var registry = new WaypointRegistry();
        var eventBus = new EventBus();
        var service = new TeleportService(dispatcher, registry, eventBus);

        TeleportExecutedEvent? firedEvent = null;
        using var sub = eventBus.Subscribe<TeleportExecutedEvent>(e => firedEvent = e);

        // Define target location with Character Rotation and Character Viewpoint
        var targetPos = new Vector3(100f, 2.5f, -50f);
        var targetRot = Quaternion.FromEuler(0f, 135f, 0f);
        var targetViewpoint = new CharacterViewpoint(135f, 15f, 4.5f, true);

        var destination = new TelePositionDTO(
            "test_spot",
            "Điểm bí mật",
            MapConstants.MapResort,
            "Khu nghỉ dưỡng",
            targetPos.X,
            targetPos.Y,
            targetPos.Z,
            targetRot,
            targetViewpoint
        );

        var res = await service.TeleportToAsync(destination, alignCamera: true);

        Assert.True(res.Success);
        Assert.NotNull(firedEvent);

        // VERIFY: Character Rotation AND Character Viewpoint were preserved and applied!
        Assert.Equal(targetRot, firedEvent.Value.Rotation);
        Assert.Equal(targetViewpoint.Yaw, firedEvent.Value.Viewpoint.Yaw);
        Assert.Equal(targetViewpoint.Pitch, firedEvent.Value.Viewpoint.Pitch);
        Assert.True(firedEvent.Value.Viewpoint.AlignCameraBehind);

        // Verify stats
        Assert.Equal(1, service.Stats.TotalTeleports);
        Assert.Equal(targetViewpoint, service.Stats.LastViewpoint);
    }

    [Fact]
    public void WaypointRegistry_Stores_CharacterRotation_AndViewpoint_ForServerAndLocal()
    {
        var registry = new WaypointRegistry();
        var all = registry.GetAllWaypoints();

        // Check that all default server waypoints have character viewpoint defined
        Assert.NotEmpty(all);
        foreach (var wp in all)
        {
            Assert.NotNull(wp.Viewpoint);
            Assert.True(wp.Viewpoint.CameraDistance > 0);
        }

        // Add custom local waypoint with rotation and viewpoint
        var myPos = new Vector3(50f, 1f, 20f);
        var myRot = Quaternion.FromEuler(0f, 270f, 0f);
        var myViewpoint = new CharacterViewpoint(270f, 5f, 3.5f, true);

        var added = registry.AddLocalWaypoint("Bãi câu bí mật của tôi", MapConstants.MapCamp, myPos, myRot, myViewpoint, "Vị trí câu cá hiếm");

        Assert.NotNull(added);
        Assert.Equal("local", added.Category);
        Assert.Equal(myRot, added.Rotation);
        Assert.Equal(myViewpoint, added.Viewpoint);

        // Lookup
        var found = registry.FindById(added.Id);
        Assert.NotNull(found);
        Assert.Equal(270f, found.Viewpoint.Yaw);
        Assert.Equal(5f, found.Viewpoint.Pitch);
    }

    [Fact]
    public async Task Fishing_CatalogAndActions_WorkCorrectly()
    {
        using var dispatcher = new GameActionDispatcher();
        var cache = new MemoryCache<int, FishItemDTO>();
        var catalog = new FishingCatalog(cache);
        var service = new FishingService(dispatcher, catalog);

        await service.StartAsync();

        // Cast rod
        var castRes = await service.CastRodAsync();
        Assert.True(castRes.Success);
        Assert.Equal(FishingSessionState.WaitingBite, service.CurrentSessionState);

        // Reel in
        var reelRes = await service.ReelInAsync();
        Assert.True(reelRes.Success);
        Assert.Equal(FishingSessionState.CatchResult, service.CurrentSessionState);
        Assert.Equal(1, service.Stats.TotalCaught);
    }

    [Fact]
    public async Task Mining_HitsOre_AndDeductsHp()
    {
        using var dispatcher = new GameActionDispatcher();
        var service = new MiningService(dispatcher);
        await service.StartAsync();

        var rock = new RockEntity { Uid = 1001, Position = new Vector3(10, 0, 10), Hp = 2 };

        var hit1 = await service.MineRockAsync(rock);
        Assert.True(hit1.Success);
        Assert.Equal(1, rock.Hp);
        Assert.False(rock.IsBroken);

        var hit2 = await service.MineRockAsync(rock);
        Assert.True(hit2.Success);
        Assert.Equal(0, rock.Hp);
        Assert.True(rock.IsBroken);
        Assert.Equal(1, service.Stats.TotalRocksMined);
    }

    [Fact]
    public void InsectAwareness_AdaptsSpeed_BasedOnDistance()
    {
        var bug = new BugEntity { Uid = 501, Position = new Vector3(0, 0, 10), AlertRadius = 3f };

        // Very close (within alert radius) -> Sneak
        float speedClose = InsectAwareness.GetSafeApproachSpeed(bug, new Vector3(0, 0, 8));
        Assert.Equal(1.2f, speedClose);

        // Medium distance -> Walk
        float speedMed = InsectAwareness.GetSafeApproachSpeed(bug, new Vector3(0, 0, 6));
        Assert.Equal(2.5f, speedMed);

        // Far distance -> Run
        float speedFar = InsectAwareness.GetSafeApproachSpeed(bug, new Vector3(0, 0, 0));
        Assert.Equal(6.0f, speedFar);
    }

    [Fact]
    public async Task Excavation_DigsRelic_Successfully()
    {
        using var dispatcher = new GameActionDispatcher();
        var service = new ExcavationService(dispatcher);
        await service.StartAsync();

        var spot = new ExcavationSpot { Uid = 901, AssetName = "spawn_excavation_pyramid", Hp = 200 };
        var res = await service.DigSpotAsync(spot);

        Assert.True(res.Success);
        Assert.True(spot.Hp <= 0);
        Assert.Equal(1, service.Stats.TotalExcavated);
    }
}
