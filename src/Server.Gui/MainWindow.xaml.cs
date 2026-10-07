using System.Diagnostics;
using System.Windows;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using DTA.Server;

namespace DTA.Server.Gui;

public partial class MainWindow : Window
{
    private WebApplication? _webApp;
    private CancellationTokenSource? _cts;

    public MainWindow()
    {
        InitializeComponent();
        AddLog("[Khởi động] Đang thiết lập ASP.NET Core Kestrel Engine...");
        StartBackgroundServer();
    }

    private void StartBackgroundServer()
    {
        _cts = new CancellationTokenSource();
        Task.Run(async () =>
        {
            try
            {
                _webApp = ServerHost.BuildServer();
                _webApp.Urls.Add("http://0.0.0.0:5000");

                Dispatcher.Invoke(() =>
                {
                    AddLog("[Thành công] Server đã lắng nghe tại: http://localhost:5000");
                    AddLog("[Endpoint] API Dịch chuyển: http://localhost:5000/api/teleport/positions");
                    AddLog("[Endpoint] API Giao diện động: http://localhost:5000/api/ui/navigation");
                    AddLog("[Endpoint] Swagger Documentation: http://localhost:5000/swagger");
                });

                await _webApp.StartAsync(_cts.Token);
                await _webApp.WaitForShutdownAsync(_cts.Token);
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    AddLog($"[Lỗi máy chủ] {ex.Message}");
                });
            }
        });
    }

    private void AddLog(string msg)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        ListLogs.Items.Insert(0, $"[{timestamp}] {msg}");
        if (ListLogs.Items.Count > 100)
            ListLogs.Items.RemoveAt(ListLogs.Items.Count - 1);
    }

    private void BtnOpenSwagger_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "http://localhost:5000/swagger",
                UseShellExecute = true
            });
            AddLog("[Người dùng] Đã mở Swagger UI trên trình duyệt.");
        }
        catch (Exception ex)
        {
            AddLog($"[Lỗi mở trình duyệt] {ex.Message}");
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _cts?.Cancel();
        _webApp?.StopAsync();
        base.OnClosed(e);
    }
}
