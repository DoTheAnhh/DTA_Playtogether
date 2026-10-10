using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using DTA.Ui;
using DTA.Features.Esp;
using DTA.Runtime.Device;

namespace DTA.App.Views.Esp;

/// <summary>
/// Lớp phủ ESP: cửa sổ trong suốt nằm đúng trên khung hình game, chuột / bàn phím xuyên qua, không chụp màn hình - vị trí nhãn tính từ camera
/// bộ quét đọc. Mỗi vật 1 chấm màu + tên (kèm khoảng cách) bên phải; vẽ lại mỗi khung hình theo camera nội suy. Phần khung hình đang bị cửa sổ
/// khác che thì không vẽ nhãn.
/// </summary>
public sealed class EspOverlay : Window
{
    private const int MaxLabels = 60, PlaceEvery = 15;
    private const double Drift = 0.25;
    private readonly Layer _layer = new();
    private EspScanner? _scanner;
    private EmulatorDevice? _device;
    private EspOptions _options = new();
    private Func<string, int, Color> _color = (_, _) => Colors.White;
    private nint _handle;
    private (int X, int Y, int W, int H)? _rect;
    private List<(int L, int T, int R, int B)> _holes = [];
    private int _ticks;
    private double _drawn;
    private readonly Dictionary<string, (float X, float Y, float Z)> _moving = [];

    public EspOverlay()
    {
        (WindowStyle, AllowsTransparency, Background, Topmost, ShowInTaskbar, ShowActivated, ResizeMode) =
            (WindowStyle.None, true, Brushes.Transparent, true, false, false, ResizeMode.NoResize);
        Content = _layer;
        SourceInitialized += (_, _) =>
        {
            _handle = new WindowInteropHelper(this).Handle;
            SetWindowLong(_handle, GwlExStyle, GetWindowLong(_handle, GwlExStyle) | WsExLayered | WsExTransparent | WsExToolWindow | WsExNoActivate);
        };
    }

    /// <summary>Bắt đầu vẽ theo dữ liệu của <paramref name="scanner"/> lên khung hình game của <paramref name="device"/>.</summary>
    public void Begin(EspScanner scanner, EmulatorDevice device, EspOptions options, Func<string, int, Color> color)
    {
        (_scanner, _device, _options, _color) = (scanner, device, options, color);
        CompositionTarget.Rendering -= Tick;
        CompositionTarget.Rendering += Tick;
    }

    public void End()
    {
        _scanner = null;
        CompositionTarget.Rendering -= Tick;
        HideLayer();
    }

    private void HideLayer()
    {
        if (_rect != null)
        {
            _rect = null;
            Hide();
        }
        _moving.Clear();
        _layer.Items = [];
        _layer.InvalidateVisual();
    }

    private void Tick(object? sender, EventArgs e)
    {
        if (_scanner is not { } scanner || _device is not { } device) return;
        var now = EspScanner.Clock;
        var look = _options.Overlay ? scanner.LookAt(now) : null;
        _ticks++;
        if (look is not { } view)
        {
            HideLayer();
            return;
        }
        if (_rect == null || _ticks % PlaceEvery == 0)
        {
            if (Locate(device) is not var (rect, holes))
            {
                HideLayer();
                return;
            }
            _holes = holes;
            if (rect != _rect)
            {
                if (_rect == null) Show();
                _rect = rect;
                SetWindowPos(_handle, -1, rect.X, rect.Y, rect.W, rect.H, 0x10 | 0x40);
            }
        }
        var (_, _, width, height) = _rect!.Value;
        var step = _drawn > 0 ? Math.Min(1, (now - _drawn) / Drift) : 1;
        _drawn = now;
        var seen = new List<(float Distance, Thing Thing, (float U, float V) Point)>();
        foreach (var thing in scanner.Things)
        {
            if (!_options.Shows(thing)) continue;
            var (x, y, z) = (thing.X, thing.Y, thing.Z);
            if (thing.Key[0] == 'i' && _moving.TryGetValue(thing.Key, out var was))
                (x, y, z) = (was.X + (x - was.X) * (float)step, was.Y + (y - was.Y) * (float)step, was.Z + (z - was.Z) * (float)step);
            var distance = MathF.Sqrt((x - view.Me.X) * (x - view.Me.X) + (z - view.Me.Z) * (z - view.Me.Z));
            if (_options.Radius > 0 && distance > _options.Radius) continue;
            if (thing.Key[0] == 'i') _moving[thing.Key] = (x, y, z);
            if (EspMath.Project(view, new(x, y, z), scanner.Aspect) is not { } p || p.X is < 0 or > 1 || p.Y is < 0 or > 1) continue;
            var (px, py) = ((int)(p.X * width), (int)(p.Y * height));
            if (_holes.Any(h => px >= h.L && px < h.R && py >= h.T && py < h.B)) continue;
            seen.Add((distance, thing, p));
        }
        _layer.Items = seen.OrderBy(s => s.Distance).Take(MaxLabels)
            .Select(s => (s.Point.U, s.Point.V, _options.Distance ? $"{s.Thing.Kind}  {s.Distance:0} m" : s.Thing.Kind, _color(s.Thing.Group, s.Thing.Grade))).ToList();
        _layer.FontSize = Theme.Pt(_options.FontSize);
        _layer.InvalidateVisual();
    }

