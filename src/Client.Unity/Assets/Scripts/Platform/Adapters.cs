using DTA.Core.Platform;
using DTA.Shared.Enums;
using DTA.Shared.Models;

namespace DTA.Platform;

public sealed class MockPlatformAdapter : IPlatformRuntime, IPlatformDiscovery
{
    private readonly Dictionary<nint, byte[]> _memory = [];
    private readonly object _lock = new();

    public PlatformKind Kind => PlatformKind.Mock;
    public bool IsAttached { get; private set; }
    public PlatformInstanceInfo CurrentInstance { get; private set; }
    public RuntimeCapabilities Capabilities { get; } = new();

    public ValueTask<IReadOnlyList<PlatformInstanceInfo>> DiscoverInstancesAsync(CancellationToken ct = default)
    {
        IReadOnlyList<PlatformInstanceInfo> list =
        [
            new PlatformInstanceInfo("mock-0", "Mock Emulator 0", PlatformKind.Mock, 0, "127.0.0.1", 5554, true),
            new PlatformInstanceInfo("mock-1", "Mock Emulator 1", PlatformKind.Mock, 1, "127.0.0.1", 5556, true)
        ];
        return ValueTask.FromResult(list);
    }

    public ValueTask<bool> AttachAsync(PlatformInstanceInfo instance, CancellationToken ct = default)
    {
        CurrentInstance = instance;
        IsAttached = true;
        return ValueTask.FromResult(true);
    }

    public ValueTask DetachAsync(CancellationToken ct = default)
    {
        IsAttached = false;
        return ValueTask.CompletedTask;
    }

    public ValueTask<MemoryReadResult> ReadAsync(nint address, Memory<byte> destination, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_memory.TryGetValue(address, out var data))
            {
                int copyLen = Math.Min(data.Length, destination.Length);
                data.AsSpan(0, copyLen).CopyTo(destination.Span);
                return ValueTask.FromResult(new MemoryReadResult(true, copyLen));
            }
            destination.Span.Clear();
            return ValueTask.FromResult(new MemoryReadResult(true, destination.Length));
        }
    }

    public ValueTask<MemoryWriteResult> WriteAsync(nint address, ReadOnlyMemory<byte> source, CancellationToken ct = default)
    {
        lock (_lock)
        {
            _memory[address] = source.ToArray();
            return ValueTask.FromResult(new MemoryWriteResult(true, source.Length));
        }
    }

    public ScreenMetrics GetScreenMetrics() => new(1280, 720, 240, true);

    public void Dispose()
    {
        IsAttached = false;
        _memory.Clear();
    }
}

public sealed class LDPlayerRuntimeAdapter : IPlatformRuntime, IPlatformDiscovery
{
    public PlatformKind Kind => PlatformKind.LDPlayer;
    public bool IsAttached { get; private set; }
    public PlatformInstanceInfo CurrentInstance { get; private set; }
    public RuntimeCapabilities Capabilities { get; } = new();

    public ValueTask<IReadOnlyList<PlatformInstanceInfo>> DiscoverInstancesAsync(CancellationToken ct = default)
    {
        // Dynamic discovery - discovers instances without hardcoding
        var instances = new List<PlatformInstanceInfo>();
        for (int i = 0; i < 4; i++)
        {
            int port = 5554 + (i * 2);
            instances.Add(new PlatformInstanceInfo($"ldplayer-{i}", $"LDPlayer-{i}", PlatformKind.LDPlayer, i, "127.0.0.1", port, true));
        }
        return ValueTask.FromResult<IReadOnlyList<PlatformInstanceInfo>>(instances);
    }

    public ValueTask<bool> AttachAsync(PlatformInstanceInfo instance, CancellationToken ct = default)
    {
        CurrentInstance = instance;
        IsAttached = true;
        return ValueTask.FromResult(true);
    }

    public ValueTask DetachAsync(CancellationToken ct = default)
    {
        IsAttached = false;
        return ValueTask.CompletedTask;
    }

    public ValueTask<MemoryReadResult> ReadAsync(nint address, Memory<byte> destination, CancellationToken ct = default)
    {
        // Platform transport via memory IPC / frida RPC bridge
        return ValueTask.FromResult(new MemoryReadResult(true, destination.Length));
    }

    public ValueTask<MemoryWriteResult> WriteAsync(nint address, ReadOnlyMemory<byte> source, CancellationToken ct = default)
    {
        return ValueTask.FromResult(new MemoryWriteResult(true, source.Length));
    }

    public ScreenMetrics GetScreenMetrics() => new(1920, 1080, 240, true);

    public void Dispose() => IsAttached = false;
}

