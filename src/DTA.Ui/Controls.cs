using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DTA.Ui;

/// <summary>Gắn hover / bấm (nhả chuột) cho 1 phần tử; con trỏ bàn tay khi bấm được.</summary>
public static class Interact
{
    /// <summary>Ghi lại mọi phần tử bấm được (chỉ bật khi tự kiểm giao diện).</summary>
    public static bool Record { get; set; }

    /// <summary>Phần tử bấm được đã ghi khi <see cref="Record"/> bật: (phần tử, đang bấm được, hành động bấm).</summary>
    public static List<(FrameworkElement Element, Func<bool> Enabled, Action Click)> Recorded { get; } = [];

    public static void Bind(FrameworkElement element, Func<bool> enabled, Action<bool>? hover, Action click)
    {
        if (Record) Recorded.Add((element, enabled, click));
        element.MouseEnter += (_, _) =>
        {
            element.Cursor = enabled() ? Cursors.Hand : null;
            if (enabled()) hover?.Invoke(true);
        };
        element.MouseLeave += (_, _) => hover?.Invoke(false);
        element.MouseLeftButtonUp += (_, e) =>
        {
            if (!enabled()) return;
            e.Handled = true;
            click();
        };
    }
}

/// <summary>Nút bo góc có hover / vô hiệu (CanvasButton).</summary>
public sealed class CButton
{
    private readonly Border _shape;
    private readonly TextBlock _label;
    private Fill _fill, _hover;
    private Color _fg;
    public bool Enabled { get; private set; } = true;

    public CButton(Board board, double x, double y, double w, double h, string text, Action command, Fill? fill = null, Fill? hover = null,
                   Color? fg = null, double radius = 14, double size = 11, bool bold = true)
    {
        (_fill, _hover, _fg) = (fill ?? Theme.AccentFill, hover ?? Theme.AccentHover, fg ?? Colors.White);
        _shape = board.Round(x, y, w, h, radius, _fill);
        _label = new TextBlock
        {
            Text = text, Foreground = new SolidColorBrush(_fg), FontFamily = new FontFamily(Theme.Font), FontSize = Theme.Pt(size),
            FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        };
        _shape.Child = _label;
        Interact.Bind(_shape, () => Enabled, inside => Board.Paint(_shape, inside ? _hover : _fill), command);
    }

    public void SetEnabled(bool enabled, string? text = null)
    {
        Enabled = enabled;
        Board.Paint(_shape, enabled ? _fill : Theme.Disabled);
        _label.Foreground = new SolidColorBrush(enabled ? _fg : Theme.Dim);
        if (text != null) _label.Text = text;
    }

    public string Text
    {
        set => _label.Text = value;
    }

    /// <summary>Đổi kiểu tô + màu chữ (vd nút tab đang chọn).</summary>
    public void Restyle(Fill fill, Fill hover, Color fg)
    {
        (_fill, _hover, _fg) = (fill, hover, fg);
        Board.Paint(_shape, Enabled ? _fill : Theme.Disabled);
        _label.Foreground = new SolidColorBrush(Enabled ? _fg : Theme.Dim);
    }

    public void MoveTo(double x, double y)
    {
        Canvas.SetLeft(_shape, x);
        Canvas.SetTop(_shape, y);
    }
}

/// <summary>Công tắc gạt + nhãn (CanvasToggle); vô hiệu thì mờ 40%.</summary>
public sealed class CToggle
{
    private readonly Border _track, _knob;
    private readonly CanvasText _label;
    private readonly Color _textColor;
    private readonly Action<bool> _command;
    public bool Value { get; private set; }
    public bool Enabled { get; private set; } = true;

    public CToggle(Board board, double x, double y, string text, Action<bool> command, bool value = false, Color? textColor = null, bool labelLeft = false)
    {
        (_command, Value, _textColor) = (command, value, textColor ?? Theme.Text);
        _track = board.Round(x, y, 34, 18, 9, Theme.AccentFill);
        _knob = Board.Shape(14, 14, 7, new Fill(Colors.White));
        var host = new Canvas { Width = 34, Height = 18, IsHitTestVisible = false };
        host.Children.Add(_knob);
        board.Add(host, x, y);
        _label = labelLeft ? board.Text(x - 8, y + 9, text, Anchor.E, _textColor) : board.Text(x + 44, y + 9, text, Anchor.W, _textColor);
        _label.Block.IsHitTestVisible = true;
        foreach (var element in new FrameworkElement[] { _track, _label.Block }) Interact.Bind(element, () => Enabled, null, Toggle);
        Redraw();
    }

    private void Redraw()
    {
        Board.Paint(_track, Value ? Theme.AccentFill : new Fill(Theme.White(40)));
        Board.Paint(_knob, new Fill(Value ? Colors.White : Color.FromRgb(176, 188, 206)));
        Canvas.SetLeft(_knob, Value ? 18 : 2);
        Canvas.SetTop(_knob, 2);
        var opacity = Enabled ? 1 : 0.4;
        _track.Opacity = _knob.Opacity = opacity;
        _label.Color = Enabled ? _textColor : Theme.Dim;
    }

    /// <summary>Đổi trạng thái hiển thị (không gọi lệnh).</summary>
    public void Set(bool value)
    {
        Value = value;
        Redraw();
    }

    public void SetEnabled(bool enabled)
    {
        Enabled = enabled;
        Redraw();
    }

