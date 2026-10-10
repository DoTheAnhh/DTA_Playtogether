using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using DTA.Server;
using DTA.Server.Http;
using DTA.Server.Store;
using Microsoft.AspNetCore.Builder;

namespace DTA.ServerPanel;

/// <summary>Giữ máy chủ HTTPS chạy nền: mở cổng, chưa mở được (cổng bận / thiếu file) thì ghi nhật ký và thử lại mỗi 5 giây.</summary>
public sealed class ServerHost(ServerFiles files, KeyStore keys, TeleStore tele, string host, int port, ConcurrentQueue<string> lines)
{
    private WebApplication? _app;
    private string _lastError = "";
    private CancellationTokenSource _stop = new();

    public int Port => port;
    public bool Running => _app != null;
    /// <summary>Trạng thái đổi (đang chạy / chưa chạy).</summary>
    public event Action? Changed;

    private static string Stamp => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

    public void Start() => _ = Run(_stop.Token);

    private async Task Run(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested && _app == null)
        {
            try
            {
                _app = await ApiHost.StartAsync(files, keys, tele, host, port, lines.Enqueue);
                lines.Enqueue($"{Stamp}  máy chủ bắt đầu nhận kết nối ở cổng {port}");
                Changed?.Invoke();
                return;
            }
            catch (Exception e)
            {
                var busy = e is IOException { InnerException: SocketException } or SocketException;
                var reason = busy ? $"cổng {port} đang bị chương trình khác dùng" : $"thiếu hoặc hỏng file trong {files.Dir} ({e.Message})";
                if (reason != _lastError) lines.Enqueue($"{Stamp}  CHƯA MỞ ĐƯỢC MÁY CHỦ: {reason}");
                _lastError = reason;
                Changed?.Invoke();
            }
            try
            {
                await Task.Delay(5000, stop);
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }
    }

    public void Stop()
    {
        _stop.Cancel();
        _app?.StopAsync().Wait(TimeSpan.FromSeconds(3));
        _app = null;
    }
}