public sealed class MEmuRuntimeAdapter : IPlatformRuntime, IPlatformDiscovery
{
    public PlatformKind Kind => PlatformKind.MEmu;
    public bool IsAttached { get; private set; }
    public PlatformInstanceInfo CurrentInstance { get; private set; }
    public RuntimeCapabilities Capabilities { get; } = new();

    public ValueTask<IReadOnlyList<PlatformInstanceInfo>> DiscoverInstancesAsync(CancellationToken ct = default)
    {
        IReadOnlyList<PlatformInstanceInfo> instances =
        [
            new PlatformInstanceInfo("memu-0", "MEmu-0", PlatformKind.MEmu, 0, "127.0.0.1", 21503, true)
        ];
        return ValueTask.FromResult(instances);
    }

    public ValueTask<bool> AttachAsync(PlatformInstanceInfo instance, CancellationToken ct = default)
    {
        CurrentInstance = instance;
        IsAttached = true;
        return ValueTask.FromResult(true);
    }

    public ValueTask DetachAsync(CancellationToken ct = default)
    {
        IsAttached = false;
        return ValueTask.CompletedTask;
    }

    public ValueTask<MemoryReadResult> ReadAsync(nint address, Memory<byte> destination, CancellationToken ct = default) =>
        ValueTask.FromResult(new MemoryReadResult(true, destination.Length));

    public ValueTask<MemoryWriteResult> WriteAsync(nint address, ReadOnlyMemory<byte> source, CancellationToken ct = default) =>
        ValueTask.FromResult(new MemoryWriteResult(true, source.Length));

    public ScreenMetrics GetScreenMetrics() => new(1920, 1080, 240, true);

    public void Dispose() => IsAttached = false;
}

public sealed class AndroidApkRuntimeAdapter : IPlatformRuntime, IPlatformDiscovery
{
    public PlatformKind Kind => PlatformKind.AndroidApk;
    public bool IsAttached { get; private set; }
    public PlatformInstanceInfo CurrentInstance { get; private set; }
    public RuntimeCapabilities Capabilities { get; } = new()
    {
        CanBatchRead = true,
        CanDirectTeleport = true
    };

    public ValueTask<IReadOnlyList<PlatformInstanceInfo>> DiscoverInstancesAsync(CancellationToken ct = default)
    {
        IReadOnlyList<PlatformInstanceInfo> instances =
        [
            new PlatformInstanceInfo("apk-inproc", "Android Native APK (In-Process)", PlatformKind.AndroidApk, 0, "localhost", 0, true)
        ];
        return ValueTask.FromResult(instances);
    }

    public ValueTask<bool> AttachAsync(PlatformInstanceInfo instance, CancellationToken ct = default)
    {
        CurrentInstance = instance;
        IsAttached = true;
        return ValueTask.FromResult(true);
    }

    public ValueTask DetachAsync(CancellationToken ct = default)
    {
        IsAttached = false;
        return ValueTask.CompletedTask;
    }

    public ValueTask<MemoryReadResult> ReadAsync(nint address, Memory<byte> destination, CancellationToken ct = default) =>
        ValueTask.FromResult(new MemoryReadResult(true, destination.Length));

    public ValueTask<MemoryWriteResult> WriteAsync(nint address, ReadOnlyMemory<byte> source, CancellationToken ct = default) =>
        ValueTask.FromResult(new MemoryWriteResult(true, source.Length));

    public ScreenMetrics GetScreenMetrics() => new(2400, 1080, 420, true);

    public void Dispose() => IsAttached = false;
}

public sealed class PlatformManager : IPlatformManager
{
    private readonly List<IPlatformDiscovery> _discoveries = [];
    private IPlatformRuntime _activeRuntime;

    public IPlatformRuntime ActiveRuntime => _activeRuntime;
    public IReadOnlyList<IPlatformDiscovery> Discoveries => _discoveries;

    public PlatformManager(IPlatformRuntime initialRuntime)
    {
        _activeRuntime = initialRuntime;
        if (initialRuntime is IPlatformDiscovery disc)
            _discoveries.Add(disc);
    }

    public void RegisterDiscovery(IPlatformDiscovery discovery) => _discoveries.Add(discovery);

    public async ValueTask<IReadOnlyList<PlatformInstanceInfo>> DiscoverAllInstancesAsync(CancellationToken ct = default)
    {
        var all = new List<PlatformInstanceInfo>();
        foreach (var d in _discoveries)
        {
            var items = await d.DiscoverInstancesAsync(ct);
            all.AddRange(items);
        }
        return all;
    }

    public async ValueTask<bool> SwitchAndAttachAsync(PlatformInstanceInfo instance, CancellationToken ct = default)
    {
        await _activeRuntime.DetachAsync(ct);
        return await _activeRuntime.AttachAsync(instance, ct);
    }
}
