using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using DTA.Core.Platform;
using DTA.Shared.Models;

namespace DTA.Core.Memory;

public interface IMemoryReader
{
    bool Read<T>(nint address, out T value) where T : unmanaged;
    bool ReadBytes(nint address, Span<byte> destination);
    string ReadString(nint address, int maxChars = 64);
    Vector3 ReadVector3(nint address);
    Quaternion ReadQuaternion(nint address);
}

public interface IMemoryWriter
{
    bool Write<T>(nint address, in T value) where T : unmanaged;
    bool WriteBytes(nint address, ReadOnlySpan<byte> source);
}

public sealed class MemoryService : IMemoryReader, IMemoryWriter
{
    private readonly IPlatformRuntime _runtime;

    public MemoryService(IPlatformRuntime runtime)
    {
        _runtime = runtime;
    }

    public bool Read<T>(nint address, out T value) where T : unmanaged
    {
        Unsafe.SkipInit(out value);
        if (!_runtime.IsAttached || address == 0) return false;

        int size = Unsafe.SizeOf<T>();
        byte[] rent = ArrayPool<byte>.Shared.Rent(size);
        try
        {
            var res = _runtime.ReadAsync(address, rent.AsMemory(0, size)).AsTask().GetAwaiter().GetResult();
            if (res.Success && res.BytesRead == size)
            {
                value = MemoryMarshal.Read<T>(rent.AsSpan(0, size));
                return true;
            }
            return false;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rent);
        }
    }

    public bool ReadBytes(nint address, Span<byte> destination)
    {
        if (!_runtime.IsAttached || address == 0) return false;
        byte[] rent = ArrayPool<byte>.Shared.Rent(destination.Length);
        try
        {
            var res = _runtime.ReadAsync(address, rent.AsMemory(0, destination.Length)).AsTask().GetAwaiter().GetResult();
            if (res.Success && res.BytesRead == destination.Length)
            {
                rent.AsSpan(0, destination.Length).CopyTo(destination);
                return true;
            }
            return false;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rent);
        }
    }

    public string ReadString(nint address, int maxChars = 64)
    {
        if (!_runtime.IsAttached || address == 0) return string.Empty;
        int byteLen = maxChars * 2;
        byte[] rent = ArrayPool<byte>.Shared.Rent(byteLen);
        try
        {
            var res = _runtime.ReadAsync(address, rent.AsMemory(0, byteLen)).AsTask().GetAwaiter().GetResult();
            if (!res.Success) return string.Empty;

            int nullIdx = -1;
            for (int i = 0; i < byteLen; i += 2)
            {
                if (rent[i] == 0 && rent[i + 1] == 0)
                {
                    nullIdx = i;
                    break;
                }
            }
            int actualBytes = nullIdx >= 0 ? nullIdx : byteLen;
            return Encoding.Unicode.GetString(rent, 0, actualBytes);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rent);
        }
    }

    public Vector3 ReadVector3(nint address) =>
        Read<Vector3>(address, out var v) ? v : Vector3.Zero;

    public Quaternion ReadQuaternion(nint address) =>
        Read<Quaternion>(address, out var q) ? q : Quaternion.Identity;

    public bool Write<T>(nint address, in T value) where T : unmanaged
    {
        if (!_runtime.IsAttached || address == 0) return false;
        int size = Unsafe.SizeOf<T>();
        byte[] rent = ArrayPool<byte>.Shared.Rent(size);
        try
        {
            MemoryMarshal.Write(rent.AsSpan(0, size), in value);
            var res = _runtime.WriteAsync(address, rent.AsMemory(0, size)).AsTask().GetAwaiter().GetResult();
            return res.Success && res.BytesWritten == size;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rent);
        }
    }

    public bool WriteBytes(nint address, ReadOnlySpan<byte> source)
    {
        if (!_runtime.IsAttached || address == 0) return false;
        byte[] rent = ArrayPool<byte>.Shared.Rent(source.Length);
        try
        {
            source.CopyTo(rent);
            var res = _runtime.WriteAsync(address, rent.AsMemory(0, source.Length)).AsTask().GetAwaiter().GetResult();
            return res.Success && res.BytesWritten == source.Length;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rent);
        }
    }
}

public readonly record struct PlayerRuntimeSnapshot(
    Vector3 Position,
    Quaternion Rotation,
    CharacterViewpoint Viewpoint,
    int MapId,
    string MapName,
    bool IsMoving,
    int HoldingToolId,
    bool IsAlive,
    long TimestampTicks
);

public interface IMemorySnapshotService
{
    PlayerRuntimeSnapshot CurrentSnapshot { get; }
    void UpdateSnapshot(PlayerRuntimeSnapshot snapshot);
}

public sealed class MemorySnapshotService : IMemorySnapshotService
{
    private PlayerRuntimeSnapshot _snapshot = new(
        Vector3.Zero,
        Quaternion.Identity,
        new CharacterViewpoint(),
        MapConstants.MapPlaza,
        "Plaza",
        false,
        0,
        true,
        DateTime.UtcNow.Ticks
    );

    public PlayerRuntimeSnapshot CurrentSnapshot => _snapshot;

    public void UpdateSnapshot(PlayerRuntimeSnapshot snapshot)
    {
        _snapshot = snapshot;
    }
}
