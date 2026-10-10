using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using DTA.Ui;
using DTA.App.Views;
using DTA.Engine.Core;
using DTA.Engine.Popups;
using DTA.Runtime.Core;

namespace DTA.App.Shell;

/// <summary>
/// Cửa sổ chính: cột menu chức năng bên trái; bên phải là trang của chức năng đang chọn (tiêu đề, hàng tab, chọn giả lập + tab giả lập, nội
/// dung, chân trang). Toàn bộ vẽ trên 1 <see cref="Board"/> toạ độ tuyệt đối như canvas của bản Tkinter.
/// </summary>
public sealed partial class MainWindow : Window
{
    private static readonly Logger L = Log.For("ui");
    public Board Board { get; } = new(Layout.WinW, Layout.WinH);
    public AppSettings Settings { get; } = AppSettings.Current;
    public IReadOnlyList<PageView> Views { get; private set; } = [];
    public PageView CurrentView { get; private set; } = null!;
    /// <summary>Người dùng bấm Đăng xuất key / Nhập key: tắt xong thì mở lại tool (hiện cửa sổ nhập key).</summary>
    public bool Relaunch { get; private set; }

    private CNav _nav = null!;
    private CanvasText _title = null!, _subtitle = null!, _pillText = null!, _license = null!;
    private CButton _help = null!;
    private Border _pill = null!;

    public MainWindow(Func<MainWindow, IReadOnlyList<PageView>> pages)
    {
        Title = Layout.AppName;
        ResizeMode = ResizeMode.CanMinimize;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Theme.Brush(Theme.Bg);
        UseLayoutRounding = true;
        Icon = Icons.App(64);
        DarkTitle.Apply(this);
        Content = Board.Canvas;
        if (!Settings.RememberOptions) AppSettings.ResetOptions();
        // Bấm ra ngoài ô nhập là thôi gõ ô đó
        Board.Canvas.Focusable = true;
        Board.Canvas.MouseLeftButtonDown += (_, e) => { if (e.OriginalSource == Board.Canvas || e.OriginalSource is not System.Windows.Controls.Primitives.TextBoxBase) Keyboard.Focus(Board.Canvas); };
        DrawBackground();
        Views = pages(this);
        CurrentView = Views.FirstOrDefault(v => v.Name == Settings.Page) ?? Views[0];
        BuildNav();
        BuildHeader();
        BuildBar();
        Board.Line(Layout.Left, Layout.FootTop, Layout.Right, Layout.FootTop, Theme.Divider);
        SetupTabs();
        Closing += (_, _) => OnClosing();
        Loaded += (_, _) =>
        {
            RefreshDevices();
            StartLicenseClock();
            PrebuildLater();
        };
        AuthSession.OnStop(reason => Ui(() => OnAuthStop(reason)));
    }

    /// <summary>Gọi trên luồng giao diện (dùng từ luồng khác).</summary>
    public void Ui(Action action)
    {
        if (Dispatcher.HasShutdownStarted) return;
        Dispatcher.BeginInvoke(action);
    }

    /// <summary>Nền chuyển màu + 2 quầng sáng mờ cùng tông màu nhấn; cột menu sáng hơn 1 chút, có vạch ngăn.</summary>
    private void DrawBackground()
    {
        var canvas = Board.Canvas;
        canvas.Background = new LinearGradientBrush(Theme.Hex("#0a0f1c"), Theme.Hex("#0d1727"), 90);
        Ellipse Glow(Color color, byte alpha, double x1, double y1, double x2, double y2)
        {
            var brush = new RadialGradientBrush(color with { A = alpha }, color with { A = 0 });
            brush.GradientStops.Insert(1, new GradientStop(color with { A = (byte)(alpha * 0.55) }, 0.45));
            return Board.Add(new Ellipse { Width = x2 - x1, Height = y2 - y1, Fill = brush, IsHitTestVisible = false }, x1, y1);
        }
        // Ellipse gốc vẽ ở 1/4 cỡ rồi làm nhoè 20 px: nở rộng thêm ~quầng nhoè
        Glow(Theme.Hex("#38bdf8"), 80, -300, -380, 460, 290);
        Glow(Theme.Hex("#2f8fe6"), 70, 780, 460, 1500, 1060);
        Board.Add(new Rectangle { Width = Layout.NavW, Height = Layout.WinH, Fill = Theme.Brush(Theme.White(8)), IsHitTestVisible = false }, 0, 0);
        Board.Line(Layout.NavW + 0.5, 0, Layout.NavW + 0.5, Layout.WinH, Theme.Divider);
    }

    /// <summary>Cột trái: tên tool + menu các chức năng + thời hạn key + nút đăng xuất.</summary>
    private void BuildNav()
    {
        Board.Add(new Image { Source = Icons.App(), Width = 40, Height = 40 }, 16, 18);
        Board.Text(66, 17, "DTA", Anchor.NW, Theme.Text, 14, true);
        Board.Text(67, 42, "Playtogether", Anchor.NW, Theme.Muted, 9);
        Board.Text(26, Layout.BarTop + 17, "Chức năng", Anchor.W, Theme.Dim, 8, true);
        _nav = new CNav(Board, 12, Layout.ContentTop, Layout.NavW - 24, Views.Select(v => (v.Name, v.Name, v.Icon)).ToList(), OnNavSelect);
        _nav.SetEnabled(false);
        _license = Board.Text(24, Layout.WinH - 52, "", Anchor.W, Theme.Dim, 8);
        if (LicenseKeeper.Current == null) return;
        new CButton(Board, 16, Layout.WinH - 38, Layout.NavW - 32, 26, AuthSession.Free ? "Nhập key" : "Đăng xuất key", Logout,
            Theme.GhostFill, Theme.GhostHover, Theme.Muted, 13, 8);
    }

