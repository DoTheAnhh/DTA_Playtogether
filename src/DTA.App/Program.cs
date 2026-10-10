using System.Diagnostics;
using System.Windows;
using DTA.App.Shell;
using DTA.App.Views;
using DTA.Engine.Core;
using DTA.Engine.Net;
using DTA.Engine.Popups;
using DTA.Runtime.Core;

namespace DTA.App;

/// <summary>
/// Điểm vào: bước key (key đã lưu được gửi ngay; máy chủ trả lời kịp 1.5 s thì vào thẳng tool, không thì hiện cửa sổ nhập key), nạp danh sách
/// chức năng máy chủ cho phép, mở cửa sổ chính; tắt tool thì báo máy chủ trả chỗ, đăng xuất thì mở lại tool.
/// </summary>
public static class Program
{
    private const int QuickMs = 1500;
    private static readonly Logger L = Log.For("app");

    /// <summary>Chạy thử giao diện (chỉ bản Debug, DTA_OFFLINE=1): bỏ bước key, không quét giả lập.</summary>
    public static bool Offline =>
#if DEBUG
        Environment.GetEnvironmentVariable("DTA_OFFLINE") == "1";
#else
        false;
#endif

    [STAThread]
    public static void Main()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.DispatcherUnhandledException += (_, e) =>
        {
            L.Error($"Lỗi giao diện: {e.Exception}");
#if DEBUG
            if (SelfTest.Enabled) SelfTest.Crash(e.Exception);
#endif
            e.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => L.Error($"Lỗi không bắt được: {e.ExceptionObject}");
        DTA.App.Views.Logs.LogCenter.Start();
        Risk.Reset();
        Risk.Install();
        if (!SignIn())
        {
            app.Shutdown();
            return;
        }
        var features = LoadFeatures();
#if DEBUG
        if (SelfTest.Enabled) SelfTest.Prepare();
#endif
        var window = new MainWindow(shell => Pages.Create(shell, features));
        window.Closed += (_, _) => app.Shutdown();
        window.Show();
#if DEBUG
        if (SelfTest.Enabled) window.Dispatcher.BeginInvoke(() => SelfTest.Run(window), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
#endif
        app.Run();
        PopupWatcher.Instance.Stop();
        LicenseKeeper.ReleaseAsync().Wait(TimeSpan.FromSeconds(3));
        if (window.Relaunch && Environment.ProcessPath is { } exe) Process.Start(exe);
    }

    /// <summary>Bước key; false = người dùng đóng cửa sổ nhập key.</summary>
    private static bool SignIn()
    {
        if (Offline) return true;
        var (key, free) = LicenseKeeper.Remembered();
        Task<(bool Ok, string Message)>? attempt = null;
        if (key.Length > 0 || free)
        {
            attempt = Task.Run(() => LicenseKeeper.SignInAsync(key, free));
            if (attempt.Wait(QuickMs) && attempt.Result.Ok) return true;
        }
        var login = new LoginWindow(key, attempt, free);
        login.ShowDialog();
        return login.SignedIn;
    }

    /// <summary>Các chức năng máy chủ bật (bản trả phí); lỗi / bản miễn phí thì dùng mặc định.</summary>
    private static List<Feature> LoadFeatures()
    {
        if (AuthSession.Free || LicenseKeeper.Current == null) return Pages.Defaults;
        try
        {
            var reply = Task.Run(ServerClient.Default.FeaturesAsync);
            return (reply.Wait(TimeSpan.FromSeconds(5)) && reply.Result["ok"]?.GetValue<bool>() == true ? Pages.Parse(reply.Result["features"]) : null) ?? Pages.Defaults;
        }
        catch (Exception e)
        {
            L.Swallowed("tải danh sách chức năng", e);
            return Pages.Defaults;
        }
    }
}
