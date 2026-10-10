using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace DTA.Ui;

/// <summary>Thanh tiêu đề Windows màu tối cho hợp nền.</summary>
public static class DarkTitle
{
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);

    public static void Apply(Window window) => window.SourceInitialized += (_, _) =>
    {
        var on = 1;
        DwmSetWindowAttribute(new WindowInteropHelper(window).Handle, 20, ref on, sizeof(int));
    };
}

/// <summary>Khung hộp thoại tối dùng chung: cỡ cố định, giữa cửa sổ cha, thanh tiêu đề tối, Esc để đóng.</summary>
public class DarkDialog : Window
{
    protected DarkDialog(Window owner, string title, double width, double height)
    {
        (Owner, Title, Width, Height) = (owner, title, width, height);
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        Background = Theme.Brush(Theme.Bg);
        UseLayoutRounding = true;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        DarkTitle.Apply(this);
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }

    public static TextBlock Label(string text, Color color, double size = 9, bool bold = false) => new()
    {
        Text = text, Foreground = Theme.Brush(color), FontFamily = new FontFamily(Theme.Font), FontSize = Theme.Pt(size), FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
    };

    /// <summary>Nút phẳng có màu rê chuột.</summary>
    public static Border FlatButton(string text, Color back, Color hover, Color fg, Color hoverFg, Action click, double width = 0)
    {
        var label = Label(text, fg, 9, true);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        var button = new Border { Background = Theme.Brush(back), Padding = new Thickness(24, 7, 24, 7), Child = label, Cursor = Cursors.Hand, CornerRadius = new CornerRadius(4) };
        if (width > 0) button.Width = width;
        button.MouseEnter += (_, _) => (button.Background, label.Foreground) = (Theme.Brush(hover), Theme.Brush(hoverFg));
        button.MouseLeave += (_, _) => (button.Background, label.Foreground) = (Theme.Brush(back), Theme.Brush(fg));
        button.MouseLeftButtonUp += (_, _) => click();
        return button;
    }
}

/// <summary>Ô nhập chữ tối (viền sáng khi đang gõ) cho hộp thoại xếp bằng layout WPF.</summary>
public static class Inputs
{
    public static TextBox Box(double width, string text = "", bool mono = false)
    {
        var box = new TextBox
        {
            Width = width, Text = text, Background = Theme.Brush(Theme.Field), Foreground = Theme.Brush(Theme.Text), CaretBrush = Theme.Brush(Theme.Text),
            BorderBrush = Theme.Brush(Theme.Line), BorderThickness = new Thickness(1), Padding = new Thickness(6, 4, 6, 4),
            FontFamily = new FontFamily(mono ? Theme.Mono : Theme.Font), FontSize = Theme.Pt(10), VerticalContentAlignment = VerticalAlignment.Center,
        };
        box.GotKeyboardFocus += (_, _) => box.BorderBrush = Theme.Brush(Theme.Accent);
        box.LostKeyboardFocus += (_, _) => box.BorderBrush = Theme.Brush(Theme.Line);
        return box;
    }
}
