using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DTA.Ui;

/// <summary>Điểm neo chữ như Tk: n/s/e/w/ne/nw/se/sw/center.</summary>
public enum Anchor { Center, N, S, E, W, NE, NW, SE, SW }

/// <summary>
/// Mặt vẽ toạ độ tuyệt đối (y hệt canvas của bản Tkinter): chữ có điểm neo, hình bo góc chuyển màu + viền, đường kẻ, thẻ kính mờ. Mọi phần tử
/// tạo ra trong <see cref="Collect"/> được gom lại để ẩn / hiện theo tab.
/// </summary>
public sealed class Board
{
    private readonly Stack<List<UIElement>> _groups = new();

    public Canvas Canvas { get; }

    public Board(double width, double height)
    {
        Canvas = new Canvas { Width = width, Height = height, ClipToBounds = true, SnapsToDevicePixels = true };
        TextOptions.SetTextFormattingMode(Canvas, TextFormattingMode.Display);
        RenderOptions.SetClearTypeHint(Canvas, ClearTypeHint.Enabled);
    }

    /// <summary>Chạy <paramref name="build"/> và trả mọi phần tử nó tạo (để ẩn / hiện theo tab).</summary>
    public List<UIElement> Collect(Action build)
    {
        var group = new List<UIElement>();
        _groups.Push(group);
        try
        {
            build();
        }
        finally
        {
            _groups.Pop();
        }
        foreach (var parent in _groups) parent.AddRange(group);
        return group;
    }

    /// <summary>Đặt 1 phần tử vào (x, y), ghi vào nhóm đang gom.</summary>
    public T Add<T>(T element, double x, double y, int z = 0) where T : UIElement
    {
        Canvas.SetLeft(element, x);
        Canvas.SetTop(element, y);
        if (z != 0) Panel.SetZIndex(element, z);
        Canvas.Children.Add(element);
        foreach (var group in _groups) group.Add(element);
        return element;
    }

    /// <summary>Hình chữ nhật bo góc: chuyển màu dọc trên -> dưới, viền 1 px.</summary>
    public Border Round(double x, double y, double w, double h, double radius, Fill fill) => Add(Shape(w, h, radius, fill), x, y);

    /// <summary>Hình bo góc rời (để đổi kiểu tô theo trạng thái).</summary>
    public static Border Shape(double w, double h, double radius, Fill fill)
    {
        var border = new Border { Width = w, Height = h, CornerRadius = new CornerRadius(Math.Min(radius, Math.Min(w, h) / 2)) };
        Paint(border, fill);
        return border;
    }

    /// <summary>Đổi kiểu tô của hình bo góc.</summary>
    public static void Paint(Border border, Fill fill)
    {
        border.Background = fill.Bottom is { } bottom && bottom != fill.Top
            ? new LinearGradientBrush(fill.Top, bottom, 90)
            : new SolidColorBrush(fill.Top);
        border.BorderBrush = fill.Outline is { } outline ? new SolidColorBrush(outline) : null;
        border.BorderThickness = new Thickness(fill.Outline != null ? 1 : 0);
    }

    /// <summary>Thẻ kính mờ (nền trắng 5-3%, viền trắng 9%, bo 16) như add_cards.</summary>
    public void Cards(params (double X, double Y, double W, double H)[] cards)
    {
        foreach (var (x, y, w, h) in cards) Round(x, y, w, h, 16, Theme.Card);
    }

    public Line Line(double x1, double y1, double x2, double y2, Color color, double width = 1)
    {
        var line = new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = new SolidColorBrush(color), StrokeThickness = width, SnapsToDevicePixels = true };
        return Add(line, 0, 0);
    }

    /// <summary>Chữ đặt tại (x, y) theo điểm neo; cỡ chữ tính theo point như Tk.</summary>
    public CanvasText Text(double x, double y, string text, Anchor anchor, Color color, double size = 9, bool bold = false, string font = Theme.Font) =>
        new(this, x, y, text, anchor, color, size, bold, font);
}

/// <summary>Ô chữ trên <see cref="Board"/>: đổi chữ / màu thì tự đặt lại vị trí theo điểm neo.</summary>
public sealed class CanvasText
{
    private readonly double _x, _y;
    private readonly Anchor _anchor;
    public TextBlock Block { get; }

    public CanvasText(Board board, double x, double y, string text, Anchor anchor, Color color, double size, bool bold, string font)
    {
        (_x, _y, _anchor) = (x, y, anchor);
        Block = new TextBlock
        {
            Text = text, Foreground = new SolidColorBrush(color), FontFamily = new FontFamily(font), FontSize = Theme.Pt(size),
            FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, IsHitTestVisible = false,
        };
        board.Add(Block, x, y);
        Place();
    }

    public string Text
    {
        get => Block.Text;
        set
        {
            if (Block.Text == value) return;
            Block.Text = value;
            Place();
        }
    }

    public Color Color
    {
        set => Block.Foreground = new SolidColorBrush(value);
    }

    public void Set(string text, Color color)
    {
        Color = color;
        Text = text;
    }

    /// <summary>Đặt lại toạ độ góc trên trái theo cỡ chữ đo được và điểm neo.</summary>
    public void Place()
    {
        Block.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var (w, h) = (Block.DesiredSize.Width, Block.DesiredSize.Height);
        var left = _anchor switch
        {
            Anchor.W or Anchor.NW or Anchor.SW => _x,
            Anchor.E or Anchor.NE or Anchor.SE => _x - w,
            _ => _x - w / 2,
        };
        var top = _anchor switch
        {
            Anchor.N or Anchor.NW or Anchor.NE => _y,
            Anchor.S or Anchor.SW or Anchor.SE => _y - h,
            _ => _y - h / 2 + 0.5,
        };
        Canvas.SetLeft(Block, Math.Round(left));
        Canvas.SetTop(Block, Math.Round(top));
    }
}
