using DTA.Core.ActionDispatcher;
using DTA.Core.Cache;
using DTA.Core.Events;
using DTA.Core.Platform;
using DTA.Core.StateMachine;
using DTA.Platform;
using DTA.Shared.Enums;
using DTA.Shared.Models;
using Xunit;

namespace DTA.Core.Tests;

public sealed class CoreTests
{
    [Fact]
    public void CacheEngine_SetAndGet_WorksWithTagInvalidation()
    {
        var cache = new MemoryCache<string, string>();
        cache.Set("fish_1", "Cá chép", TimeSpan.FromMinutes(5), ["fish", "rare"]);
        cache.Set("fish_2", "Cá mè", TimeSpan.FromMinutes(5), ["fish"]);
        cache.Set("rock_1", "Đá thạch anh", TimeSpan.FromMinutes(5), ["ore"]);

        Assert.True(cache.TryGet("fish_1", out var v1));
        Assert.Equal("Cá chép", v1);
        Assert.True(cache.Hits > 0);

        // Invalidate tag "fish"
        int removed = cache.InvalidateByTag("fish");
        Assert.Equal(2, removed);

        Assert.False(cache.TryGet("fish_1", out _));
        Assert.False(cache.TryGet("fish_2", out _));
        Assert.True(cache.TryGet("rock_1", out var vRock));
        Assert.Equal("Đá thạch anh", vRock);
    }

    [Fact]
    public void EventBus_PublishesAndSubscribes_DomainEvents()
    {
        var bus = new EventBus();
        MapChangedEvent? received = null;

        using (bus.Subscribe<MapChangedEvent>(e => received = e))
        {
            bus.Publish(new MapChangedEvent(1001, 1301, "Khu nghỉ dưỡng"));
        }

        Assert.NotNull(received);
        Assert.Equal(1001, received.Value.PreviousMapId);
        Assert.Equal(1301, received.Value.NewMapId);
        Assert.Equal("Khu nghỉ dưỡng", received.Value.MapName);

        // After unsubscribe
        received = null;
        bus.Publish(new MapChangedEvent(1301, 1001, "Plaza"));
        Assert.Null(received);
    }

    private sealed class TestState(string name) : IState<object>
    {
        public string Name => name;
        public int EnterCount { get; private set; }
        public int ExitCount { get; private set; }

        public ValueTask EnterAsync(object context, CancellationToken ct = default)
        {
            EnterCount++;
            return ValueTask.CompletedTask;
        }

        public ValueTask<IState<object>?> TickAsync(object context, CancellationToken ct = default) =>
            ValueTask.FromResult<IState<object>?>(null);

        public ValueTask ExitAsync(object context, CancellationToken ct = default)
        {
            ExitCount++;
            return ValueTask.CompletedTask;
        }

        public bool CanTransitionTo(IState<object> nextState, object context) => true;
    }

    [Fact]
    public async Task GenericStateMachine_TransitionsCorrectly()
    {
        var ctx = new object();
        var s1 = new TestState("Idle");
        var s2 = new TestState("Working");

        var fsm = new StateMachine<object>(ctx, s1);
        Assert.Equal("Idle", fsm.CurrentStateName);

        bool ok = await fsm.TransitionToAsync(s2);
        Assert.True(ok);
        Assert.Equal("Working", fsm.CurrentStateName);
        Assert.Equal(1, s1.ExitCount);
        Assert.Equal(1, s2.EnterCount);
    }

    [Fact]
    public async Task PlatformDiscovery_DiscoversMockAndAttaches()
    {
        using var adapter = new MockPlatformAdapter();
        var instances = await adapter.DiscoverInstancesAsync();

        Assert.NotEmpty(instances);
        var first = instances[0];
        Assert.Equal(PlatformKind.Mock, first.Kind);

        bool attached = await adapter.AttachAsync(first);
        Assert.True(attached);
        Assert.True(adapter.IsAttached);
        Assert.Equal("mock-0", adapter.CurrentInstance.Id);
    }

    [Fact]
    public async Task ActionDispatcher_SuppressesDuplicates()
    {
        using var dispatcher = new GameActionDispatcher();

        var act1 = new GameAction
        {
            Type = GameActionType.JumpPress,
            DeduplicationKey = "jump_spam",
            Priority = ActionPriority.High
        };

        var act2 = new GameAction
        {
            Type = GameActionType.JumpPress,
            DeduplicationKey = "jump_spam",
            Priority = ActionPriority.High
        };

        var res1 = await dispatcher.EnqueueAsync(act1);
        var res2 = await dispatcher.EnqueueAsync(act2);

        Assert.True(res1.Success);
        Assert.True(res2.Success);
        Assert.Equal("Deduplicated", res2.Message);
    }
}
