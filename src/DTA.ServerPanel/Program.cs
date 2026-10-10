using System.Collections.Concurrent;
using System.IO;
using System.Windows;
using DTA.Server;
using DTA.Server.Store;

namespace DTA.ServerPanel;

/// <summary>
/// Máy chủ key + cửa sổ quản lý: mở cổng phục vụ tool ngay (không chờ đăng nhập), đăng nhập quản trị rồi mới hiện panel; đóng panel = tắt máy chủ.
/// Tham số: [--dir thư_mục_dữ_liệu] [--host 127.0.0.1 | 0.0.0.0 khi lên VPS] [--port 28445] [--headless (không giao diện, nhật ký ra server.log)].
/// </summary>
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var options = args.Select((a, i) => (a, i)).Where(p => p.a.StartsWith("--") && p.i + 1 < args.Length).ToDictionary(p => p.a[2..], p => args[p.i + 1]);
        var files = new ServerFiles(options.GetValueOrDefault("dir", AppContext.BaseDirectory));
        var host = options.GetValueOrDefault("host", "127.0.0.1");
        var port = int.TryParse(options.GetValueOrDefault("port"), out var p) ? p : files.Port;
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        using var keys = new KeyStore(files.Database);
        using var db = KeyStore.Open(files.Database);
        var tele = new TeleStore(db, files.TeleJson);
        var lines = new ConcurrentQueue<string>();
        var server = new ServerHost(files, keys, tele, host, port, lines);
        server.Start();
        if (args.Contains("--headless"))
        {
            RunHeadless(files, lines);
            return;
        }
        var signIn = new SignInWindow();
        signIn.ShowDialog();
        if (signIn.SignedIn)
        {
            var panel = new PanelWindow(keys, tele, server, lines);
            panel.Closed += (_, _) => app.Shutdown();
            panel.Show();
            app.Run();
        }
        server.Stop();
    }

    /// <summary>Không giao diện (--headless): chạy mãi tới khi bị tắt, nhật ký máy chủ ghi nối vào server.log cạnh dữ liệu.</summary>
    private static void RunHeadless(ServerFiles files, ConcurrentQueue<string> lines)
    {
        var log = Path.Combine(files.Dir, "server.log");
        while (true)
        {
            while (lines.TryDequeue(out var line)) File.AppendAllText(log, line + Environment.NewLine);
            Thread.Sleep(500);
        }
    }
}
