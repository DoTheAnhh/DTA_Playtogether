using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DTA.App.Shell;
using DTA.Ui;

namespace DTA.App.Views.Farm;

/// <summary>Hộp chọn các biến thể muốn giữ lại (bỏ tick = không giữ; để trống = giữ tất cả): Chọn hết / Bỏ hết / Lưu &amp; Đóng.</summary>
public sealed class MutationDialog : DarkDialog
{
    /// <summary>Bảng biến thể mặc định khi chưa đọc được từ game.</summary>
    public static readonly IReadOnlyDictionary<int, (string Name, float Multiplier)> Defaults = new Dictionary<int, (string, float)>
    {
        [1] = ("Khổng lồ", 1.5f), [2] = ("Lấp lánh", 2f), [3] = ("Ngọt ngào", 2f), [4] = ("Vàng ròng", 3f), [5] = ("Kim cương", 4f), [6] = ("Cầu vồng", 5f),
        [7] = ("Bốc hỏa", 2.5f), [8] = ("Đóng băng", 2.5f), [9] = ("Ánh sao", 3.5f), [10] = ("Bóng tối", 3.5f), [11] = ("Siêu to", 2f), [12] = ("Huyền thoại", 5f),
    };

    private readonly Dictionary<int, CheckBox> _boxes = [];
    private HashSet<int>? _chosen;

    private MutationDialog(Window owner, string title, IReadOnlyDictionary<int, (string Name, float Multiplier)> table, HashSet<int> selected)
        : base(owner, title, 380, 480 + SystemParameters.WindowCaptionHeight)
    {
        var root = new DockPanel();
        var head = new StackPanel { Margin = new Thickness(16, 14, 16, 8) };
        var caption = Label(title, Theme.Text, 11, true);
        caption.HorizontalAlignment = HorizontalAlignment.Center;
        var note = Label("Bỏ tick = không giữ (bán/hái)  |  Để trống = giữ tất cả", Theme.Muted, 8);
        (note.HorizontalAlignment, note.Margin) = (HorizontalAlignment.Center, new Thickness(0, 2, 0, 0));
        head.Children.Add(caption);
        head.Children.Add(note);
        DockPanel.SetDock(head, Dock.Top);
        root.Children.Add(head);
        var buttons = new DockPanel { Margin = new Thickness(16, 10, 16, 14), LastChildFill = false };
        var all = FlatButton("Chọn hết", Theme.Hex("#1a253a"), Theme.Hex("#24324d"), Theme.Text, Colors.White, () => SetAll(true));
        var none = FlatButton("Bỏ hết", Theme.Hex("#1a253a"), Theme.Hex("#24324d"), Theme.Muted, Colors.White, () => SetAll(false));
        var save = FlatButton("Lưu & Đóng", Theme.Accent, Theme.Hex("#7ddff0"), Colors.Black, Colors.Black, Save);
        none.Margin = new Thickness(8, 0, 0, 0);
        DockPanel.SetDock(save, Dock.Right);
        buttons.Children.Add(all);
        buttons.Children.Add(none);
        buttons.Children.Add(save);
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);
        var list = new StackPanel { Margin = new Thickness(6) };
        foreach (var (id, (name, multiplier)) in table.OrderBy(p => p.Key))
        {
            var box = new CheckBox
            {
                Content = multiplier > 1 ? $"{name}  (hệ số x{multiplier:0.##})" : name, IsChecked = selected.Contains(id), Foreground = Theme.Brush(Theme.Text),
                FontFamily = new FontFamily(Theme.Font), FontSize = Theme.Pt(9), Margin = new Thickness(8, 3, 8, 3),
            };
            _boxes[id] = box;
            list.Children.Add(box);
        }
        root.Children.Add(new Border
        {
            Margin = new Thickness(16, 4, 16, 0), Background = Theme.Brush(Theme.Field), BorderBrush = Theme.Brush(Theme.Line), BorderThickness = new Thickness(1),
            Child = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = list },
        });
        Content = root;
    }

    private void SetAll(bool on)
    {
        foreach (var box in _boxes.Values) box.IsChecked = on;
    }

    private void Save()
    {
        _chosen = _boxes.Where(b => b.Value.IsChecked == true).Select(b => b.Key).ToHashSet();
        Close();
    }

    /// <summary>Mở hộp; trả các biến thể đã chọn (null = đóng không lưu).</summary>
    public static HashSet<int>? Ask(Window owner, string title, IReadOnlyDictionary<int, (string Name, float Multiplier)> table, HashSet<int> selected)
    {
        var dialog = new MutationDialog(owner, title, table, selected);
        dialog.ShowDialog();
        return dialog._chosen;
    }
}
