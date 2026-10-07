using DTA.Shared.Enums;
using DTA.Shared.Models;

namespace DTA.Core.Platform;

public readonly record struct PlatformInstanceInfo(
    string Id,
    string Name,
    PlatformKind Kind,
    int Index,
    string Host,
    int Port,
    bool IsRunning
);

public readonly record struct MemoryReadResult(bool Success, int BytesRead, string? Error = null);
public readonly record struct MemoryWriteResult(bool Success, int BytesWritten, string? Error = null);

public interface IPlatformRuntime : IDisposable
{
    PlatformKind Kind { get; }
    bool IsAttached { get; }
    PlatformInstanceInfo CurrentInstance { get; }
    RuntimeCapabilities Capabilities { get; }

    ValueTask<bool> AttachAsync(PlatformInstanceInfo instance, CancellationToken ct = default);
    ValueTask DetachAsync(CancellationToken ct = default);
    ValueTask<MemoryReadResult> ReadAsync(nint address, Memory<byte> destination, CancellationToken ct = default);
    ValueTask<MemoryWriteResult> WriteAsync(nint address, ReadOnlyMemory<byte> source, CancellationToken ct = default);
    ScreenMetrics GetScreenMetrics();
}

public interface IPlatformDiscovery
{
    PlatformKind Kind { get; }
    ValueTask<IReadOnlyList<PlatformInstanceInfo>> DiscoverInstancesAsync(CancellationToken ct = default);
}

public interface IPlatformManager
{
    IPlatformRuntime ActiveRuntime { get; }
    IReadOnlyList<IPlatformDiscovery> Discoveries { get; }
    ValueTask<IReadOnlyList<PlatformInstanceInfo>> DiscoverAllInstancesAsync(CancellationToken ct = default);
    ValueTask<bool> SwitchAndAttachAsync(PlatformInstanceInfo instance, CancellationToken ct = default);
}
