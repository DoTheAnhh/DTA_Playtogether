using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace DTA.Ui;

/// <summary>Bỏ dấu tiếng Việt + chữ thường: gõ "ca quan" vẫn ra "Cá quân".</summary>
public static class Fold
{
    public static string Plain(string text)
    {
        var normalized = text.ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        return new string(normalized.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
    }

    /// <summary>Mọi từ của <paramref name="query"/> đều có trong <paramref name="text"/> (không dấu).</summary>
    public static bool Matches(string text, string query) => Plain(query).Split(' ', StringSplitOptions.RemoveEmptyEntries).All(Plain(text).Contains);
}

/// <summary>Khung bo góc cho ô nhập / ô chọn: viền sáng khi đang gõ, mờ khi vô hiệu (CanvasField).</summary>
public abstract class CField
{
    protected readonly Border Frame;
    public bool Enabled { get; private set; } = true;
    protected bool Focused;

    protected CField(Board board, double x, double y, double w, double h)
    {
        Frame = board.Round(x, y, w, h, 10, new Fill(Theme.Field, null, Theme.Line));
        X = x; Y = y; W = w; H = h;
    }

    public double X { get; }
    public double Y { get; }
    public double W { get; }
    public double H { get; }

    protected void Redraw() => Board.Paint(Frame, new Fill(Theme.Field, null, !Enabled ? Theme.Hex("#172135") : Focused ? Theme.Accent : Theme.Line));

    public virtual void SetEnabled(bool enabled)
    {
        Enabled = enabled;
        Redraw();
    }
}

/// <summary>Ô nhập chữ bo góc có dòng gợi ý mờ khi trống (CanvasEntry).</summary>
public sealed class CEntry : CField
{
    private readonly TextBox _box;
    private readonly TextBlock _hint;

    public CEntry(Board board, double x, double y, double w, double h, string placeholder, Action onChange, string font = Theme.Font, double size = 10) : base(board, x, y, w, h)
    {
        var grid = new Grid { Margin = new Thickness(10, 0, 8, 0) };
        _hint = new TextBlock { Text = placeholder, Foreground = new SolidColorBrush(Theme.Dim), FontFamily = new FontFamily(font), FontSize = Theme.Pt(size), VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
        _box = new TextBox
        {
            Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = new SolidColorBrush(Theme.Text), CaretBrush = new SolidColorBrush(Theme.Text),
            FontFamily = new FontFamily(font), FontSize = Theme.Pt(size), VerticalAlignment = VerticalAlignment.Center, Padding = new Thickness(0),
        };
        grid.Children.Add(_hint);
        grid.Children.Add(_box);
        Frame.Child = grid;
        _box.GotKeyboardFocus += (_, _) => { Focused = true; Redraw(); _hint.Visibility = Visibility.Hidden; };
        _box.LostKeyboardFocus += (_, _) => { Focused = false; Redraw(); _hint.Visibility = _box.Text.Length == 0 ? Visibility.Visible : Visibility.Hidden; };
        _box.TextChanged += (_, _) => { if (!_silent) onChange(); };
    }

    private bool _silent;

    public string Text => _box.Text;

    /// <summary>Đổi chữ (không gọi on_change).</summary>
    public void Set(string text)
    {
        _silent = true;
        _box.Text = text;
        _silent = false;
        _hint.Visibility = text.Length == 0 && !Focused ? Visibility.Visible : Visibility.Hidden;
    }

    public override void SetEnabled(bool enabled)
    {
        _box.IsEnabled = enabled;
        _box.Foreground = new SolidColorBrush(enabled ? Theme.Text : Theme.Dim);
        base.SetEnabled(enabled);
    }
}

/// <summary>Ô chọn xổ xuống bo góc; danh sách tự vẽ (bo góc, dòng dưới chuột sáng, dòng đang chọn có ✓, cuộn, có thể gõ để lọc) (CanvasCombo).</summary>
public sealed class CCombo : CField
{
    private readonly TextBlock _value;
    private readonly System.Windows.Shapes.Path _arrow;
    private readonly Action _command;
    private readonly bool _filterable;
    private List<string> _values;

    public int Index { get; private set; } = -1;
    public string Value => Index >= 0 && Index < _values.Count ? _values[Index] : "";

