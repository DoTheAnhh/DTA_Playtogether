using System.Windows.Media;

namespace DTA.Ui;

/// <summary>Kiểu tô của 1 hình bo góc: màu trên, màu dưới (null = 1 màu), viền 1 px (null = không viền).</summary>
public sealed record Fill(Color Top, Color? Bottom = null, Color? Outline = null);

/// <summary>
/// Bảng màu + kiểu tô + cỡ chữ dùng chung (y hệt bản Tkinter): nền xanh đen trung tính, đúng 1 màu nhấn (xanh ngọc - lam); đỏ / vàng chỉ để
/// báo dừng, lỗi, cảnh báo.
/// </summary>
public static class Theme
{
    public static readonly Color Bg = Hex("#0a0f1c"), Field = Hex("#0f1828"), Line = Hex("#23314d"), Text = Hex("#e6edf7"), Muted = Hex("#8a97ad"),
        Dim = Hex("#56627a"), Accent = Hex("#5ccfee"), Ok = Hex("#5eead4"), Warn = Hex("#fbbf24"), Danger = Hex("#f87171"),
        Stripe = Hex("#121d30"), Divider = Hex("#182238");

    public const string Font = "Segoe UI, Segoe UI Symbol", Mono = "Cascadia Mono";

    public static readonly Fill AccentFill = new(Hex("#4fd1e8"), Hex("#2f8fe6"));
    public static readonly Fill AccentHover = new(Hex("#7ddff0"), Hex("#4aa3f0"));
    public static readonly Fill SuccessFill = new(Hex("#10b981"), Hex("#059669"));
    public static readonly Fill SuccessHover = new(Hex("#34d399"), Hex("#10b981"));
    public static readonly Fill SecondaryFill = new(Hex("#202b3d"), Hex("#141d2b"), Argb(220, "#4a5d7c"));
    public static readonly Fill SecondaryHover = new(Hex("#2a3950"), Hex("#1c2738"), Hex("#6b82a8"));
    public static readonly Fill GhostFill = new(White(16), null, White(36));
    public static readonly Fill GhostHover = new(White(34), null, White(64));
    public static readonly Fill DangerFill = new(Argb(34, "#f87171"), null, Argb(150, "#f87171"));
    public static readonly Fill DangerHover = new(Argb(70, "#f87171"), null, Argb(210, "#f87171"));
    public static readonly Fill DangerButtonFill = new(Hex("#4c131a"), Hex("#2d0a0f"), Argb(200, "#f87171"));
    public static readonly Fill DangerButtonHover = new(Hex("#6e1b24"), Hex("#3f0e15"), Hex("#f87171"));
    public static readonly Fill Disabled = new(White(10), null, White(20));
    public static readonly Fill Card = new(White(12), White(7), White(24));

    /// <summary>Màu chấm theo nền vật phẩm (VVIP chuyển 2 màu).</summary>
    public static readonly IReadOnlyDictionary<int, (Color A, Color B)> Grades = new Dictionary<int, (Color, Color)>
    {
        [1] = (Hex("#e4e0c5"), Hex("#e4e0c5")), [2] = (Hex("#a3e467"), Hex("#a3e467")), [3] = (Hex("#59c6d9"), Hex("#59c6d9")),
        [4] = (Hex("#e793e8"), Hex("#e793e8")), [5] = (Hex("#9fc7ff"), Hex("#a9aaff")),
    };

    public static Color Hex(string hex) => (Color)ColorConverter.ConvertFromString(hex);
    public static Color Argb(byte alpha, string hex) => Hex(hex) with { A = alpha };
    public static Color White(byte alpha) => Color.FromArgb(alpha, 255, 255, 255);

    /// <summary>Cỡ chữ Tk (point) -> đơn vị WPF (1/96 inch).</summary>
    public static double Pt(double points) => points * 96 / 72;

    /// <summary>Bề rộng chữ (px) theo cỡ point như tkfont.measure.</summary>
    public static double Measure(string text, double size = 9, bool bold = false) => new FormattedText(text, System.Globalization.CultureInfo.CurrentCulture,
        System.Windows.FlowDirection.LeftToRight, new Typeface(new FontFamily(Font), System.Windows.FontStyles.Normal, bold ? System.Windows.FontWeights.Bold : System.Windows.FontWeights.Normal,
            System.Windows.FontStretches.Normal), Pt(size), Brushes.White, null, TextFormattingMode.Display, 1.0).WidthIncludingTrailingWhitespace;

    public static SolidColorBrush Brush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
