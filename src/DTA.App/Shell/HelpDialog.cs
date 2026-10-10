using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using DTA.Ui;
using DTA.App.Views;

namespace DTA.App.Shell;

/// <summary>Hướng dẫn sử dụng của 1 menu: hàng tab (nếu có từ 2 tab), khung cuộn các mục; chữ trong [ngoặc vuông] là tên nút, tô màu nổi.</summary>
public sealed partial class HelpDialog : DarkDialog
{
    private readonly PageView _view;
    private readonly StackPanel _body = new();
    private readonly ScrollViewer _scroll;
    private readonly Dictionary<string, Border> _tabs = [];

    private HelpDialog(Window owner, PageView view, string tab) : base(owner, $"Hướng dẫn sử dụng - {view.Name}", 640, 540 + SystemParameters.WindowCaptionHeight)
    {
        _view = view;
        var root = new DockPanel { Margin = new Thickness(20, 16, 20, 14) };
        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        header.Children.Add(Label($"Hướng dẫn sử dụng: {view.Name}", Theme.Accent, 13, true));
        var subtitle = Label(view.Subtitle, Theme.Muted);
        subtitle.Margin = new Thickness(0, 2, 0, 0);
        header.Children.Add(subtitle);
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);
        if (view.Tabs.Count > 1)
        {
            var bar = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
            foreach (var (key, label) in view.Tabs)
            {
                var button = new Border
                {
                    Padding = new Thickness(14, 6, 14, 6), Margin = new Thickness(0, 0, 8, 0), BorderThickness = new Thickness(1), BorderBrush = Theme.Brush(Theme.Line),
                    Child = Label($" {label} ", Theme.Muted, 9, true), Cursor = System.Windows.Input.Cursors.Hand,
                };
                button.MouseLeftButtonUp += (_, _) => Render(key);
                _tabs[key] = button;
                bar.Children.Add(button);
            }
            DockPanel.SetDock(bar, Dock.Top);
            root.Children.Add(bar);
        }
        var footer = FlatButton("Đã hiểu", Theme.Hex("#1f5f8f"), Theme.Hex("#2f7fbf"), Colors.White, Colors.White, Close);
        (footer.HorizontalAlignment, footer.Margin) = (HorizontalAlignment.Right, new Thickness(0, 14, 0, 0));
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);
        _scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _body };
        root.Children.Add(new Border { Background = Theme.Brush(Theme.Field), BorderBrush = Theme.Brush(Theme.Line), BorderThickness = new Thickness(1), Child = _scroll });
        Content = root;
        Render(tab.Length > 0 ? tab : view.Tabs[0].Key);
    }

    public static void Open(Window owner, PageView view, string tab) => new HelpDialog(owner, view, tab).ShowDialog();

    private void Render(string tab)
    {
        foreach (var (key, button) in _tabs)
        {
            button.Background = Theme.Brush(key == tab ? Theme.Hex("#1f5f8f") : Theme.Field);
            ((TextBlock)button.Child).Foreground = Theme.Brush(key == tab ? Colors.White : Theme.Muted);
        }
        var guide = HelpGuides.All.GetValueOrDefault(tab)
                    ?? new Guide($"Hướng dẫn {_view.Name}", "Hướng dẫn", [("Thông tin", $"• {_view.Subtitle}\n• Sử dụng các phím điều khiển và tùy chọn trên giao diện để vận hành.")]);
        _body.Children.Clear();
        var top = new DockPanel { Margin = new Thickness(16, 16, 16, 12) };
        var badge = new Border { Background = Theme.Brush(Theme.Hex("#1a324b")), Padding = new Thickness(6, 2, 6, 2), Child = Label($" {guide.Badge} ", Theme.Accent, 8, true), VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(badge, Dock.Right);
        top.Children.Add(badge);
        top.Children.Add(Label(guide.Title, Theme.Text, 12, true));
        _body.Children.Add(top);
        _body.Children.Add(new Border { Height = 1, Background = Theme.Brush(Theme.Line), Margin = new Thickness(16, 0, 16, 12) });
        foreach (var (title, text) in guide.Sections)
        {
            var head = Label($"▶  {title}", title.Contains("Cách") ? Theme.Ok : title.Contains("Lưu ý") ? Theme.Warn : Theme.Accent, 9, true);
            head.Margin = new Thickness(16, 0, 16, 4);
            _body.Children.Add(head);
            var body = Label("", Theme.Text);
            (body.TextWrapping, body.Margin, body.LineHeight) = (TextWrapping.Wrap, new Thickness(30, 0, 16, 12), Theme.Pt(9) * 1.55);
            var parts = Brackets().Split(text);
            for (var i = 0; i < parts.Length; i++) body.Inlines.Add(i % 2 == 1 ? new Run(parts[i]) { Foreground = Theme.Brush(Theme.Accent), FontWeight = FontWeights.Bold } : new Run(parts[i]));
            _body.Children.Add(body);
        }
        _scroll.ScrollToTop();
    }

    [GeneratedRegex(@"\[([^\]]+)\]")]
    private static partial Regex Brackets();
}
