using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using DTA.App.Shell;
using DTA.Ui;
using DTA.Runtime.Core;
using DTA.Runtime.Storage;
using Microsoft.Win32;

namespace DTA.App.Views.Logs;

/// <summary>
/// Trang Log: tab Tất cả / Hệ thống / từng chức năng (tiêu đề kèm số lỗi chưa xem), lọc mức + tìm chữ, tạm dừng cuộn, xoá màn hình, xuất log, mở
/// thư mục log, tự kiểm tra lỗi, xuất gói báo lỗi (.zip). Xả bản ghi mới theo lô mỗi 150 ms.
/// </summary>
public sealed class LogView(MainWindow app) : PageView(app)
{
    private static readonly string[] Levels = ["Tất cả", "DEBUG", "INFO", "WARN", "ERROR"];
    private string _tab = LogCenter.All;
    private CSegment<string> _tabs = null!;
    private CCombo _level = null!;
    private CEntry _search = null!;
    private CButton _scroll = null!;
    private LogBox _box = null!;
    private CanvasText _stats = null!;

    public override string Name => "Log";
    public override string Icon => "log";
    public override string Subtitle => "Xem nhật ký hệ thống và tự kiểm tra lỗi theo từng chức năng";
    public override IReadOnlyList<(string Key, string Label)> Tabs => [("log_center", "Nhật ký")];

    public override void BuildTab(string tab)
    {
        const double left = Layout.Left, right = Layout.Right, top = Layout.ContentTop, bottom = Layout.ContentBottom;
        Board.Cards((left, top, right - left, bottom - top));
        Board.Text(left + 16, top + 22, "NHẬT KÝ HỆ THỐNG & CHỨC NĂNG", Anchor.W, Theme.Text, 10, true);
        new CButton(Board, right - 16 - 120, top + 8, 120, 28, "📁 Thư mục log", OpenFolder, Theme.GhostFill, Theme.GhostHover, Theme.Text, 14, 8);
        new CButton(Board, right - 16 - 120 - 8 - 178, top + 8, 178, 28, "📦 Xuất gói báo lỗi (.zip)", ExportBundle, Theme.GhostFill, Theme.GhostHover, Theme.Text, 14, 8);
        _tabs = new CSegment<string>(Board, left + 16, top + 44, right - left - 32, LogCenter.Tabs.Select(t => (t.Key, t.Title)).ToList(), key =>
        {
            _tab = key;
            Reload();
        });
        const double row = top + 96;
        Board.Text(left + 16, row, "Mức", Anchor.W, Theme.Muted, 8, true);
        _level = new CCombo(Board, left + 48, row - 14, 110, 28, Levels, Reload);
        _level.Select(0);
        Board.Text(left + 174, row, "Tìm", Anchor.W, Theme.Muted, 8, true);
        _search = new CEntry(Board, left + 204, row - 14, 220, 28, "Gõ chữ cần tìm trong log", Reload);
        _scroll = new CButton(Board, left + 438, row - 14, 92, 28, "⏸ Cuộn", ToggleScroll, Theme.GhostFill, Theme.GhostHover, Theme.Text, 14, 8);
        new CButton(Board, left + 538, row - 14, 76, 28, "🧹 Xoá", () =>
        {
            _box.Clear();
            ShowStats();
        }, Theme.GhostFill, Theme.GhostHover, Theme.Text, 14, 8);
        new CButton(Board, left + 622, row - 14, 76, 28, "💾 Xuất", ExportTab, Theme.GhostFill, Theme.GhostHover, Theme.Text, 14, 8);
        new CButton(Board, right - 16 - 130, row - 14, 130, 28, "🔍 Kiểm tra lỗi", Diagnose, Theme.SecondaryFill, Theme.SecondaryHover, Theme.Text, 14, 8);
        _box = new LogBox(Board, left + 16, row + 22, right - left - 32, bottom - row - 22 - 34);
        _stats = Board.Text(left + 16, bottom - 16, "", Anchor.W, Theme.Muted, 8);
        Reload();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        timer.Tick += (_, _) => Flush();
        timer.Start();
    }

    public override void BuildFooter() => Board.Text(Layout.Left, Layout.FootTop + 26, "Nhật ký được lưu tự động trong runtime/logs/ và tự xoay vòng khi đầy.", Anchor.W, Theme.Muted, 8);

    private static Color ColorOf(LogLevel level) => level switch
    {
        LogLevel.Debug => Theme.Hex("#808080"),
        LogLevel.Warn => Theme.Hex("#FFCC00"),
        LogLevel.Error => Theme.Hex("#FF4444"),
        _ => Theme.Hex("#D4D4D4"),
    };

    private bool InTab(LogEntry e) => _tab == LogCenter.All || LogCenter.TabOf(e.Channel) == _tab;

