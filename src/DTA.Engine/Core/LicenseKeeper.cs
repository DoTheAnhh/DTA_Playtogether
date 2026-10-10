using System.Text.Json.Nodes;
using DTA.Engine.Net;
using DTA.Runtime.Core;
using DTA.Runtime.Storage;

namespace DTA.Engine.Core;

/// <summary>Thông tin key đang dùng: key, ghi chú, hết hạn (giây máy chủ; 0 = không hết hạn), lệch giờ máy chủ, bản miễn phí.</summary>
public sealed record LicenseInfo(string Key, string Note, long Expires, long ClockOffset, bool Free);

/// <summary>
/// Bước key: gửi key đã lưu (hoặc xin bản miễn phí) ngay lúc mở, nhịp tim 60 s trong lúc chạy (key bị khoá / hết hạn / sang máy khác thì
/// dừng toàn bộ qua <see cref="AuthSession.ForceLogout"/>; mất mạng 10 lần liền cũng vậy), báo máy chủ trả chỗ khi tắt tool.
/// </summary>
public static class LicenseKeeper
{
    private const int Grace = 10, Retries = 2, RetryGapMs = 400;
    private static readonly TimeSpan CheckEvery = TimeSpan.FromSeconds(60);
    private static readonly string FilePath = Path.Combine(Paths.DataDir, "license.json");
    private static readonly Logger L = Log.For("license");
    private static CancellationTokenSource? _heartbeat;

    private sealed record Saved(string Key, bool Free);

    public static LicenseInfo? Current { get; private set; }

    /// <summary>Key / lựa chọn miễn phí lưu lần trước.</summary>
    public static (string Key, bool Free) Remembered()
    {
        var saved = JsonStore.Load<Saved?>(FilePath, null);
        return saved == null ? ("", false) : (saved.Key, saved.Free && saved.Key.Length == 0);
    }

    /// <summary>Nhớ lựa chọn (key vừa nhập nhớ ngay kể cả khi bị từ chối - lần sau khỏi gõ lại).</summary>
    public static void Remember(string key, bool free = false) => JsonStore.Save(FilePath, new Saved(key, free));

    /// <summary>Kích hoạt key (hoặc bản miễn phí khi key rỗng); tự thử lại 2 lần nếu hỏng. (thành công, thông báo).</summary>
    public static async Task<(bool Ok, string Message)> SignInAsync(string key, bool free, int retries = Retries)
    {
        JsonObject reply = [];
        for (var attempt = 0; attempt <= retries; attempt++)
        {
            reply = free ? await ServerClient.Default.FreeAsync() : await ServerClient.Default.ActivateAsync(key);
            if (reply["ok"]?.GetValue<bool>() == true) break;
            if (attempt < retries) await Task.Delay(RetryGapMs);
        }
        if (reply["ok"]?.GetValue<bool>() != true) return (false, reply["message"]?.GetValue<string>() is { Length: > 0 } m ? m : "Key không hợp lệ");
        Remember(free ? "" : key, free);
        var now = reply["now"]?.GetValue<long>() ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Current = new LicenseInfo(key, reply["note"]?.GetValue<string>() ?? "", reply["expires"]?.GetValue<long>() ?? 0, now - DateTimeOffset.UtcNow.ToUnixTimeSeconds(), free);
        (ServerClient.Default.Key, ServerClient.Default.Token) = (key, reply["token"]?.GetValue<string>() ?? "");
        AuthSession.Free = free;
        AuthSession.Activate();
        if (!free) StartHeartbeat();
        L.Info(free ? "Vào bản miễn phí" : $"Key hợp lệ ({Current.Note})");
        return (true, "");
    }

    /// <summary>Nhịp tim: hỏi lại máy chủ mỗi 60 s.</summary>
    private static void StartHeartbeat()
    {
        _heartbeat?.Cancel();
        var stop = _heartbeat = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            var failures = 0;
            while (!stop.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(CheckEvery, stop.Token);
                }
                catch (TaskCanceledException)
                {
                    return;
                }
                var reply = await ServerClient.Default.CheckAsync();
                var ok = reply["ok"];
                if (ok?.GetValue<bool>() == true)
                {
                    failures = 0;
                    Current = Current! with { Expires = reply["expires"]?.GetValue<long>() ?? Current.Expires };
                }
                else if (ok == null && ++failures < Grace) continue;
                else
                {
                    var reason = ok == null ? "Mất kết nối tới máy chủ key quá lâu" : reply["message"]?.GetValue<string>() ?? "Key không còn hiệu lực";
                    AuthSession.ForceLogout(reason, reason.Contains("hết hạn", StringComparison.OrdinalIgnoreCase) ? AuthState.Expired : AuthState.Revoked);
                    return;
                }
            }
        });
    }

    /// <summary>Tắt tool: dừng nhịp tim, báo máy chủ trả chỗ để máy khác dùng được key ngay.</summary>
    public static async Task ReleaseAsync()
    {
        _heartbeat?.Cancel();
        if (Current is { Free: false }) await ServerClient.Default.ReleaseAsync();
    }

    /// <summary>Đăng xuất: quên lựa chọn đã lưu (lần mở sau hiện cửa sổ nhập key).</summary>
    public static void Logout() => Remember("");
}
