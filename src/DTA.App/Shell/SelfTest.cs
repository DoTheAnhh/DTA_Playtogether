#if DEBUG
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DTA.Runtime.Core;
using DTA.Runtime.Storage;
using DTA.Ui;

namespace DTA.App.Shell;

/// <summary>
/// Tự kiểm giao diện (chỉ bản Debug, DTA_SELFTEST=1): mở từng tab, bấm lần lượt mọi nút / công tắc / lựa chọn đang hiện, rồi bấm ngẫu nhiên
/// xen đổi tab (combo); bảng phụ bật lên thì tự đóng. Ghi mọi lỗi giao diện / log lỗi kèm chuỗi bấm ngay trước đó vào runtime/logs/selftest.txt
/// rồi tắt tool. Không bấm đăng xuất / thoát.
/// </summary>
public static class SelfTest
{
    private static readonly string[] Deny = ["Đăng xuất", "Thoát", "Đăng xuất key"];
    private static readonly List<string> Trail = [];
    private static readonly StringBuilder Report = new();
    private static int _errors, _clicks;

    public static bool Enabled => Environment.GetEnvironmentVariable("DTA_SELFTEST") == "1";

    /// <summary>Bật ghi phần tử bấm được - gọi TRƯỚC khi dựng cửa sổ chính.</summary>
    public static void Prepare()
    {
        Interact.Record = true;
        Log.Added += entry =>
        {
            if (entry.Level != LogLevel.Error) return;
            lock (Report) Report.AppendLine($"[LOG LỖI] {entry.Channel}: {entry.Message}\n    sau: {string.Join(" > ", Trail.TakeLast(6))}");
            Interlocked.Increment(ref _errors);
        };
    }

    /// <summary>Lỗi giao diện không bắt được (từ Application.DispatcherUnhandledException).</summary>
    public static void Crash(Exception e)
    {
        lock (Report) Report.AppendLine($"[LỖI GIAO DIỆN] {e}\n    sau: {string.Join(" > ", Trail.TakeLast(6))}");
        Interlocked.Increment(ref _errors);
    }

    /// <summary>Chạy toàn bộ kịch bản trên luồng giao diện rồi tắt tool.</summary>
    public static async void Run(MainWindow window)
    {
        var closer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        closer.Tick += (_, _) =>
        {
            foreach (var other in Application.Current.Windows.OfType<Window>().Where(w => w != window && w.IsVisible).ToList())
            {
                Trail.Add($"(đóng bảng {other.Title})");
                other.Close();
            }
        };
        closer.Start();
        var tabs = window.Views.SelectMany(v => v.Tabs.Select(t => t.Key)).ToList();
        var random = new Random(7);
        foreach (var tab in tabs)
        {
            await Open(window, tab);
            foreach (var (element, enabled, click) in Visible())
                await Click(element, enabled, click, 120);
        }
        for (var i = 0; i < 400; i++)
        {
            if (i % 25 == 0) await Open(window, tabs[random.Next(tabs.Count)]);
            var items = Visible().ToList();
            if (items.Count == 0) continue;
            var (element, enabled, click) = items[random.Next(items.Count)];
            await Click(element, enabled, click, random.Next(0, 3) == 0 ? 0 : random.Next(30, 400));
        }
        foreach (var tab in tabs) await Open(window, tab);
        await Task.Delay(3000);
        closer.Stop();
        var path = Path.Combine(Paths.LogDir, "selftest.txt");
        lock (Report) File.WriteAllText(path, $"Tự kiểm giao diện: {_clicks} lần bấm, {tabs.Count} tab, {_errors} lỗi\n\n{Report}");
        window.Close();
    }

    /// <summary>Mở 1 tab rồi chờ dựng xong.</summary>
    private static async Task Open(MainWindow window, string tab)
    {
        Trail.Add($"[tab {tab}]");
        try
        {
            window.ShowTab(tab);
        }
        catch (Exception e)
        {
            Crash(e);
        }
        await Task.Delay(400);
    }

    /// <summary>Phần tử bấm được đang hiện trên màn (trừ đăng xuất / thoát).</summary>
    private static IEnumerable<(FrameworkElement Element, Func<bool> Enabled, Action Click)> Visible() =>
        Interact.Recorded.Where(r => r.Element.IsVisible && !Deny.Contains(Label(r.Element))).ToList();

    /// <summary>Bấm 1 phần tử (nếu đang bấm được) rồi chờ <paramref name="wait"/> ms.</summary>
    private static async Task Click(FrameworkElement element, Func<bool> enabled, Action click, int wait)
    {
        if (!element.IsVisible || !enabled()) return;
        Trail.Add(Label(element));
        _clicks++;
        try
        {
            click();
        }
        catch (Exception e)
        {
            Crash(e);
        }
        await Task.Delay(wait);
    }

    /// <summary>Chữ hiển thị trên phần tử (TextBlock đầu tiên bên trong), không có thì tên kiểu + toạ độ.</summary>
    private static string Label(FrameworkElement element)
    {
        var queue = new Queue<DependencyObject>([element]);
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            if (node is TextBlock { Text.Length: > 0 } text) return text.Text;
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) queue.Enqueue(VisualTreeHelper.GetChild(node, i));
        }
        return $"{element.GetType().Name}@{Canvas.GetLeft(element):F0},{Canvas.GetTop(element):F0}";
    }
}
#endif
