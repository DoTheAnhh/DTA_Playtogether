using DTA.Runtime.Core;

namespace DTA.Game.Invoke;

/// <summary>
/// Khoá hook theo tab giả lập, chung cả giữa các TIẾN TRÌNH (named mutex): mọi nơi hook cùng 1 slot vtable + 1 vùng trampoline,
/// 2 nơi hook cùng lúc là văng game (SIGSEGV trong libhoudini).
/// </summary>
public sealed class DeviceLock
{
    private static readonly Logger L = Log.For("invoke");
    private static readonly Dictionary<string, DeviceLock> Locks = new();
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);
    private readonly Mutex _mutex;
    private readonly string _serial;

    private DeviceLock(string serial)
    {
        _serial = serial;
        _mutex = new Mutex(false, $@"Local\DTA_Hook_{string.Concat(serial.Select(c => char.IsLetterOrDigit(c) ? c : '_'))}");
    }

    /// <summary>Khoá của tab (dùng chung trong tiến trình).</summary>
    public static DeviceLock For(string serial)
    {
        lock (Locks)
        {
            if (!Locks.TryGetValue(serial, out var l)) Locks[serial] = l = new DeviceLock(serial);
            return l;
        }
    }

    /// <summary>Giữ khoá tới khi Dispose (cùng luồng); quá 5 giây thì ném GameError.</summary>
    public IDisposable Acquire()
    {
        try
        {
            if (!_mutex.WaitOne(Wait)) throw new GameError("Nơi khác giữ khoá gọi hàm game quá lâu", true);
        }
        catch (AbandonedMutexException)
        {
            L.Warn($"Khoá hook {_serial} bị tiến trình khác bỏ dở - nhận lại");
        }
        return new Release(_mutex);
    }

    private sealed class Release(Mutex mutex) : IDisposable
    {
        public void Dispose() => mutex.ReleaseMutex();
    }
}
