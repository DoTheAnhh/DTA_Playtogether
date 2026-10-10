using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace DTA.Ui;

/// <summary>
/// Ô xem log 1 dòng / bản ghi (font Consolas 9, nền tối, màu theo mức): tự vẽ đúng các dòng đang hiện, giới hạn số dòng, tự cuộn xuống cuối
/// (tắt được), cuộn chuột / kéo thanh cuộn.
/// </summary>
public sealed class LogBox : FrameworkElement
{
    private const double LineH = 16, Pad = 6, BarW = 8;
    private const int MaxLines = 3000;
    private static readonly Typeface Face = new(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    private static readonly Brush Back = Theme.Brush(Theme.Hex("#0b111d")), Thumb = Theme.Brush(Theme.Hex("#2a3a58")), Outline = Theme.Brush(Theme.White(24));
    private readonly List<(string Text, Brush Brush)> _lines = [];
    private int _start;
    private bool _dragging;
    private double _dragFrom;
    private int _dragStart;

    public bool AutoScroll { get; set; } = true;
    public int Count => _lines.Count;
    public IEnumerable<string> Texts => _lines.Select(l => l.Text);

    public LogBox(Board board, double x, double y, double width, double height)
    {
        (Width, Height) = (width, height);
        ClipToBounds = true;
        board.Add(this, x, y);
    }

    private int Visible => (int)((Height - 2 * Pad) / LineH);

    public void Clear()
    {
        _lines.Clear();
        _start = 0;
        InvalidateVisual();
    }

    /// <summary>Thêm dòng (cắt bớt dòng cũ quá giới hạn); đang tự cuộn thì nhảy xuống cuối.</summary>
    public void Append(IEnumerable<(string Text, Color Color)> lines)
    {
        foreach (var (text, color) in lines) _lines.Add((text, Theme.Brush(color)));
        if (_lines.Count > MaxLines) _lines.RemoveRange(0, _lines.Count - MaxLines);
        _start = AutoScroll ? Math.Max(0, _lines.Count - Visible) : Math.Min(_start, Math.Max(0, _lines.Count - Visible));
        InvalidateVisual();
    }

    private (double Top, double Size) BarThumb()
    {
        var track = Height - 2 * Pad;
        var size = Math.Max(track * Visible / Math.Max(_lines.Count, Visible), 18);
        return (Pad + (track - size) * _start / Math.Max(1, _lines.Count - Visible), size);
    }

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRoundedRectangle(Back, new Pen(Outline, 1), new Rect(0.5, 0.5, Width - 1, Height - 1), 10, 10);
        dc.PushClip(new RectangleGeometry(new Rect(Pad, Pad, Width - 2 * Pad - BarW, Height - 2 * Pad)));
        var dip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        for (var k = 0; k < Visible && _start + k < _lines.Count; k++)
        {
            var (text, brush) = _lines[_start + k];
            dc.DrawText(new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Face, Theme.Pt(9), brush, dip), new Point(Pad + 4, Pad + k * LineH));
        }
        dc.Pop();
        var (top, size) = BarThumb();
        dc.DrawRoundedRectangle(Thumb, null, new Rect(Width - Pad - BarW + 2, top, BarW - 3, size), 2, 2);
    }

    private void Scroll(int start)
    {
        start = Math.Clamp(start, 0, Math.Max(0, _lines.Count - Visible));
        if (start == _start) return;
        _start = start;
        InvalidateVisual();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        Scroll(_start - Math.Sign(e.Delta) * 3);
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        var p = e.GetPosition(this);
        if (p.X < Width - Pad - BarW) return;
        var (top, size) = BarThumb();
        if (p.Y < top || p.Y > top + size) Scroll(_start + (p.Y < top ? -Visible : Visible));
        (_dragging, _dragFrom, _dragStart) = (true, p.Y, _start);
        CaptureMouse();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!_dragging) return;
        var (_, size) = BarThumb();
        Scroll(_dragStart + (int)Math.Round((e.GetPosition(this).Y - _dragFrom) / Math.Max(1, Height - 2 * Pad - size) * (_lines.Count - Visible)));
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        _dragging = false;
        ReleaseMouseCapture();
    }
}