    private bool Matches(LogEntry e)
    {
        if (!InTab(e)) return false;
        if (_level.Index > 0 && e.Level.ToString().ToUpperInvariant() != Levels[_level.Index]) return false;
        var text = _search.Text.Trim();
        return text.Length == 0 || e.Message.Contains(text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Vẽ lại tab đang xem từ bộ nhớ log (2000 dòng gần nhất).</summary>
    private void Reload()
    {
        if (_box == null) return;
        _box.Clear();
        _box.Append(DTA.Runtime.Core.Log.Snapshot().Where(Matches).Select(e => (e.ToString(), ColorOf(e.Level))));
        if (App.CurrentView == this) LogCenter.MarkRead(_tab);
        ShowStats();
        ShowBadges();
    }

    /// <summary>Xả bản ghi mới theo lô; tab đang mở thì coi như đã đọc lỗi của nó.</summary>
    private void Flush()
    {
        var fresh = LogCenter.Drain();
        if (fresh.Count > 0) _box.Append(fresh.Where(Matches).Select(e => (e.ToString(), ColorOf(e.Level))));
        if (App.CurrentView == this) LogCenter.MarkRead(_tab);
        if (fresh.Count > 0) ShowStats();
        ShowBadges();
    }

    private void ShowBadges()
    {
        foreach (var (key, title) in LogCenter.Tabs)
        {
            var unread = key == LogCenter.All ? 0 : LogCenter.UnreadOf(key);
            _tabs.SetLabel(key, unread > 0 ? $"{title} ({unread})" : title);
        }
    }

    private void ShowStats()
    {
        var entries = DTA.Runtime.Core.Log.Snapshot().Where(InTab).ToList();
        _stats.Text = $"{_box.Count} dòng | {entries.Count(e => e.Level == LogLevel.Error)} Lỗi | {entries.Count(e => e.Level == LogLevel.Warn)} Cảnh báo";
    }

    private void ToggleScroll()
    {
        _box.AutoScroll = !_box.AutoScroll;
        _scroll.Text = _box.AutoScroll ? "⏸ Cuộn" : "▶ Cuộn";
    }

    private static void OpenFolder() => Process.Start(new ProcessStartInfo("explorer.exe", Paths.LogDir) { UseShellExecute = true });

    private void ExportTab()
    {
        var dialog = new SaveFileDialog { DefaultExt = ".txt", Filter = "Text files|*.txt", FileName = $"log_{_tab}.txt" };
        if (dialog.ShowDialog(App) != true) return;
        File.WriteAllLines(dialog.FileName, _box.Texts);
        MessageBox.Show(App, $"Đã xuất log ra:\n{dialog.FileName}", "Thành công");
    }

    /// <summary>Đóng gói thư mục runtime/logs thành 1 file zip để gửi dev.</summary>
    private void ExportBundle()
    {
        var dialog = new SaveFileDialog { DefaultExt = ".zip", Filter = "Zip Archive|*.zip", FileName = "DTA_BugReport.zip" };
        if (dialog.ShowDialog(App) != true) return;
        try
        {
            if (File.Exists(dialog.FileName)) File.Delete(dialog.FileName);
            using var zip = ZipFile.Open(dialog.FileName, ZipArchiveMode.Create);
            foreach (var file in Directory.EnumerateFiles(Paths.LogDir, "*", SearchOption.AllDirectories))
            {
                using var source = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var target = zip.CreateEntry(Path.GetRelativePath(Paths.LogDir, file)).Open();
                source.CopyTo(target);
            }
            MessageBox.Show(App, $"Đã xuất gói báo cáo lỗi ra:\n{dialog.FileName}", "Thành công");
        }
        catch (Exception e)
        {
            MessageBox.Show(App, $"Không thể tạo gói báo lỗi: {e.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>Tóm tắt lỗi + cảnh báo của tab theo kênh và nội dung (bỏ số để gom lỗi giống nhau).</summary>
    private void Diagnose()
    {
        var title = LogCenter.Tabs.First(t => t.Key == _tab).Title;
        var groups = DTA.Runtime.Core.Log.Snapshot().Where(e => InTab(e) && e.Level >= LogLevel.Warn)
            .GroupBy(e => $"{e.Level.ToString().ToUpperInvariant()} [{e.Channel}] {new string(e.Message.Select(c => char.IsDigit(c) ? '#' : c).Take(70).ToArray())}")
            .OrderByDescending(g => g.Count()).ToList();
        if (groups.Count == 0)
        {
            MessageBox.Show(App, $"Không có lỗi nào được ghi nhận trong kênh [{title}]!", "Kiểm tra lỗi");
            return;
        }
        var text = $"TỔNG KẾT LỖI KÊNH [{title}]:\n\n" + string.Join("\n\n", groups.Select(g => $"• {g.Key}: {g.Count()} lần\n  Mẫu: {g.Last().Message}"));
        ReportDialog.Show(App, $"Báo cáo lỗi: {title}", text);
    }
}