    private void OnNavSelect(string name)
    {
        var view = Views.First(v => v.Name == name);
        ShowTab(_viewTab[view]);
    }

    /// <summary>Tiêu đề trang bên trái (+ nút ?), công tắc ghim cửa sổ + huy hiệu trạng thái bên phải.</summary>
    private void BuildHeader()
    {
        _title = Board.Text(Layout.Left, 15, "", Anchor.NW, Theme.Text, 15, true);
        _subtitle = Board.Text(Layout.Left + 1, 43, "", Anchor.NW, Theme.Muted, 8);
        _help = new CButton(Board, Layout.Left + 160, 16, 24, 24, "?", OpenHelp, new Fill(Theme.White(14), null, Theme.White(45)),
            new Fill(Theme.Accent with { A = 40 }, null, Theme.Accent with { A = 180 }), Theme.Accent, 12, 10);
        const double pillW = 172, pillH = 32, pillX = Layout.Right - pillW, pillY = 22;
        _pill = Board.Round(pillX, pillY, pillW, pillH, 16, new Fill(Theme.White(14), null, Theme.White(40)));
        _pillText = Board.Text(pillX + pillW / 2, pillY + pillH / 2, "●  Chưa chọn tab", Anchor.Center, Theme.Muted, 9, true);
        new CToggle(Board, pillX - 16 - 34, pillY + 7, "Ghim trên cùng", on => Topmost = on, false, Theme.Muted, true);
    }

    private void OpenHelp() => HelpDialog.Open(this, CurrentView, _viewTab.GetValueOrDefault(CurrentView, ""));

    /// <summary>1 chức năng vừa bắt đầu / ngừng chạy: huy hiệu ghi việc đang làm và khoá chọn giả lập trong lúc chạy.</summary>
    public void SetRunning(bool running, string doing = "")
    {
        _running = running;
        _refresh.SetEnabled(!running);
        _emulatorBox.SetEnabled(!running);
        _instanceBox.SetEnabled(!running);
        Board.Paint(_pill, running ? new Fill(Theme.Accent with { A = 40 }, null, Theme.Accent with { A = 150 }) : new Fill(Theme.White(14), null, Theme.White(40)));
        _pillText.Set(running ? $"●  {(doing.Length > 0 ? doing : "Đang chạy")}" : "●  Đã dừng", running ? Theme.Accent : Theme.Muted);
    }

    /// <summary>Chức năng <paramref name="view"/> sắp bật: dừng mọi chức năng điều khiển nhân vật khác (ESP chỉ xem nên vẫn chạy song song).</summary>
    public void StopOthers(PageView view)
    {
        foreach (var other in Views.Where(v => v != view))
        {
            try
            {
                other.Halt();
            }
            catch (Exception e)
            {
                L.Swallowed($"dừng {other.Name}", e);
            }
        }
    }

    /// <summary>Công tắc dừng toàn cục (key hết hạn / bị thu hồi): đưa mọi trang về đã dừng; không phải tự đăng xuất thì báo rồi đóng tool.</summary>
    private void OnAuthStop(string reason)
    {
        _running = false;
        _pillText.Set("●  Đã dừng", Theme.Muted);
        foreach (var view in Views)
        {
            try
            {
                view.OnAuthStop(reason);
            }
            catch (Exception e)
            {
                L.Swallowed($"dừng {view.Name}", e);
            }
        }
        if (AuthSession.State == AuthState.LoggedOut) return;
        MessageBox.Show(this, $"{reason}.\nTool sẽ đóng lại.", Layout.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
        Close();
    }

    /// <summary>Bỏ key đang dùng: tool đóng lại rồi tự mở cửa sổ nhập key.</summary>
    private void Logout()
    {
        var question = AuthSession.Free ? "Nhập key để mở toàn bộ chức năng?\nTool sẽ đóng lại rồi mở cửa sổ nhập key."
            : "Đăng xuất key này?\nTool sẽ đóng lại rồi mở cửa sổ nhập key mới.";
        if (MessageBox.Show(this, question, Layout.AppName, MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        AuthSession.ForceLogout("Người dùng đăng xuất", AuthState.LoggedOut);
        LicenseKeeper.Logout();
        Relaunch = true;
        Dispatcher.BeginInvoke(Close);
    }

    private void OnClosing()
    {
        PopupWatcher.Instance.Stop();
        foreach (var view in Built())
        {
            try
            {
                view.OnClose();
            }
            catch (Exception e)
            {
                L.Swallowed($"đóng {view.Name}", e);
            }
        }
    }

    /// <summary>Báo 1 dòng cho mọi trang đã dựng.</summary>
    public void Notify(string text, Color color)
    {
        foreach (var view in Built()) view.ShowMessage(text, color);
    }
}