    public CCombo(Board board, double x, double y, double w, double h, IEnumerable<string> values, Action command, bool filterable = false) : base(board, x, y, w, h)
    {
        (_values, _command, _filterable) = (values.ToList(), command, filterable);
        var grid = new Grid { Margin = new Thickness(13, 0, 10, 0) };
        _value = new TextBlock { Foreground = new SolidColorBrush(Theme.Text), FontFamily = new FontFamily(Theme.Font), FontSize = Theme.Pt(9), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 18, 0) };
        _arrow = new System.Windows.Shapes.Path { Data = Geometry.Parse("M0,0 L8,0 L4,5 Z"), Fill = new SolidColorBrush(Theme.Muted), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        grid.Children.Add(_value);
        grid.Children.Add(_arrow);
        Frame.Child = grid;
        Interact.Bind(Frame, () => Enabled && _values.Count > 0, null, Open);
        Board = board;
    }

    private Board Board { get; }

    /// <summary>Đổi danh sách giá trị.</summary>
    public void SetValues(IEnumerable<string> values)
    {
        _values = values.ToList();
        if (Index >= _values.Count) Index = -1;
        _value.Text = Value;
    }

    public IReadOnlyList<string> Values => _values;

    /// <summary>Chọn theo chỉ số (không gọi lệnh); -1 = trống.</summary>
    public void Select(int index)
    {
        Index = index;
        _value.Text = Value;
    }

    /// <summary>Đặt chữ hiện trên ô (giá trị ngoài danh sách - vd "").</summary>
    public void SetText(string text) => (_value.Text, Index) = (text, _values.IndexOf(text));

    public override void SetEnabled(bool enabled)
    {
        _value.Foreground = new SolidColorBrush(enabled ? Theme.Text : Theme.Dim);
        _arrow.Fill = new SolidColorBrush(enabled ? Theme.Muted : Theme.Dim);
        base.SetEnabled(enabled);
    }

    private void Open() => new DropList(this, Board, _values, Index, _filterable, index =>
    {
        if (index == Index) return;
        Select(index);
        _command();
    });
}

/// <summary>Danh sách xổ xuống của <see cref="CCombo"/>: dòng 28 px, tối đa 8 dòng, cuộn chuột, Esc / bấm ngoài để đóng.</summary>
internal sealed class DropList
{
    private const double RowH = 28, Pad = 6;
    private const int MaxRows = 8;
    private readonly Popup _popup;
    private readonly Canvas _canvas;
    private readonly List<string> _all;
    private readonly int _chosen;
    private readonly bool _filterable;
    private readonly Action<int> _pick;
    private readonly double _width, _head;
    private List<int> _shown;
    private string _query = "";
    private int _start, _hover = -1, _rows;

    public DropList(CCombo combo, Board board, List<string> values, int chosen, bool filterable, Action<int> pick)
    {
        (_all, _chosen, _filterable, _pick) = (values, chosen, filterable, pick);
        _shown = Enumerable.Range(0, values.Count).ToList();
        _head = filterable ? RowH : 0;
        _rows = Math.Min(values.Count, MaxRows);
        _start = Math.Clamp(chosen - _rows + 1, 0, Math.Max(values.Count - _rows, 0));
        var measure = new TextBlock { FontFamily = new FontFamily(Theme.Font), FontSize = Theme.Pt(9) };
        _width = Math.Max(combo.W, values.Max(v => { measure.Text = v; measure.Measure(new Size(1e4, 1e4)); return measure.DesiredSize.Width; }) + 48);
        var height = _rows * RowH + 2 * Pad + _head;
        _canvas = new Canvas { Width = _width, Height = height, Focusable = true, Background = Brushes.Transparent };
        var back = new Border
        {
            Width = _width, Height = height, CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1),
            Background = new LinearGradientBrush(Theme.Hex("#111c2f"), Theme.Hex("#0d1626"), 90), BorderBrush = new SolidColorBrush(Theme.Accent with { A = 70 }),
        };
        _popup = new Popup
        {
            Child = new Grid { Children = { back, _canvas } }, AllowsTransparency = true, StaysOpen = false, PlacementTarget = board.Canvas,
            Placement = PlacementMode.Relative, HorizontalOffset = combo.X, VerticalOffset = combo.Y + combo.H + 4,
        };
        _canvas.MouseMove += (_, e) => SetHover(RowAt(e.GetPosition(_canvas).Y));
        _canvas.MouseLeave += (_, _) => SetHover(-1);
        _canvas.MouseLeftButtonUp += (_, e) => { if (RowAt(e.GetPosition(_canvas).Y) is var i and >= 0) Pick(i); };
        _canvas.MouseWheel += (_, e) => Scroll(e.Delta > 0 ? -1 : 1);
        _canvas.KeyDown += OnKey;
        _canvas.TextInput += (_, e) => { if (_filterable && e.Text.Length > 0 && !char.IsControl(e.Text[0])) Filter(_query + e.Text); };
        _popup.IsOpen = true;
        _canvas.Focus();
        Draw();
    }

