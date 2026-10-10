using DTA.Runtime.Core;
using DTA.Runtime.Device;

namespace DTA.Game.Session;

/// <summary>
/// Giữ 1 <see cref="GameSession"/> cho mỗi tab giả lập; mọi chức năng xin phiên ở đây thay vì tự kết nối (trước đây 9 nơi tự
/// kết nối riêng: 9 phiên su, 9 bộ đệm). Phiên chết (game tắt / đổi pid) thì tự đóng và tạo lại ở lần xin sau.
/// </summary>
public static class SessionHub
{
    private static readonly Logger L = Log.For("session");
    private static readonly Dictionary<string, GameSession> Sessions = new();
    private static readonly Dictionary<string, object> Locks = new();

    /// <summary>Phát khi 1 phiên mới được tạo (luồng nền dùng để làm nóng).</summary>
    public static event Action<GameSession>? Opened;

    /// <summary>Phiên game của tab; chưa có / đã chết thì kết nối mới (có thể ném GameError khi game chưa mở).</summary>
    public static GameSession Get(EmulatorDevice device, Action<string>? progress = null)
    {
        lock (LockOf(device.Serial))
        {
            GameSession? current;
            lock (Sessions) Sessions.TryGetValue(device.Serial, out current);
            if (current != null && current.Alive()) return current;
            if (current != null)
            {
                L.Warn($"Phiên {device.Serial} đã chết - kết nối lại");
                Close(device.Serial);
            }
            var session = new GameSession(device, progress);
            lock (Sessions) Sessions[device.Serial] = session;
            Opened?.Invoke(session);
            return session;
        }
    }

    /// <summary>Phiên đang mở của tab (không kết nối mới).</summary>
    public static GameSession? Peek(string serial)
    {
        lock (Sessions) return Sessions.GetValueOrDefault(serial);
    }

    /// <summary>Đóng phiên của tab (đổi tab / tắt tool).</summary>
    public static void Close(string serial)
    {
        GameSession? session;
        lock (Sessions)
        {
            if (!Sessions.Remove(serial, out session)) return;
        }
        try { session.Dispose(); } catch (Exception e) { L.Swallowed("đóng phiên", e); }
    }

    public static void CloseAll()
    {
        List<string> serials;
        lock (Sessions) serials = Sessions.Keys.ToList();
        foreach (var s in serials) Close(s);
    }

    private static object LockOf(string serial)
    {
        lock (Locks)
        {
            if (!Locks.TryGetValue(serial, out var l)) Locks[serial] = l = new object();
            return l;
        }
    }
}
