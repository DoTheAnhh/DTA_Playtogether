using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using DTA.Server.Store;
using DTA.Ui;

namespace DTA.ServerPanel;

/// <summary>
/// Cửa sổ quản lý máy chủ (1180x680): tab Quản lý key (tạo / sửa / gia hạn / khoá / gỡ máy / xoá, lọc theo trạng thái, tìm, nhật ký) và tab Quản lý
/// vị trí TELE (thêm / sửa / bật tắt / xoá vị trí chuẩn cho mọi tool). Danh sách key tự nạp lại mỗi 3 s; đóng cửa sổ = tắt máy chủ.
/// </summary>
public sealed partial class PanelWindow : Window
{
    public static readonly Color Card = Theme.Hex("#111a2c"), Button = Theme.Hex("#172238"), ButtonHover = Theme.Hex("#1e2c47"), Sky = Theme.Hex("#38bdf8"),
        Good = Theme.Hex("#4ade80"), Ink = Theme.Hex("#06121f"), Hint = Theme.Hex("#5f6b82");
    private const double W = 1180, H = 680;
    private readonly Board _board = new(W, H);
    private readonly KeyStore _keys;
    private readonly TeleStore _tele;
    private readonly ServerHost _server;
    private readonly ConcurrentQueue<string> _lines;
    private CButton _keysTab = null!, _teleTab = null!;
    private CanvasText _status = null!;
    private List<UIElement> _keysItems = [], _teleItems = [];

    public PanelWindow(KeyStore keys, TeleStore tele, ServerHost server, ConcurrentQueue<string> lines)
    {
        (_keys, _tele, _server, _lines) = (keys, tele, server, lines);
        Title = "DTA Playtogether - SERVER CONTROL PANEL";
        (ResizeMode, SizeToContent, WindowStartupLocation, UseLayoutRounding) = (ResizeMode.CanMinimize, SizeToContent.WidthAndHeight, WindowStartupLocation.CenterScreen, true);
        _board.Canvas.Background = Theme.Brush(Theme.Hex("#0b1220"));
        Content = _board.Canvas;
        DarkTitle.Apply(this);
        _board.Text(18, 27, "DTA Playtogether", Anchor.W, Theme.Text, 14, true);
        _keysTab = new CButton(_board, 210, 12, 236, 30, "🔑  QUẢN LÝ KEY / BẢN QUYỀN", () => SwitchTab(true), new Fill(Sky), new Fill(Sky), Ink, 6, 9);
        _teleTab = new CButton(_board, 454, 12, 206, 30, "📍  QUẢN LÝ VỊ TRÍ TELE", () => SwitchTab(false), new Fill(Button), new Fill(ButtonHover), Theme.Muted, 6, 9);
        _status = _board.Text(W - 18, 27, "", Anchor.E, Good, 9, true);
        _keysItems = _board.Collect(BuildKeys);
        _teleItems = _board.Collect(BuildTele);
        SwitchTab(true);
        ShowServer();
        _server.Changed += () => Dispatcher.BeginInvoke(ShowServer);
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        timer.Tick += (_, _) => ReloadKeys();
        timer.Start();
        var pump = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        pump.Tick += (_, _) => PumpLog();
        pump.Start();
    }

    /// <summary>Nút thao tác kiểu panel: nền xanh đen, chữ màu riêng.</summary>
    private CButton Action(double x, double y, double w, string text, System.Action command, Color? color = null) =>
        new(_board, x, y, w, 28, text, command, new Fill(Button), new Fill(ButtonHover), color ?? Sky, 6, 9);

    private void SwitchTab(bool keys)
    {
        foreach (var item in _keysItems) item.Visibility = keys ? Visibility.Visible : Visibility.Hidden;
        foreach (var item in _teleItems) item.Visibility = keys ? Visibility.Hidden : Visibility.Visible;
        Paint(_keysTab, keys);
        Paint(_teleTab, !keys);
        if (!keys) ReloadTele();
    }

    private static void Paint(CButton button, bool selected) => button.Restyle(selected ? new Fill(Sky) : new Fill(Button), selected ? new Fill(Sky) : new Fill(ButtonHover), selected ? Ink : Theme.Muted);

    private void ShowServer() => _status.Set(_server.Running ? $"●  Máy chủ đang chạy - cổng {_server.Port}" : "●  MÁY CHỦ CHƯA CHẠY (xem nhật ký) - đang thử lại...",
        _server.Running ? Good : Theme.Danger);

    /// <summary>Ghi 1 dòng thao tác quản trị vào nhật ký.</summary>
    private void Note(string action) => _lines.Enqueue($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  QUẢN LÝ: {action}");

    private void PumpLog()
    {
        var fresh = new List<(string, Color)>();
        while (_lines.TryDequeue(out var line)) fresh.Add((line, Theme.Muted));
        if (fresh.Count > 0) _log.Append(fresh);
    }

    protected override void OnClosed(EventArgs e)
    {
        _server.Stop();
        base.OnClosed(e);
    }
}
