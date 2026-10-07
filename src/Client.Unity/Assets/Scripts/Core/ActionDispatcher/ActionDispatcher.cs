using System.Diagnostics;
using System.Threading.Channels;
using DTA.Shared.Models;

namespace DTA.Core.ActionDispatcher;

public enum GameActionType
{
    None = 0,
    ClickFishing = 1,
    ClickPickaxe = 2,
    ClickInsect = 3,
    ClickExcavate = 4,
    ActionTool = 5,
    JumpPress = 6,
    JumpRelease = 7,
    Repair = 8,
    KeepFish = 9,
    SellFish = 10,
    PickObject = 11,
    Teleport = 12,
    ConnectToZoneMove = 13,
    AlignCamera = 14,
    CustomNative = 15
}

public enum ActionPriority
{
    Low = 0,
    Normal = 1,
    High = 2
}

public sealed class GameAction
{
    public GameActionType Type { get; init; }
    public nint TargetAddress { get; init; }
    public int IntParam { get; init; }
    public float FloatParam { get; init; }
    public Vector3 VectorParam { get; init; }
    public Quaternion RotationParam { get; init; }
    public CharacterViewpoint? ViewpointParam { get; init; }
    public ActionPriority Priority { get; init; } = ActionPriority.Normal;
    public string? DeduplicationKey { get; init; }
    public int TimeoutMs { get; init; } = 3000;
}

public readonly record struct ActionResult(bool Success, string Message, double DurationMs = 0.0)
{
    public static ActionResult Ok(string msg = "OK", double durationMs = 0.0) => new(true, msg, durationMs);
    public static ActionResult Fail(string error, double durationMs = 0.0) => new(false, error, durationMs);
}

public interface IGameActionDispatcher
{
    ValueTask<ActionResult> EnqueueAsync(GameAction action, CancellationToken ct = default);
    int QueueDepth { get; }
    double AverageDurationMs { get; }
    double P95DurationMs { get; }
}

public sealed class GameActionDispatcher : IGameActionDispatcher, IDisposable
{
    private readonly Channel<(GameAction Action, TaskCompletionSource<ActionResult> Tcs)> _channel;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _workerTask;
    private readonly Dictionary<string, long> _dedupMap = [];
    private readonly object _dedupLock = new();
    private readonly List<double> _durationSamples = [];
    private readonly object _samplesLock = new();

    public int QueueDepth => _channel.Reader.Count;
    public double AverageDurationMs { get; private set; }
    public double P95DurationMs { get; private set; }

    public GameActionDispatcher(int capacity = 256)
    {
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true
        };
        _channel = Channel.CreateBounded<(GameAction, TaskCompletionSource<ActionResult>)>(options);
        _workerTask = Task.Run(ProcessQueueAsync);
    }

    public async ValueTask<ActionResult> EnqueueAsync(GameAction action, CancellationToken ct = default)
    {
        if (_cts.IsCancellationRequested)
            return ActionResult.Fail("Dispatcher is stopped");

        // Duplicate suppression
        if (!string.IsNullOrEmpty(action.DeduplicationKey))
        {
            long now = Environment.TickCount64;
            lock (_dedupLock)
            {
                if (_dedupMap.TryGetValue(action.DeduplicationKey, out long lastTime) && now - lastTime < 150)
                {
                    return ActionResult.Ok("Deduplicated");
                }
                _dedupMap[action.DeduplicationKey] = now;
            }
        }

        var tcs = new TaskCompletionSource<ActionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linkedCts.CancelAfter(action.TimeoutMs);

        await _channel.Writer.WriteAsync((action, tcs), linkedCts.Token);

        using (linkedCts.Token.Register(() => tcs.TrySetResult(ActionResult.Fail("Action timed out"))))
        {
            return await tcs.Task;
        }
    }

    private async Task ProcessQueueAsync()
    {
        var reader = _channel.Reader;
        var sw = new Stopwatch();

        while (await reader.WaitToReadAsync(_cts.Token))
        {
            while (reader.TryRead(out var item))
            {
                sw.Restart();
                ActionResult result;
                try
                {
                    // Action execution simulation / bridge invocation
                    result = ExecuteAction(item.Action);
                }
                catch (Exception ex)
                {
                    result = ActionResult.Fail($"Execution failed: {ex.Message}");
                }
                sw.Stop();
                double duration = sw.Elapsed.TotalMilliseconds;
                RecordMetric(duration);

                var finalResult = new ActionResult(result.Success, result.Message, duration);
                item.Tcs.TrySetResult(finalResult);
            }
        }
    }

    private ActionResult ExecuteAction(GameAction action)
    {
        // Executes based on action type
        return action.Type switch
        {
            GameActionType.Teleport => ActionResult.Ok($"Teleported to {action.VectorParam} with viewpoint {action.ViewpointParam}"),
            GameActionType.AlignCamera => ActionResult.Ok("Camera aligned"),
            _ => ActionResult.Ok($"Action {action.Type} executed")
        };
    }

    private void RecordMetric(double durationMs)
    {
        lock (_samplesLock)
        {
            _durationSamples.Add(durationMs);
            if (_durationSamples.Count > 100)
                _durationSamples.RemoveAt(0);

            AverageDurationMs = _durationSamples.Average();
            var sorted = _durationSamples.OrderBy(x => x).ToList();
            int p95Idx = (int)Math.Ceiling(sorted.Count * 0.95) - 1;
            P95DurationMs = sorted[Math.Clamp(p95Idx, 0, sorted.Count - 1)];
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _channel.Writer.TryComplete();
        _cts.Dispose();
    }
}
