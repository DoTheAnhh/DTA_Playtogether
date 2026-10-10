using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DTA.Ui;

/// <summary>Loại nút nguồn: Bật (xanh) / Tắt (đỏ).</summary>
public enum PowerKind { Start, Stop }

/// <summary>
/// Nút nguồn Bật / Tắt của các trang chức năng: biểu tượng trong vòng tròn + tên đậm + dòng phụ, nền chuyển màu (Bật xanh ngọc - lam, Tắt đỏ).
/// Không bấm được thì về nền tối phẳng, chữ mờ - nhìn là biết nút nào đang dùng được.
/// </summary>
public sealed class CPower
{
    private static readonly Fill Off = new(Theme.Hex("#111a2b"), null, Theme.Line);
    private readonly Border _shape, _badge;
    private readonly TextBlock _glyph, _title, _note;
    private readonly Fill _fill, _hover;
    public bool Enabled { get; private set; } = true;

    public CPower(Board board, double x, double y, double w, double h, PowerKind kind, Action command)
    {
        var start = kind == PowerKind.Start;
        (_fill, _hover) = start
            ? (Theme.AccentFill, Theme.AccentHover)
            : (new Fill(Theme.Hex("#ef4444"), Theme.Hex("#b91c1c")), new Fill(Theme.Hex("#f87171"), Theme.Hex("#dc2626")));
        _shape = board.Round(x, y, w, h, 16, _fill);
        _glyph = Text(start ? "▶" : "■", start ? 11 : 10, true);
        _glyph.Margin = start ? new Thickness(2, 0, 0, 0) : new Thickness(0);
        var size = Math.Min(36, h - 16);
        _badge = new Border { Width = size, Height = size, CornerRadius = new CornerRadius(size / 2), Child = _glyph, Margin = new Thickness(0, 0, 12, 0) };
        _title = Text(start ? "Bật" : "Tắt", 13, true);
        _note = Text(start ? "Bắt đầu chạy" : "Dừng ngay", 8, false);
        var words = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { _title } };
        if (h >= 56) words.Children.Add(_note);
        _shape.Child = new StackPanel
        {
            Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            Children = { _badge, words },
        };
        Interact.Bind(_shape, () => Enabled, inside => Board.Paint(_shape, inside ? _hover : _fill), command);
        Paint();
    }

    /// <summary>Bật / tắt khả năng bấm.</summary>
    public void SetEnabled(bool enabled)
    {
        Enabled = enabled;
        Paint();
    }

    /// <summary>Tô theo trạng thái: bấm được = màu đầy, chữ trắng; không = nền tối, chữ mờ.</summary>
    private void Paint()
    {
        Board.Paint(_shape, Enabled ? _fill : Off);
        var text = new SolidColorBrush(Enabled ? Colors.White : Theme.Dim);
        (_glyph.Foreground, _title.Foreground) = (text, text);
        _note.Foreground = new SolidColorBrush(Enabled ? Theme.White(210) : Theme.Dim);
        _badge.Background = new SolidColorBrush(Enabled ? Theme.White(48) : Theme.White(10));
    }

    private static TextBlock Text(string text, double size, bool bold) => new()
    {
        Text = text, FontFamily = new FontFamily(Theme.Font), FontSize = Theme.Pt(size), FontWeight = bold ? FontWeights.Bold : FontWeights.SemiBold,
        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
    };
}
