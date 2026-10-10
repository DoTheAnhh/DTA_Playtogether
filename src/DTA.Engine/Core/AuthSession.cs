using DTA.Engine.Bots;
using DTA.Runtime.Core;

namespace DTA.Engine.Core;

/// <summary>Trạng thái phiên xác thực.</summary>
public enum AuthState { Active, Expired, Revoked, Kicked, LoggedOut }

/// <summary>
/// Phiên xác thực trung tâm + công tắc dừng toàn cục (key hết hạn / bị thu hồi / bị máy chủ đá): dừng mọi bot, gọi các callback đã đăng ký
/// (tắt đóng băng, POV, giao diện...). <see cref="Free"/> = bản miễn phí (chỉ câu cá; không POV, không cắn nhanh, không kéo cá lớn).
/// </summary>
public static class AuthSession
{
    private static readonly Logger L = Log.For("auth");
    private static readonly List<Action<string>> StopCallbacks = [];
    private static readonly object Gate = new();

    public static AuthState State { get; private set; } = AuthState.Active;
    public static string Reason { get; private set; } = "";
    public static bool Free { get; set; }

    /// <summary>Tự động hoá chỉ được chạy khi phiên đang hiệu lực.</summary>
    public static bool Active => State == AuthState.Active;

    /// <summary>Đăng ký việc cần làm khi phiên bị dừng.</summary>
    public static void OnStop(Action<string> callback)
    {
        lock (Gate) StopCallbacks.Add(callback);
    }

    /// <summary>Bật lại phiên (đăng nhập key mới).</summary>
    public static void Activate()
    {
        lock (Gate) (State, Reason) = (AuthState.Active, "");
    }

    /// <summary>Công tắc dừng toàn cục: đổi trạng thái, dừng mọi bot, gọi các callback (1 lần).</summary>
    public static void ForceLogout(string reason = "Key đã hết hạn", AuthState state = AuthState.Expired)
    {
        List<Action<string>> callbacks;
        lock (Gate)
        {
            if (State != AuthState.Active) return;
            (State, Reason) = (state, reason);
            callbacks = [.. StopCallbacks];
        }
        L.Warn($"Dừng toàn bộ: {state} ({reason})");
        BotRegistry.StopAll();
        foreach (var callback in callbacks)
        {
            try
            {
                callback(reason);
            }
            catch (Exception e)
            {
                L.Swallowed("callback dừng", e);
            }
        }
    }
}