    private int RowAt(double y)
    {
        var k = (int)Math.Floor((y - Pad - _head) / RowH);
        return y - Pad - _head >= 0 && k < Math.Min(_rows, _shown.Count - _start) ? _start + k : -1;
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                _popup.IsOpen = false;
                break;
            case Key.Back when _filterable && _query.Length > 0:
                Filter(_query[..^1]);
                break;
            case Key.Enter:
                var index = _hover >= 0 ? _hover : _shown.Count > 0 ? _start : -1;
                if (index >= 0) Pick(index);
                break;
            case Key.Down or Key.Up when _shown.Count > 0:
                var step = e.Key == Key.Down ? 1 : -1;
                _hover = Math.Clamp((_hover >= 0 ? _hover : _start - step) + step, 0, _shown.Count - 1);
                if (_hover < _start || _hover >= _start + _rows) _start = Math.Clamp(step > 0 ? _hover - _rows + 1 : _hover, 0, Math.Max(_shown.Count - _rows, 0));
                Draw();
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    private void Filter(string query)
    {
        _query = query;
        _shown = Enumerable.Range(0, _all.Count).Where(i => Fold.Matches(_all[i], query)).ToList();
        (_start, _hover) = (0, _shown.Count > 0 ? 0 : -1);
        Draw();
    }

    private void SetHover(int index)
    {
        if (index == _hover) return;
        _hover = index;
        Draw();
    }

    private void Scroll(int step)
    {
        var start = Math.Clamp(_start + step, 0, Math.Max(_shown.Count - _rows, 0));
        if (start == _start) return;
        (_start, _hover) = (start, -1);
        Draw();
    }

    private void Pick(int index)
    {
        _popup.IsOpen = false;
        _pick(_shown[index]);
    }

    private void Draw()
    {
        _canvas.Children.Clear();
        TextBlock Label(string text, Color color, bool bold = false, double size = 9) => new()
        {
            Text = text, Foreground = new SolidColorBrush(color), FontFamily = new FontFamily(Theme.Font), FontSize = Theme.Pt(size), FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
        };
        void Put(UIElement element, double x, double centerY)
        {
            element.Measure(new Size(1e4, 1e4));
            Canvas.SetLeft(element, x);
            Canvas.SetTop(element, centerY - element.DesiredSize.Height / 2);
            _canvas.Children.Add(element);
        }
        if (_filterable)
        {
            Put(Label(_query.Length > 0 ? $"Lọc: {_query}|" : "Gõ để lọc...", _query.Length > 0 ? Colors.White : Theme.Dim), Pad + 12, Pad + RowH / 2);
            _canvas.Children.Add(new System.Windows.Shapes.Line { X1 = Pad + 6, X2 = _width - Pad - 6, Y1 = Pad + RowH, Y2 = Pad + RowH, Stroke = new SolidColorBrush(Theme.Dim) });
            if (_shown.Count == 0) Put(Label("Không có mục nào khớp", Theme.Dim), Pad + 12, Pad + _head + RowH / 2);
        }
        for (var k = 0; k < Math.Min(_rows, _shown.Count - _start); k++)
        {
            var index = _start + k;
            var y = Pad + _head + k * RowH;
            if (index == _hover)
            {
                var light = Board.Shape(_width - 2 * Pad, RowH - 2, 8, new Fill(Theme.Accent with { A = 40 }, null, Theme.Accent with { A = 90 }));
                Canvas.SetLeft(light, Pad);
                Canvas.SetTop(light, y + 1);
                _canvas.Children.Add(light);
            }
            var chosen = _shown[index] == _chosen;
            Put(Label(_all[_shown[index]], chosen || index == _hover ? Colors.White : Theme.Text, chosen), Pad + 12, y + RowH / 2);
            if (chosen)
            {
                var tick = Label("✓", Theme.Accent, true, 10);
                tick.Measure(new Size(1e4, 1e4));
                Put(tick, _width - Pad - 12 - tick.DesiredSize.Width, y + RowH / 2);
            }
        }
        if (_shown.Count <= _rows) return;
        var track = _rows * RowH;
        var size = Math.Max(track * _rows / _shown.Count, 16);
        var offset = (track - size) * _start / (_shown.Count - _rows);
        _canvas.Children.Add(new System.Windows.Shapes.Line
        {
            X1 = _width - 4, X2 = _width - 4, Y1 = Pad + _head + offset, Y2 = Pad + _head + offset + size, Stroke = new SolidColorBrush(Theme.Dim),
            StrokeThickness = 3, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
        });
    }
}
