using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DTA.Ui;

/// <summary>Thanh kéo: tiêu đề + mô tả, huy hiệu giá trị góc phải, rãnh tối + phần đã kéo màu nhấn, nút tròn, mốc min / max (CanvasSlider).</summary>
public sealed class CSlider
{
    private const double TrackH = 8, Thumb = 20, R = Thumb / 2;
    private readonly double _x, _w, _trackY;
    private readonly int _min, _max;
    private readonly string _unit;
    private readonly Action<int>? _command;
    private readonly CanvasText _badge;
    private readonly Line _fill;
    private readonly Ellipse _thumb;
    public int Value { get; private set; }
    public bool Enabled { get; set; } = true;

    public CSlider(Board board, double x, double y, double width, int min, int max, int initial, string title, string subtitle = "", string unit = "giây", Action<int>? command = null)
    {
        (_x, _w, _min, _max, _unit, _command) = (x, width, min, max, unit, command);
        Value = Math.Clamp(initial, min, max);
        board.Text(x, y + 2, title, Anchor.NW, Colors.White, 9, true);
        if (subtitle.Length > 0) board.Text(x, y + 22, subtitle, Anchor.NW, Theme.Muted, 8);
        board.Round(x + width - 76, y, 76, 26, 8, new Fill(Theme.Hex("#13233a"), Theme.Hex("#0e1a2c"), Theme.Accent with { A = 180 }));
        _badge = board.Text(x + width - 38, y + 13, "", Anchor.Center, Theme.Accent, 9, true);
        _trackY = y + (subtitle.Length > 0 ? 50 : 36);
        var track = board.Round(x, _trackY, width, TrackH, 4, new Fill(Color.FromArgb(120, 0, 0, 0), null, Theme.White(30)));
        _fill = board.Line(x + 4, _trackY + TrackH / 2, x + 4, _trackY + TrackH / 2, Theme.Accent, TrackH - 2);
        _fill.StrokeStartLineCap = _fill.StrokeEndLineCap = PenLineCap.Round;
        _thumb = board.Add(new Ellipse { Width = Thumb, Height = Thumb, Fill = new SolidColorBrush(Theme.Ok), Stroke = new SolidColorBrush(Theme.Hex("#3b82f6")), StrokeThickness = 2 }, x, _trackY + TrackH / 2 - R);
        var core = board.Add(new Ellipse { Width = Thumb - 8, Height = Thumb - 8, Fill = Brushes.White, IsHitTestVisible = false }, x, _trackY + TrackH / 2 - R + 4);
        board.Text(x, _trackY + TrackH + 6, $"{min}s", Anchor.NW, Theme.Dim, 8);
        board.Text(x + width, _trackY + TrackH + 6, $"{max}s", Anchor.NE, Theme.Dim, 8);
        foreach (FrameworkElement element in new FrameworkElement[] { track, _fill, _thumb })
        {
            element.MouseEnter += (_, _) => element.Cursor = Enabled ? Cursors.Hand : null;
            element.MouseLeftButtonDown += (_, e) =>
            {
                if (!Enabled) return;
                element.CaptureMouse();
                Apply(ValueAt(e.GetPosition(board.Canvas).X));
                e.Handled = true;
            };
            element.MouseMove += (_, e) => { if (element.IsMouseCaptured) Apply(ValueAt(e.GetPosition(board.Canvas).X)); };
            element.MouseLeftButtonUp += (_, _) =>
            {
                if (!element.IsMouseCaptured) return;
                element.ReleaseMouseCapture();
                _command?.Invoke(Value);
            };
        }
        _core = core;
        Update();
    }

    private readonly Ellipse _core;

    private double XOf(int value) => Math.Floor(_x + (double)(value - _min) / Math.Max(1, _max - _min) * (_w - 2 * R));

    private int ValueAt(double x) => (int)Math.Round(_min + Math.Clamp((x - _x - R) / Math.Max(1, _w - 2 * R), 0, 1) * (_max - _min));

    private void Apply(int value)
    {
        value = Math.Clamp(value, _min, _max);
        if (value == Value) return;
        Value = value;
        Update();
        _command?.Invoke(Value);
    }

    private void Update()
    {
        _badge.Text = $"{Value} {_unit}";
        var left = XOf(Value);
        Canvas.SetLeft(_thumb, left);
        Canvas.SetLeft(_core, left + 4);
        _fill.X2 = Math.Max(_x + 4, left + R);
    }
}
