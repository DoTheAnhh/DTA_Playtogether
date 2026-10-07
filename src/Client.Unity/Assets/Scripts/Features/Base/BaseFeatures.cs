using DTA.Core.ActionDispatcher;
using DTA.Shared.Enums;
using DTA.Shared.Models;

namespace DTA.Features.Base;

public readonly record struct FeatureResult(bool Success, string Message, object? Data = null)
{
    public static FeatureResult Ok(string msg = "OK", object? data = null) => new(true, msg, data);
    public static FeatureResult Fail(string err) => new(false, err);
}

public interface IFeatureService
{
    FeatureId Id { get; }
    string Name { get; }
    bool IsRunning { get; }
    ValueTask<FeatureResult> StartAsync(CancellationToken ct = default);
    ValueTask<FeatureResult> StopAsync(CancellationToken ct = default);
}

public abstract class BaseFeatureService<TOptions, TStats> : IFeatureService where TOptions : class, new() where TStats : class, new()
{
    public abstract FeatureId Id { get; }
    public abstract string Name { get; }
    public bool IsRunning { get; protected set; }

    public TOptions Options { get; set; } = new();
    public TStats Stats { get; protected set; } = new();

    protected readonly IGameActionDispatcher Dispatcher;

    protected BaseFeatureService(IGameActionDispatcher dispatcher)
    {
        Dispatcher = dispatcher;
    }

    public abstract ValueTask<FeatureResult> StartAsync(CancellationToken ct = default);
    public abstract ValueTask<FeatureResult> StopAsync(CancellationToken ct = default);
}

public interface ITargetScanner<TTarget>
{
    ValueTask<IReadOnlyList<TTarget>> ScanTargetsAsync(Vector3 currentPosition, float radius, CancellationToken ct = default);
}

public interface ITargetSelector<TTarget, TRule>
{
    TTarget? SelectBestTarget(IReadOnlyList<TTarget> targets, TRule rules, Vector3 currentPosition);
}
