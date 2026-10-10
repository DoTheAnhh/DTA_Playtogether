using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DTA.Ui;

/// <summary>Menu dọc: mỗi mục = biểu tượng + tên, mục đang chọn tô sáng, có huy hiệu đếm (CanvasNav).</summary>
public sealed class CNav
{
    public const double ItemH = 38, Gap = 5;
    private readonly Board _board;
    private readonly double _x, _width;
    private readonly Action<string> _command;
    private readonly Dictionary<string, (Border Back, Image Icon, CanvasText Label, string Kind, double Top)> _rows = [];
    private readonly Dictionary<string, Border> _badges = [];
    private static readonly Fill Hover = new(Theme.White(14)), Selected = new(Theme.Accent with { A = 34 }, null, Theme.Accent with { A = 84 }), Normal = new(Colors.Transparent);
    public string Value { get; private set; }
    public bool Enabled { get; private set; } = true;

    /// <summary>items: (giá trị, tên, loại biểu tượng - xem <see cref="Icons.Nav"/>).</summary>
    public CNav(Board board, double x, double y, double width, IReadOnlyList<(string Value, string Label, string Icon)> items, Action<string> command)
    {
        (_board, _x, _width, _command, Value) = (board, x, width, command, items[0].Value);
        for (var i = 0; i < items.Count; i++)
        {
            var (value, label, icon) = items[i];
            var top = y + i * (ItemH + Gap);
            var back = board.Round(x, top, width, ItemH, 12, Normal);
            var image = board.Add(new Image { Width = 18, Height = 18, IsHitTestVisible = false }, x + 14, top + ItemH / 2 - 9);
            var text = board.Text(x + 44, top + ItemH / 2, label, Anchor.W, Theme.Muted, 10, true);
            _rows[value] = (back, image, text, icon, top);
            Interact.Bind(back, () => Enabled && value != Value, inside => { if (value != Value) Board.Paint(back, inside ? Hover : Normal); }, () => Select(value));
        }
        Redraw();
    }

    private void Redraw()
    {
        foreach (var (value, (back, icon, label, kind, _)) in _rows)
        {
            var selected = value == Value;
            Board.Paint(back, selected && Enabled ? Selected : Normal);
            icon.Source = Icons.Nav(kind, !Enabled ? Theme.Dim : selected ? Theme.Accent : Theme.Muted);
            label.Color = !Enabled ? Theme.Dim : selected ? Colors.White : Theme.Muted;
        }
    }

    public void SetEnabled(bool enabled)
    {
        Enabled = enabled;
        Redraw();
    }

    /// <summary>Đổi mục đang chọn (không gọi lệnh).</summary>
    public void Set(string value)
    {
        Value = value;
        Redraw();
    }

    public void Select(string value)
    {
        if (!Enabled || value == Value) return;
        Set(value);
        _command(value);
    }

    /// <summary>Đặt / xoá huy hiệu đếm ở mép phải 1 mục ("" hoặc "0" = xoá).</summary>
    public void SetBadge(string value, string count = "", Color? background = null, Color? foreground = null)
    {
        if (!_rows.TryGetValue(value, out var row)) return;
        if (_badges.Remove(value, out var old)) _board.Canvas.Children.Remove(old);
        if (count is "" or "0") return;
        var w = Math.Max(20, count.Length * 8 + 10);
        var badge = _board.Round(_x + _width - w - 8, row.Top + (ItemH - 18) / 2, w, 18, 9, new Fill(background ?? Theme.Hex("#ef4444")));
        badge.IsHitTestVisible = false;
        badge.Child = new TextBlock
        {
            Text = count, Foreground = new SolidColorBrush(foreground ?? Colors.White), FontFamily = new FontFamily(Theme.Font), FontSize = Theme.Pt(8), FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        };
        _badges[value] = badge;
    }
}
