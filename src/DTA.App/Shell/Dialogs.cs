using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using DTA.Ui;

namespace DTA.App.Shell;

/// <summary>Hộp xác nhận chức năng rủi ro cao (dịch chuyển, đóng băng bọ...): "⚠ CẢNH BÁO", [Không] / [Đồng ý].</summary>
public sealed class RiskDialog : DarkDialog
{
    private bool _accepted;

    private RiskDialog(Window owner) : base(owner, "Cảnh báo", 440, 220 + SystemParameters.WindowCaptionHeight)
    {
        var card = new StackPanel();
        var title = Label("⚠  CẢNH BÁO", Theme.Warn, 12, true);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.Margin = new Thickness(0, 16, 0, 8);
        var message = Label("Chức năng có rủi ro cao\nbạn có đồng ý sử dụng không ?", Theme.Text, 10, true);
        (message.HorizontalAlignment, message.TextAlignment, message.Margin) = (HorizontalAlignment.Center, TextAlignment.Center, new Thickness(0, 0, 0, 20));
        var buttons = new Grid { Margin = new Thickness(24, 0, 24, 16) };
        buttons.ColumnDefinitions.Add(new ColumnDefinition());
        buttons.ColumnDefinitions.Add(new ColumnDefinition());
        var no = FlatButton("Không", Theme.Hex("#1e293b"), Theme.Hex("#334155"), Theme.Hex("#94a3b8"), Colors.White, Close, 110);
        var yes = FlatButton("Đồng ý", Theme.Hex("#0284c7"), Theme.Hex("#0ea5e9"), Colors.White, Colors.White, () => { _accepted = true; Close(); }, 110);
        Grid.SetColumn(yes, 1);
        buttons.Children.Add(no);
        buttons.Children.Add(yes);
        card.Children.Add(title);
        card.Children.Add(message);
        card.Children.Add(buttons);
        Content = new Border
        {
            Margin = new Thickness(16), Background = Theme.Brush(Theme.Field), BorderBrush = Theme.Brush(Theme.Line), BorderThickness = new Thickness(1), Child = card,
        };
    }

    /// <summary>Hỏi người dùng; true = đồng ý.</summary>
    public static bool Ask(Window owner)
    {
        var dialog = new RiskDialog(owner);
        dialog.ShowDialog();
        return dialog._accepted;
    }
}

/// <summary>Hộp xem 1 đoạn chữ dài (báo cáo lỗi...): chữ Consolas, cuộn được, chọn / chép được.</summary>
public sealed class ReportDialog : DarkDialog
{
    private ReportDialog(Window owner, string title, string text) : base(owner, title, 600, 400 + SystemParameters.WindowCaptionHeight) =>
        Content = new TextBox
        {
            Text = text, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(8),
            FontFamily = new FontFamily("Consolas"), FontSize = Theme.Pt(9), Background = Theme.Brush(Theme.Field), Foreground = Theme.Brush(Theme.Text), BorderThickness = new Thickness(0),
        };

    public static void Show(Window owner, string title, string text) => new ReportDialog(owner, title, text).ShowDialog();
}

/// <summary>
/// Hộp chọn nhiều mục (biến thể cá...): lưới ô bấm bật / tắt, "Bỏ chọn hết", "Xong". Không chọn mục nào = tất cả (ghi rõ trên hộp).
/// Đóng bằng Esc / nút X = giữ nguyên lựa chọn cũ.
/// </summary>
public sealed class PickDialog : DarkDialog
{
    private const int Columns = 3;
    private readonly HashSet<int> _picked;
    private bool _done;

    private PickDialog(Window owner, string title, IReadOnlyList<(int Value, string Label)> items, IEnumerable<int> picked)
        : base(owner, title, 520, 120 + (items.Count + Columns - 1) / Columns * 40 + SystemParameters.WindowCaptionHeight)
    {
        _picked = picked.ToHashSet();
        var panel = new StackPanel { Margin = new Thickness(16) };
        var hint = Label("(Không chọn mặc định tất cả)", Theme.Dim, 9);
        hint.Margin = new Thickness(0, 0, 0, 10);
        panel.Children.Add(hint);
        var grid = new UniformGrid { Columns = Columns };
        var cells = new List<Action>();
        foreach (var (value, label) in items)
        {
            var text = Label(label, Theme.Text, 9, true);
            text.HorizontalAlignment = HorizontalAlignment.Center;
            var cell = new Border { Margin = new Thickness(4), Padding = new Thickness(6, 7, 6, 7), CornerRadius = new CornerRadius(6), Child = text, Cursor = Cursors.Hand, BorderThickness = new Thickness(1) };
            void Paint()
            {
                var on = _picked.Contains(value);
                (cell.Background, cell.BorderBrush, text.Foreground) = on
                    ? (Theme.Brush(Theme.Accent), Theme.Brush(Theme.Accent), Theme.Brush(Colors.White))
                    : (Theme.Brush(Theme.Field), Theme.Brush(Theme.Line), Theme.Brush(Theme.Text));
            }
            cell.MouseLeftButtonUp += (_, _) =>
            {
                if (!_picked.Remove(value)) _picked.Add(value);
                Paint();
            };
            Paint();
            cells.Add(Paint);
            grid.Children.Add(cell);
        }
        panel.Children.Add(grid);
        var buttons = new Grid { Margin = new Thickness(4, 14, 4, 0) };
        buttons.ColumnDefinitions.Add(new ColumnDefinition());
        buttons.ColumnDefinitions.Add(new ColumnDefinition());
        var clear = FlatButton("Bỏ chọn hết", Theme.Hex("#1e293b"), Theme.Hex("#334155"), Theme.Hex("#94a3b8"), Colors.White, () =>
        {
            _picked.Clear();
            foreach (var paint in cells) paint();
        }, 140);
        clear.HorizontalAlignment = HorizontalAlignment.Left;
        var done = FlatButton("Xong", Theme.Hex("#0284c7"), Theme.Hex("#0ea5e9"), Colors.White, Colors.White, () => { _done = true; Close(); }, 140);
        done.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(done, 1);
        buttons.Children.Add(clear);
        buttons.Children.Add(done);
        panel.Children.Add(buttons);
        Content = panel;
    }

    /// <summary>Hỏi lựa chọn; null nếu người dùng đóng hộp mà không bấm Xong.</summary>
    public static HashSet<int>? Ask(Window owner, string title, IReadOnlyList<(int Value, string Label)> items, IEnumerable<int> picked)
    {
        var dialog = new PickDialog(owner, title, items, picked);
        dialog.ShowDialog();
        return dialog._done ? dialog._picked : null;
    }
}
