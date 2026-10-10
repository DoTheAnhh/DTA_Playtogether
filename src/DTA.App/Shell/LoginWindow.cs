using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DTA.Ui;
using DTA.Engine.Core;
using DTA.Engine.Net;

namespace DTA.App.Shell;

/// <summary>
/// Cửa sổ nhập key (460x470): ô key, [Kích hoạt key], dòng trạng thái, "hoặc", [Dùng bản miễn phí], mã máy. Có thể theo tiếp 1 lượt gửi key
/// đã lưu đang chờ máy chủ. Đóng cửa sổ khi chưa vào được = thoát tool.
/// </summary>
public sealed class LoginWindow : Window
{
    private const double W = 460, H = 470, Pad = 36;
    private static readonly Color Card = Theme.Hex("#111a2c"), FieldBack = Theme.Hex("#0c1422"), Blue = Theme.Hex("#2f8fe6"), BlueHover = Theme.Hex("#4aa3f0");
    private readonly Board _board = new(W, H);
    private readonly TextBox _entry;
    private readonly Border _field, _button, _free;
    private readonly CanvasText _buttonText, _freeText, _status;
    private bool _busy;

    /// <summary>Đã vào được (key hợp lệ / bản miễn phí).</summary>
    public bool SignedIn { get; private set; }

    public LoginWindow(string key, Task<(bool Ok, string Message)>? attempt = null, bool attemptFree = false)
    {
        Title = Layout.AppName;
        ResizeMode = ResizeMode.CanMinimize;
        SizeToContent = SizeToContent.WidthAndHeight;
        Topmost = true;
        Background = Theme.Brush(Theme.Bg);
        Icon = Icons.App(64);
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = (SystemParameters.PrimaryScreenWidth - W) / 2;
        Top = (SystemParameters.PrimaryScreenHeight - H) / 3;
        DarkTitle.Apply(this);
        Content = _board.Canvas;
        const double left = Pad, right = W - Pad, mid = W / 2;
        _board.Canvas.Background = Theme.Brush(Theme.Bg);
        _board.Round(14, 14, W - 28, H - 28, 28, new Fill(Card, null, Theme.Line));
        _board.Round(mid - 70, 14, 140, 4, 2, new Fill(Blue));
        _board.Round(mid - 30, 44, 60, 60, 20, new Fill(Blue));
        _board.Text(mid, 74, "DTA", Anchor.Center, Colors.White, 13, true);
        _board.Text(mid, 130, Layout.AppName, Anchor.Center, Theme.Text, 17, true);
        _board.Text(mid, 156, "Nhập key để mở toàn bộ chức năng", Anchor.Center, Theme.Muted, 9);
        _board.Text(left + 4, 186, "KEY KÍCH HOẠT", Anchor.W, Theme.Muted, 8, true);
        _field = _board.Round(left, 198, right - left, 46, 16, new Fill(FieldBack, null, Theme.Line));
        _entry = new TextBox
        {
            Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Theme.Brush(Theme.Text), CaretBrush = Theme.Brush(Theme.Text),
            FontFamily = new FontFamily("Consolas"), FontSize = Theme.Pt(11), TextAlignment = TextAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
            Width = right - left - 36, Height = 28, Text = key,
        };
        _board.Add(_entry, mid - _entry.Width / 2, 221 - 14);
        _entry.GotKeyboardFocus += (_, _) => Board.Paint(_field, new Fill(FieldBack, null, Blue));
        _entry.LostKeyboardFocus += (_, _) => Board.Paint(_field, new Fill(FieldBack, null, Theme.Line));
        _entry.KeyDown += (_, e) => { if (e.Key == Key.Enter) Submit(); };
        (_button, _buttonText) = Button(left, 258, right, 302, "Kích hoạt key", new Fill(Blue), new Fill(BlueHover), Colors.White, Submit);
        _status = _board.Text(mid, 322, "Key đã nhập sẽ được nhớ cho lần mở sau", Anchor.Center, Theme.Muted, 9);
        _board.Line(left, 352, mid - 26, 352, Theme.Line);
        _board.Line(mid + 26, 352, right, 352, Theme.Line);
        _board.Text(mid, 352, "hoặc", Anchor.Center, Theme.Muted, 8);
        (_free, _freeText) = Button(left, 370, right, 410, "Dùng bản miễn phí", new Fill(Card, null, Theme.Line), new Fill(Theme.Hex("#16233a"), null, Theme.Line), Theme.Text, UseFree);
        _board.Text(mid, 426, "Chỉ có câu cá cơ bản  •  chức năng nâng cao cần key", Anchor.Center, Theme.Dim, 8);
        _board.Text(mid, H - 24, $"Mã máy: {DeviceId.Value[..12]}", Anchor.Center, Theme.Dim, 8);
        Loaded += (_, _) =>
        {
            _entry.Focus();
            _entry.CaretIndex = _entry.Text.Length;
            if (attempt != null) Follow(attempt, attemptFree);
        };
    }

    /// <summary>Nút bo góc: rê chuột đổi màu, bấm gọi lệnh (trừ lúc đang chờ máy chủ).</summary>
    private (Border, CanvasText) Button(double x1, double y1, double x2, double y2, string text, Fill fill, Fill hover, Color fg, Action command)
    {
        var shape = _board.Round(x1, y1, x2 - x1, y2 - y1, 16, fill);
        var label = _board.Text((x1 + x2) / 2, (y1 + y2) / 2, text, Anchor.Center, fg, 11, true);
        Interact.Bind(shape, () => !_busy, inside => Board.Paint(shape, inside ? hover : fill), command);
        return (shape, label);
    }

    private void Submit()
    {
        var key = _entry.Text.Trim();
        if (key.Length == 0 || _busy) return;
        LicenseKeeper.Remember(key);
        Follow(LicenseKeeper.SignInAsync(key, false, 0), false);
    }

    private void UseFree()
    {
        if (!_busy) Follow(LicenseKeeper.SignInAsync("", true), true);
    }

    /// <summary>Chờ 1 lượt gửi (không đứng cửa sổ): được thì đóng, hỏng thì báo lý do và cho thử lại.</summary>
    private async void Follow(Task<(bool Ok, string Message)> attempt, bool free)
    {
        SetBusy(true, free ? "Đang mở bản miễn phí..." : "Đang kiểm tra key...", Theme.Muted);
        var (ok, message) = await attempt;
        if (ok)
        {
            SignedIn = true;
            Close();
            return;
        }
        SetBusy(false, message, Theme.Warn);
    }

    private void SetBusy(bool busy, string text, Color color)
    {
        _busy = busy;
        _entry.IsEnabled = !busy;
        _entry.Foreground = Theme.Brush(busy ? Theme.Muted : Theme.Text);
        Board.Paint(_button, new Fill(busy ? Theme.Hex("#1d3a5c") : Blue));
        _buttonText.Color = busy ? Theme.Hex("#c9d6ea") : Colors.White;
        _freeText.Color = busy ? Theme.Muted : Theme.Text;
        _status.Set(text, color);
        if (!busy) _entry.Focus();
    }
}
