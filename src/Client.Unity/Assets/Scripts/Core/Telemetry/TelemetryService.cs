namespace DTA.Core.Telemetry;

public sealed class TelemetryMetrics
{
    public double AverageActionLatencyMs { get; set; }
    public double P95ActionLatencyMs { get; set; }
    public double CacheHitRate { get; set; }
    public int ActionQueueDepth { get; set; }
    public long TotalActionsExecuted { get; set; }
    public long TotalTeleportsCompleted { get; set; }
    public long MemoryAllocatedBytes { get; set; }
}

public interface ITelemetryService
{
    TelemetryMetrics CurrentMetrics { get; }
    void RecordActionLatency(double ms);
    void RecordTeleport();
}

public sealed class TelemetryService : ITelemetryService
{
    private readonly TelemetryMetrics _metrics = new();
    private long _totalActions;
    private long _totalTeleports;

    public TelemetryMetrics CurrentMetrics
    {
        get
        {
            _metrics.TotalActionsExecuted = Interlocked.Read(ref _totalActions);
            _metrics.TotalTeleportsCompleted = Interlocked.Read(ref _totalTeleports);
            _metrics.MemoryAllocatedBytes = GC.GetTotalMemory(false);
            return _metrics;
        }
    }

    public void RecordActionLatency(double ms)
    {
        Interlocked.Increment(ref _totalActions);
        _metrics.AverageActionLatencyMs = ms;
    }

    public void RecordTeleport()
    {
        Interlocked.Increment(ref _totalTeleports);
    }
}
