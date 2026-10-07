namespace DTA.Core.StateMachine;

public interface IState<TContext>
{
    string Name { get; }
    ValueTask EnterAsync(TContext context, CancellationToken ct = default);
    ValueTask<IState<TContext>?> TickAsync(TContext context, CancellationToken ct = default);
    ValueTask ExitAsync(TContext context, CancellationToken ct = default);
    bool CanTransitionTo(IState<TContext> nextState, TContext context);
}

public sealed class StateMachine<TContext>
{
    private IState<TContext>? _currentState;
    private readonly TContext _context;
    private readonly object _lock = new();

    public IState<TContext>? CurrentState => _currentState;
    public string CurrentStateName => _currentState?.Name ?? "None";
    public event Action<string, string>? StateChanged;

    public StateMachine(TContext context, IState<TContext>? initialState = null)
    {
        _context = context;
        _currentState = initialState;
    }

    public async ValueTask<bool> TransitionToAsync(IState<TContext> nextState, CancellationToken ct = default)
    {
        IState<TContext>? previous;
        lock (_lock)
        {
            if (_currentState != null && !_currentState.CanTransitionTo(nextState, _context))
                return false;

            previous = _currentState;
            _currentState = nextState;
        }

        if (previous != null)
            await previous.ExitAsync(_context, ct);

        await nextState.EnterAsync(_context, ct);
        StateChanged?.Invoke(previous?.Name ?? "None", nextState.Name);
        return true;
    }

    public async ValueTask TickAsync(CancellationToken ct = default)
    {
        IState<TContext>? current;
        lock (_lock)
        {
            current = _currentState;
        }

        if (current == null) return;

        var next = await current.TickAsync(_context, ct);
        if (next != null && next != current)
        {
            await TransitionToAsync(next, ct);
        }
    }
}
