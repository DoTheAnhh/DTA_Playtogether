using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace DTA.Ui;

/// <summary>
/// Dòng trạng thái 1 dòng dùng chung (StatusTicker): vừa chỗ thì hiện nguyên; dài hơn <c>limit</c> ký tự thì trượt mượt từng pixel sang trái
/// (45 px/s, đứng 1.2 s ở đầu câu). Chữ được nhân đôi trong cùng ô chữ rồi cắt khung nhìn bằng Clip + dịch bằng RenderTransform, nên vẫn ẩn / hiện
/// theo tab như ô chữ thường. Chỉ chạy khung hình khi đang phải trượt.
/// </summary>
public sealed class StatusTicker
{
    private const double Speed = 45, Pause = 1.2, Gap = 48;
    private readonly IReadOnlyList<CanvasText> _labels;
    private readonly IReadOnlyList<CanvasText> _dots;
    private readonly double _width;
    private (string, Color)? _key;
    private double _loop, _height;
    private readonly Stopwatch _clock = new();
    private double _started;
    private bool _running;

    public StatusTicker(IReadOnlyList<CanvasText> labels, IReadOnlyList<CanvasText> dots, int limit)
    {
        (_labels, _dots) = (labels, dots);
        _width = Measure(new string('n', limit));
        _clock.Start();
    }

    private double Measure(string text)
    {
        var block = _labels[0].Block;
        var formatted = new FormattedText(text, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(block.FontFamily, block.FontStyle, block.FontWeight, block.FontStretch), block.FontSize, Brushes.White, 1.0);
        _height = formatted.Height + 2;
        return formatted.WidthIncludingTrailingWhitespace;
    }

    /// <summary>Đổi câu + màu; cùng câu đang chạy thì để chạy tiếp, không giật về đầu.</summary>
    public void Set(string text, Color color)
    {
        foreach (var dot in _dots) dot.Color = color;
        if (_key == (text, color)) return;
        _key = (text, color);
        Stop();
        if (Measure(text) <= _width)
        {
            foreach (var label in _labels)
            {
                (label.Block.Clip, label.Block.RenderTransform) = (null, null);
                label.Set(text, color);
            }
            return;
        }
        _loop = Measure(text) + Gap;
        foreach (var label in _labels)
        {
            label.Color = color;
            label.Block.Inlines.Clear();
            label.Block.Inlines.Add(new Run(text));
            label.Block.Inlines.Add(new InlineUIContainer(new FrameworkElement { Width = Gap }) { BaselineAlignment = BaselineAlignment.Center });
            label.Block.Inlines.Add(new Run(text));
            label.Place();
            label.Block.RenderTransform = new TranslateTransform();
        }
        _started = _clock.Elapsed.TotalSeconds + Pause;
        Render(0);
        _running = true;
        CompositionTarget.Rendering += Tick;
    }

    private void Stop()
    {
        if (!_running) return;
        CompositionTarget.Rendering -= Tick;
        _running = false;
    }

    private void Render(double offset)
    {
        foreach (var label in _labels)
        {
            ((TranslateTransform)label.Block.RenderTransform).X = -offset;
            label.Block.Clip = new RectangleGeometry(new Rect(offset, -2, _width, _height + 4));
        }
    }

    private double _offset = -1;

    private void Tick(object? sender, EventArgs e)
    {
        var elapsed = _clock.Elapsed.TotalSeconds - _started;
        var offset = elapsed > 0 ? Math.Floor(elapsed * Speed) : 0;
        if (offset >= _loop)
        {
            _started = _clock.Elapsed.TotalSeconds + Pause;
            offset = 0;
        }
        if (offset == _offset) return;
        _offset = offset;
        Render(offset);
    }
}