    /// <summary>Khung hình game trên màn hình + các phần bị cửa sổ khác che (toạ độ trong khung); null nếu ẩn / thu nhỏ / bị che kín.</summary>
    private ((int X, int Y, int W, int H) Rect, List<(int L, int T, int R, int B)> Holes)? Locate(EmulatorDevice device)
    {
        if (device.ScreenRect() is not var (x, y, width, height, top)) return null;
        var holes = new List<(int, int, int, int)>();
        for (var window = GetWindow(top, GwHwndPrev); window != 0; window = GetWindow(window, GwHwndPrev))
        {
            if (window == _handle || !IsWindowVisible(window) || IsIconic(window)) continue;
            if ((GetWindowLong(window, GwlExStyle) & (WsExLayered | WsExTransparent)) == (WsExLayered | WsExTransparent)) continue;
            if (DwmGetWindowAttribute(window, 14, out var cloaked, 4) == 0 && cloaked != 0) continue;
            if (!GetWindowRect(window, out var frame)) continue;
            int left = Math.Max(frame.Left, x) - x, upper = Math.Max(frame.Top, y) - y, right = Math.Min(frame.Right, x + width) - x, lower = Math.Min(frame.Bottom, y + height) - y;
            if (right <= left || lower <= upper) continue;
            if (left <= 0 && upper <= 0 && right >= width && lower >= height) return null;
            holes.Add((left, upper, right, lower));
        }
        return ((x, y, width, height), holes);
    }

    /// <summary>Lớp vẽ nhãn: (vị trí 0..1, chữ, màu) - chấm viền đen + chữ đậm có bóng đen.</summary>
    private sealed class Layer : FrameworkElement
    {
        private static readonly Typeface Face = new(new FontFamily(Theme.Font), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        private static readonly Pen DotPen = new(Brushes.Black, 1);
        public List<(float U, float V, string Text, Color Color)> Items { get; set; } = [];
        public double FontSize { get; set; } = Theme.Pt(12);

        protected override void OnRender(DrawingContext dc)
        {
            var dip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            foreach (var (u, v, text, color) in Items)
            {
                var (x, y) = (Math.Round(u * ActualWidth), Math.Round(v * ActualHeight));
                var brush = Theme.Brush(color);
                dc.DrawEllipse(brush, DotPen, new Point(x, y), 4, 4);
                var shadow = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Face, FontSize, Brushes.Black, dip);
                dc.DrawText(shadow, new Point(x + 11, y + 1 - shadow.Height / 2));
                dc.DrawText(new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Face, FontSize, brush, dip), new Point(x + 10, y - shadow.Height / 2));
            }
        }
    }

    private const int GwlExStyle = -20, WsExTransparent = 0x20, WsExToolWindow = 0x80, WsExLayered = 0x80000, WsExNoActivate = 0x08000000;
    private const uint GwHwndPrev = 3;
    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern int GetWindowLong(nint hWnd, int index);
    [DllImport("user32.dll")] private static extern int SetWindowLong(nint hWnd, int index, int value);
    [DllImport("user32.dll")] private static extern nint GetWindow(nint hWnd, uint cmd);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hWnd);
    [DllImport("user32.dll")] private static extern bool IsIconic(nint hWnd);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint hWnd, out RECT rect);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint hWnd, nint after, int x, int y, int cx, int cy, uint flags);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint hWnd, int attribute, out int value, int size);
}
