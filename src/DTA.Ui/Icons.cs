using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DTA.Ui;

/// <summary>Biểu tượng 1 màu 18x18 của menu trái (y hệt make_nav_icon): phần tô trừ phần khoét, đông cứng + nhớ đệm theo (loại, màu).</summary>
public static class Icons
{
    private static readonly Dictionary<(string, Color), DrawingImage> Cache = [];

    /// <summary>Ảnh biểu tượng: fish, pin, shovel, eye, pickaxe, bug, card, monster, farm, gear, bolt, log.</summary>
    public static DrawingImage Nav(string kind, Color color)
    {
        if (Cache.TryGetValue((kind, color), out var cached)) return cached;
        var (fill, hole) = Shapes(kind);
        Geometry shape = fill.Aggregate((a, b) => new CombinedGeometry(GeometryCombineMode.Union, a, b));
        foreach (var cut in hole) shape = new CombinedGeometry(GeometryCombineMode.Exclude, shape, cut);
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(Brushes.Transparent, null, new RectangleGeometry(new Rect(0, 0, 18, 18))));
        group.Children.Add(new GeometryDrawing(Theme.Brush(color), null, shape));
        var image = new DrawingImage(group);
        image.Freeze();
        return Cache[(kind, color)] = image;
    }

    /// <summary>Biểu tượng tool (make_app_icon): ô vuông bo góc màu nhấn chuyển màu + con cá trắng; dùng cho logo và icon cửa sổ.</summary>
    public static BitmapSource App(int size = 40)
    {
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(new LinearGradientBrush(Theme.AccentFill.Top, Theme.AccentFill.Bottom!.Value, 90), null, Box(0, 0, 40, 40, 12)));
        group.Children.Add(new GeometryDrawing(Brushes.White, null, new CombinedGeometry(GeometryCombineMode.Union, Oval(8, 13, 27, 27), Poly(24, 20, 33, 12, 33, 28))));
        group.Children.Add(new GeometryDrawing(Theme.Brush(Theme.Hex("#2f8fe6")), null, Oval(12, 17, 15, 20)));
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(size / 40.0, size / 40.0));
            dc.DrawDrawing(group);
        }
        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static readonly Dictionary<(int, string, bool), DrawingImage> Badges = [];

    /// <summary>Ô vật phẩm 20x20 (make_item_badge): nền màu theo cấp nền, hình hạt / quả / dụng cụ, góc đỏ nếu đã khoá.</summary>
    public static DrawingImage ItemBadge(int grade, string kind, bool locked)
    {
        if (Badges.TryGetValue((grade, kind, locked), out var cached)) return cached;
        var (a, b) = Theme.Grades.TryGetValue(grade, out var pair) ? pair : (Theme.Hex("#59c6d9"), Theme.Hex("#59c6d9"));
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(a == b ? Theme.Brush(a) : new LinearGradientBrush(a, b, 45), null, Box(0, 0, 20, 20, 5)));
        var ink = Theme.Brush(grade <= 2 ? Color.FromArgb(230, 20, 25, 35) : Color.FromArgb(240, 255, 255, 255));
        Geometry[] shape = kind switch
        {
            "seed" => [Bar(10, 15, 10, 7.5, 1.5), Oval(6, 4.5, 10, 9.5), Oval(10, 3.5, 14, 8.5)],
            "fruit" => [Oval(5, 6.67, 15, 16.67), Bar(10, 6.67, 12, 3.33, 1.5)],
            _ => [Bar(5, 15, 15, 5, 2.5)],
        };
        foreach (var part in shape) group.Children.Add(new GeometryDrawing(ink, null, part));
        if (locked) group.Children.Add(new GeometryDrawing(Theme.Brush(Theme.Danger), null, new RectangleGeometry(new Rect(11, 11, 9, 9))));
        var image = new DrawingImage(group);
        image.Freeze();
        return Badges[(grade, kind, locked)] = image;
    }

    private static Geometry Oval(double x1, double y1, double x2, double y2) => new EllipseGeometry(new Rect(x1, y1, x2 - x1, y2 - y1));

    private static Geometry Poly(params double[] xy)
    {
        var figure = new PathFigure { StartPoint = new Point(xy[0], xy[1]), IsClosed = true, IsFilled = true };
        for (var i = 2; i < xy.Length; i += 2) figure.Segments.Add(new LineSegment(new Point(xy[i], xy[i + 1]), false));
        return new PathGeometry([figure]);
    }

    private static Geometry Bar(double x1, double y1, double x2, double y2, double width) =>
        new LineGeometry(new Point(x1, y1), new Point(x2, y2)).GetWidenedPathGeometry(new Pen(Brushes.Black, width));

    private static Geometry Box(double x1, double y1, double x2, double y2, double r) => new RectangleGeometry(new Rect(x1, y1, x2 - x1, y2 - y1), r, r);

    /// <summary>Phần tô + phần khoét của từng loại (toạ độ như bản PIL).</summary>
    private static (Geometry[] Fill, Geometry[] Hole) Shapes(string kind) => kind switch
    {
        "fish" => ([Oval(1, 5, 13, 13), Poly(11, 9, 17, 4, 17, 14)], [Oval(3.5, 7, 5.5, 9)]),
        "pin" => ([Oval(3, 1, 15, 13), Poly(4.2, 10, 13.8, 10, 9, 17.5)], [Oval(6.5, 4.5, 11.5, 9.5)]),
        "shovel" => ([Bar(14.5, 3.5, 8, 10, 2), Bar(12.5, 1.5, 16.5, 5.5, 2), Poly(4.5, 9, 9, 13.5, 5.5, 16.5, 1.5, 16.5, 1.5, 12.5)], []),
        "eye" => ([Poly(0.5, 9, 5, 4.5, 9, 3.5, 13, 4.5, 17.5, 9, 13, 13.5, 9, 14.5, 5, 13.5)], [new CombinedGeometry(GeometryCombineMode.Exclude, Oval(5.5, 5.5, 12.5, 12.5), Oval(7.5, 7.5, 10.5, 10.5))]),
        "pickaxe" => ([Bar(4, 15.5, 11.5, 6.5, 2), Poly(5, 5, 9, 2.5, 13.5, 3, 16.5, 7, 14, 6, 11.5, 5.2, 8.5, 5.4)], []),
        "bug" => ([
            .. new[] { (9.0, -1.8), (11.5, 0.0), (14.0, 1.8) }.SelectMany(p => new[] { Bar(5.5, p.Item1, 1.8, p.Item1 + p.Item2, 1.3), Bar(12.5, p.Item1, 16.2, p.Item1 + p.Item2, 1.3) }),
            Oval(4.5, 6, 13.5, 16.5), Oval(6.3, 2, 11.7, 7.5)], [Bar(9, 8, 9, 16, 0.9)]),
        "card" => ([Box(3.5, 1.5, 14.5, 16.5, 2)], [Poly(9, 5, 12, 9, 9, 13, 6, 9)]),
        "monster" => ([Poly(4, 5, 2, 0.8, 6.5, 3), Poly(14, 5, 16, 0.8, 11.5, 3), Oval(2.5, 2.5, 15.5, 13.5),
            Poly(2.5, 8, 15.5, 8, 15.5, 16.5, 13.3, 14.3, 11.2, 16.5, 9, 14.3, 6.8, 16.5, 4.7, 14.3, 2.5, 16.5)], [Oval(5.2, 6, 8, 9.2), Oval(10, 6, 12.8, 9.2)]),
        "farm" => ([Bar(9, 16, 9, 8, 1.8), Poly(9, 9.8, 4, 9, 1.8, 4.2, 6.5, 4.6, 9, 8), Poly(9, 8, 11.3, 3.2, 16.2, 2, 15.2, 6.8, 9, 9.6), Bar(3.5, 16.3, 14.5, 16.3, 1.6)], []),
        "gear" => ([Oval(2, 2, 16, 16), .. Enumerable.Range(0, 8).Select(k =>
        {
            var (cx, cy) = (9 + Math.Cos(k * Math.PI / 4) * 7.6, 9 + Math.Sin(k * Math.PI / 4) * 7.6);
            return Oval(cx - 1.7, cy - 1.7, cx + 1.7, cy + 1.7);
        })], [Oval(6, 6, 12, 12)]),
        "bolt" => ([Poly(10.5, 0.5, 3, 10, 8.5, 10, 6.5, 17.5, 15, 7.5, 9.5, 7.5)], []),
        "log" => ([Box(3.5, 1.5, 14.5, 16.5, 2)], [Bar(6, 5.5, 12, 5.5, 1.3), Bar(6, 9, 12, 9, 1.3), Bar(6, 12.5, 10, 12.5, 1.3)]),
        _ => ([Oval(3, 3, 15, 15)], []),
    };
}