    public void Toggle()
    {
        Set(!Value);
        _command(Value);
    }
}

/// <summary>Ô chọn bật / tắt nhỏ, có thể kèm chấm màu (CanvasChip).</summary>
public sealed class CChip
{
    private readonly Border _shape;
    private readonly CanvasText _label;
    private readonly UIElement? _dot;
    private readonly Action<bool> _command;
    public bool Value { get; private set; }
    public bool Enabled { get; private set; } = true;

    public CChip(Board board, double x, double y, double w, double h, string text, Action<bool> command, (Color A, Color B)? dot = null)
    {
        _command = command;
        _shape = board.Round(x, y, w, h, 9, Theme.GhostFill);
        if (dot is { } colors)
        {
            _dot = board.Add(Dot(colors, 10), x + 10, y + h / 2 - 5);
            _label = board.Text(x + 26, y + h / 2, text, Anchor.W, Theme.Muted, 8, true);
        }
        else _label = board.Text(x + w / 2, y + h / 2, text, Anchor.Center, Theme.Muted, 9, true);
        Interact.Bind(_shape, () => Enabled, null, Toggle);
        if (_dot is FrameworkElement d) d.IsHitTestVisible = false;
        Redraw();
    }

    /// <summary>Chấm tròn 1 màu / chuyển màu chéo.</summary>
    public static FrameworkElement Dot((Color A, Color B) colors, double size) => new System.Windows.Shapes.Ellipse
    {
        Width = size, Height = size,
        Fill = colors.A == colors.B ? new SolidColorBrush(colors.A) : new LinearGradientBrush(colors.A, colors.B, 45),
    };

    private void Redraw()
    {
        Board.Paint(_shape, (Value, Enabled) switch
        {
            (false, true) => Theme.GhostFill,
            (true, true) => Theme.AccentFill,
            (false, false) => new Fill(Theme.White(8), null, Theme.White(18)),
            _ => new Fill(Theme.Accent with { A = 46 }, null, Theme.Accent with { A = 70 }),
        });
        _label.Color = !Enabled ? Theme.Dim : Value ? Colors.White : Theme.Muted;
        if (_dot != null) _dot.Opacity = Enabled ? 1 : 0.4;
    }

    public void Set(bool value)
    {
        Value = value;
        Redraw();
    }

    public void SetEnabled(bool enabled)
    {
        Enabled = enabled;
        Redraw();
    }

    public void Toggle()
    {
        Set(!Value);
        _command(Value);
    }
}

/// <summary>Dãy ô chọn ngang, ô đang chọn tô sáng; khoá được bớt ô (CanvasSegment).</summary>
public sealed class CSegment<T> where T : notnull
{
    public const double H = 34, Pad = 3;
    private readonly List<T> _values;
    private readonly List<CanvasText> _labels = [];
    private readonly Border _thumb;
    private readonly double _x, _y, _cell;
    private readonly Action<T> _command;
    private readonly HashSet<T> _locked = [];
    public T Value { get; private set; }

    public CSegment(Board board, double x, double y, double width, IReadOnlyList<(T Value, string Label)> options, Action<T> command)
    {
        (_x, _y, _command) = (x, y, command);
        _values = options.Select(o => o.Value).ToList();
        Value = _values[0];
        _cell = Math.Floor((width - 2 * Pad) / options.Count);
        var track = board.Round(x, y, width, H, 11, new Fill(Color.FromArgb(70, 0, 0, 0), null, Theme.White(22)));
        _thumb = board.Round(x + Pad, y + Pad, _cell, H - 2 * Pad, 9, Theme.AccentFill);
        _thumb.IsHitTestVisible = false;
        for (var i = 0; i < options.Count; i++) _labels.Add(board.Text(x + Pad + _cell * i + _cell / 2, y + H / 2, options[i].Label, Anchor.Center, Theme.Muted, 9, true));
        track.MouseMove += (_, e) => track.Cursor = _locked.Contains(ValueAt(e.GetPosition(board.Canvas).X)) ? null : Cursors.Hand;
        track.MouseLeftButtonUp += (_, e) => Select(ValueAt(e.GetPosition(board.Canvas).X));
        Redraw();
    }

    private T ValueAt(double x) => _values[Math.Clamp((int)((x - _x - Pad) / _cell), 0, _values.Count - 1)];

    private void Redraw()
    {
        var index = _values.IndexOf(Value);
        Canvas.SetLeft(_thumb, _x + Pad + _cell * index);
        for (var i = 0; i < _values.Count; i++) _labels[i].Color = i == index ? Colors.White : _locked.Contains(_values[i]) ? Theme.Dim : Theme.Muted;
    }

    public void Set(T value)
    {
        Value = value;
        Redraw();
    }

    public void Select(T value)
    {
        if (_locked.Contains(value) || EqualityComparer<T>.Default.Equals(value, Value)) return;
        Set(value);
        _command(value);
    }

    /// <summary>Đổi chữ 1 ô (vd thêm số lỗi chưa xem).</summary>
    public void SetLabel(T value, string text) => _labels[_values.IndexOf(value)].Text = text;

    public void SetLocked(T value, bool locked)
    {
        if (locked) _locked.Add(value); else _locked.Remove(value);
        if (locked && EqualityComparer<T>.Default.Equals(Value, value))
        {
            Set(_values[0]);
            _command(Value);
        }
        Redraw();
    }
}
