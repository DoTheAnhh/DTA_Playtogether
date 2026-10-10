using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace DTA.Ui;

/// <summary>Cột bảng: mã, tiêu đề, bề rộng, căn lề (W / Center / E).</summary>
public sealed record Column(string Key, string Title, double Width, Anchor Align = Anchor.W);

/// <summary>1 dòng bảng: mã dòng, chữ từng cột, màu chữ / nền riêng (null = mặc định), ảnh nhỏ đầu cột 1.</summary>
public sealed record TableRow(string Key, string[] Cells, Color? Fg = null, Color? Bg = null, ImageSource? Icon = null);

public enum SelectMode { None, Browse, Extended }

/// <summary>
/// Bảng tối (kiểu Dark.Treeview) trong khung bo góc: tiêu đề phẳng chữ mờ, dòng 24 px xen kẽ 2 nền, dòng chọn xanh dịu, thanh cuộn mảnh. Tự vẽ
/// đúng các dòng đang hiện (không tạo control cho từng ô) nên cập nhật hàng trăm dòng vẫn nhẹ.
/// </summary>
public sealed class CTable : FrameworkElement
{
    public const double Pad = 5, RowH = 24, HeadH = 29, BarW = 8;
    private readonly Column[] _columns;
    private readonly double[] _widths;
    private readonly int _visible;
    private readonly SelectMode _mode;
    private readonly Typeface _face = new(new FontFamily(Theme.Font), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    private readonly Typeface _bold = new(new FontFamily(Theme.Font), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
    private static readonly Brush Back = Theme.Brush(Theme.Field), Stripe = Theme.Brush(Theme.Stripe), Chosen = Theme.Brush(Theme.Hex("#18486b")),
        Thumb = Theme.Brush(Theme.Hex("#2a3a58")), ThumbHover = Theme.Brush(Theme.Hex("#3b5179")), Outline = Theme.Brush(Theme.White(24));
    private IReadOnlyList<TableRow> _rows = [];
    private readonly HashSet<string> _selected = [];
    private int _start, _hoverHead = -1, _anchorRow = -1;
    private bool _barHover, _dragging;
    private double _dragFrom;
    private int _dragStart;

    /// <summary>Bấm 1 dòng: (dòng, mã cột).</summary>
    public event Action<TableRow, string>? RowClicked;
    public event Action? SelectionChanged;
    public event Action<string>? HeadingClicked;
    /// <summary>Nhấp đúp 1 dòng.</summary>
    public event Action<TableRow>? RowActivated;

    public CTable(Board board, double x, double y, double width, IReadOnlyList<Column> columns, string stretch, int rows, SelectMode mode = SelectMode.None)
    {
        (_columns, _visible, _mode) = (columns.ToArray(), rows, mode);
        Width = width;
        Height = HeadH + rows * RowH + 2 * Pad;
        var inner = width - 2 * Pad - BarW;
        _widths = _columns.Select(c => c.Width - (c.Key == stretch ? 2 * Pad : 0)).ToArray();
        var index = Array.FindIndex(_columns, c => c.Key == stretch);
        if (index >= 0) _widths[index] = Math.Max(40, inner - _widths.Where((_, i) => i != index).Sum());
        board.Add(this, x, y);
        Cursor = null;
    }

    public IReadOnlyList<TableRow> Rows => _rows;
    public IReadOnlyCollection<string> Selected => _selected;

    /// <summary>Thay toàn bộ dòng; giữ vị trí cuộn và các dòng đang chọn còn tồn tại.</summary>
    public void SetRows(IReadOnlyList<TableRow> rows)
    {
        _rows = rows;
        var keys = rows.Select(r => r.Key).ToHashSet();
        var lost = _selected.RemoveWhere(k => !keys.Contains(k)) > 0;
        _start = Math.Clamp(_start, 0, Math.Max(rows.Count - _visible, 0));
        InvalidateVisual();
        if (lost) SelectionChanged?.Invoke();
    }

    /// <summary>Chọn các dòng theo mã (không báo sự kiện).</summary>
    public void Select(IEnumerable<string> keys)
    {
        _selected.Clear();
        _selected.UnionWith(keys);
        InvalidateVisual();
    }

    private double ColumnLeft(int index) => Pad + _widths.Take(index).Sum();

    private int ColumnAt(double x)
    {
        for (var i = 0; i < _widths.Length; i++) if (x < ColumnLeft(i + 1)) return i;
        return _widths.Length - 1;
    }

    private int RowAt(double y)
    {
        var k = (int)Math.Floor((y - Pad - HeadH) / RowH);
        return y >= Pad + HeadH && k < _visible && _start + k < _rows.Count ? _start + k : -1;
    }

    private bool HasBar => _rows.Count > _visible;

    /// <summary>Rãnh thanh cuộn: cao hết bảng (kể cả dòng tiêu đề) như ttk.</summary>
    private double Track => HeadH + _visible * RowH;

    private (double Top, double Size) BarThumb()
    {
        var size = Math.Max(Track * _visible / Math.Max(_rows.Count, _visible), 18);
        return (Pad + (Track - size) * _start / Math.Max(1, _rows.Count - _visible), size);
    }

    private FormattedText Text(string text, Brush brush, double size, bool bold, double maxWidth) => new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, bold ? _bold : _face,
        Theme.Pt(size), brush, VisualTreeHelper.GetDpi(this).PixelsPerDip) { MaxTextWidth = Math.Max(1, maxWidth), MaxLineCount = 1, Trimming = TextTrimming.CharacterEllipsis };

    private static double AlignX(FormattedText text, double left, double width, Anchor align) => align switch
    {
        Anchor.Center => left + (width - text.Width) / 2,
        Anchor.E => left + width - 8 - text.Width,
        _ => left + 8,
    };

    protected override void OnRender(DrawingContext dc)
    {
        var width = ActualWidth > 0 ? ActualWidth : Width;
        dc.DrawRoundedRectangle(Back, new Pen(Outline, 1), new Rect(0.5, 0.5, width - 1, Height - 1), 12, 12);
        dc.PushClip(new RectangleGeometry(new Rect(Pad, Pad, width - 2 * Pad, Height - 2 * Pad)));
        for (var c = 0; c < _columns.Length; c++)
        {
            var head = Text(_columns[c].Title, Theme.Brush(c == _hoverHead && HeadingClicked != null ? Theme.Muted : Theme.Dim), 8, true, _widths[c] - 12);
            dc.DrawText(head, new Point(AlignX(head, ColumnLeft(c), _widths[c], _columns[c].Align), Pad + (HeadH - head.Height) / 2));
        }
        var rowW = width - 2 * Pad - BarW;
        for (var k = 0; k < _visible && _start + k < _rows.Count; k++)
        {
            var row = _rows[_start + k];
            var top = Pad + HeadH + k * RowH;
            var chosen = _selected.Contains(row.Key);
            var back = chosen ? Chosen : row.Bg is { } bg ? Theme.Brush(bg) : (_start + k) % 2 == 1 ? Stripe : null;
            if (back != null) dc.DrawRectangle(back, null, new Rect(Pad, top, rowW, RowH));
            var fg = Theme.Brush(chosen ? Colors.White : row.Fg ?? Theme.Text);
            for (var c = 0; c < _columns.Length && c < row.Cells.Length; c++)
            {
                var left = ColumnLeft(c);
                var room = _widths[c];
                if (c == 0 && row.Icon != null)
                {
                    dc.DrawImage(row.Icon, new Rect(left + 6, top + (RowH - 18) / 2, 18, 18));
                    (left, room) = (left + 24, room - 24);
                }
                var cell = Text(row.Cells[c], fg, 9, false, room - 12);
                dc.DrawText(cell, new Point(AlignX(cell, left, room, _columns[c].Align), top + (RowH - cell.Height) / 2));
            }
        }
        dc.Pop();
        // Như thanh cuộn ttk: luôn hiện, chưa cuộn được thì con trượt dài hết rãnh
        var (thumbTop, size) = BarThumb();
        dc.DrawRoundedRectangle(_barHover || _dragging ? ThumbHover : Thumb, null, new Rect(width - Pad - BarW + 2, thumbTop, BarW - 3, size), 2, 2);
    }

    private void Scroll(int start)
    {
        start = Math.Clamp(start, 0, Math.Max(_rows.Count - _visible, 0));
        if (start == _start) return;
        _start = start;
        InvalidateVisual();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        Scroll(_start - Math.Sign(e.Delta) * 3);
        e.Handled = true;
    }

    private bool OnBar(Point p) => HasBar && p.X >= ActualWidth - Pad - BarW;

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var p = e.GetPosition(this);
        if (_dragging)
        {
            var (_, size) = BarThumb();
            Scroll(_dragStart + (int)Math.Round((p.Y - _dragFrom) / Math.Max(1, Track - size) * (_rows.Count - _visible)));
            return;
        }
        var head = p.Y >= Pad && p.Y < Pad + HeadH ? ColumnAt(p.X) : -1;
        var bar = OnBar(p);
        Cursor = head >= 0 && HeadingClicked != null || RowAt(p.Y) >= 0 && (_mode != SelectMode.None || RowClicked != null) ? Cursors.Hand : null;
        if (head == _hoverHead && bar == _barHover) return;
        (_hoverHead, _barHover) = (head, bar);
        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        (_hoverHead, _barHover) = (-1, false);
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        var p = e.GetPosition(this);
        if (e.ClickCount == 2 && !OnBar(p) && RowAt(p.Y) is var row and >= 0)
        {
            RowActivated?.Invoke(_rows[row]);
            return;
        }
        if (!OnBar(p)) return;
        var (top, size) = BarThumb();
        if (p.Y < top || p.Y > top + size) Scroll(_start + (p.Y < top ? -_visible : _visible));
        (_dragging, _dragFrom, _dragStart) = (true, p.Y, _start);
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (_dragging)
        {
            _dragging = false;
            ReleaseMouseCapture();
            InvalidateVisual();
            return;
        }
        var p = e.GetPosition(this);
        if (p.Y >= Pad && p.Y < Pad + HeadH)
        {
            HeadingClicked?.Invoke(_columns[ColumnAt(p.X)].Key);
            return;
        }
        var index = RowAt(p.Y);
        if (index < 0) return;
        Choose(index);
        RowClicked?.Invoke(_rows[index], _columns[ColumnAt(p.X)].Key);
    }

    /// <summary>Chọn dòng theo kiểu Treeview: browse = 1 dòng; extended = Ctrl thêm / bớt, Shift chọn dải.</summary>
    private void Choose(int index)
    {
        if (_mode == SelectMode.None) return;
        var key = _rows[index].Key;
        var modifiers = Keyboard.Modifiers;
        if (_mode == SelectMode.Extended && modifiers.HasFlag(ModifierKeys.Control))
        {
            if (!_selected.Remove(key)) _selected.Add(key);
        }
        else if (_mode == SelectMode.Extended && modifiers.HasFlag(ModifierKeys.Shift) && _anchorRow >= 0 && _anchorRow < _rows.Count)
        {
            _selected.Clear();
            for (var i = Math.Min(_anchorRow, index); i <= Math.Max(_anchorRow, index); i++) _selected.Add(_rows[i].Key);
        }
        else
        {
            _selected.Clear();
            _selected.Add(key);
        }
        if (!modifiers.HasFlag(ModifierKeys.Shift)) _anchorRow = index;
        InvalidateVisual();
        SelectionChanged?.Invoke();
    }
}
