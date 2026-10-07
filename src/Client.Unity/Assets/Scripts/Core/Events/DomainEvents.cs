using DTA.Shared.Enums;
using DTA.Shared.Models;

namespace DTA.Core.Events;

public readonly record struct GameAttachedEvent(PlatformKind Kind, string InstanceId, GameIdentity Identity);
public readonly record struct MapChangedEvent(int PreviousMapId, int NewMapId, string MapName);
public readonly record struct PlayerStateChangedEvent(Vector3 Position, Quaternion Rotation, CharacterViewpoint Viewpoint, bool IsMoving);
public readonly record struct FeatureStateChangedEvent(FeatureId Feature, bool IsRunning, string StateName);
public readonly record struct CacheInvalidatedEvent(string Tag, int EntriesRemoved);
public readonly record struct TeleportExecutedEvent(Vector3 TargetPosition, Quaternion Rotation, CharacterViewpoint Viewpoint, bool Success);

public interface IEventBus
{
    void Publish<TEvent>(in TEvent @event) where TEvent : struct;
    IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct;
}

public sealed class EventBus : IEventBus
{
    private readonly Dictionary<Type, List<Delegate>> _handlers = [];
    private readonly object _lock = new();

    public void Publish<TEvent>(in TEvent @event) where TEvent : struct
    {
        List<Delegate> copy;
        lock (_lock)
        {
            if (!_handlers.TryGetValue(typeof(TEvent), out var list) || list.Count == 0)
                return;
            copy = [.. list];
        }

        foreach (var d in copy)
        {
            ((Action<TEvent>)d)(@event);
        }
    }

    public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct
    {
        lock (_lock)
        {
            var t = typeof(TEvent);
            if (!_handlers.TryGetValue(t, out var list))
            {
                list = [];
                _handlers[t] = list;
            }
            list.Add(handler);
        }

        return new Unsubscriber(() =>
        {
            lock (_lock)
            {
                if (_handlers.TryGetValue(typeof(TEvent), out var list))
                {
                    list.Remove(handler);
                }
            }
        });
    }

    private sealed class Unsubscriber(Action onDispose) : IDisposable
    {
        private Action? _onDispose = onDispose;
        public void Dispose()
        {
            Interlocked.Exchange(ref _onDispose, null)?.Invoke();
        }
    }
}
