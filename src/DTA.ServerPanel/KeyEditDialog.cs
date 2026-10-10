using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DTA.Server.Store;
using DTA.Ui;

namespace DTA.ServerPanel;

/// <summary>Sửa 1 key: ghi chú, số máy, thời hạn (giữ nguyên / số ngày tính lại từ lần nhập kế / hết hạn vào lúc / không giới hạn).</summary>
public sealed class KeyEditDialog : DarkDialog
{
    private readonly KeyItem _item;
    private readonly TextBox _note, _devices, _days, _until;
    private readonly RadioButton _keep, _duration, _deadline, _forever;
    private readonly TextBlock _message = Label("", Theme.Warn);
    private KeyChanges? _changes;

    private KeyEditDialog(Window owner, KeyItem item) : base(owner, "Sửa key", 640, 360 + SystemParameters.WindowCaptionHeight)
    {
        _item = item;
        Background = Theme.Brush(PanelWindow.Card);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        _note = Inputs.Box(300, item.Note);
        _devices = Inputs.Box(60, item.MaxDevices.ToString());
        _days = Inputs.Box(70, item.Duration > 0 ? PanelWindow.Days(item.Duration) : "30");
        _until = Inputs.Box(160, DateTimeOffset.FromUnixTimeSeconds(item.Expires > 0 ? item.Expires : now + 30 * 86400).LocalDateTime.ToString("dd/MM/yyyy HH:mm"));
        RadioButton Radio(string text, bool on = false) => new() { Content = text, IsChecked = on, GroupName = "mode", Foreground = Theme.Brush(Theme.Text), FontSize = Theme.Pt(9), Margin = new Thickness(0, 4, 8, 4), VerticalAlignment = VerticalAlignment.Center };
        _keep = Radio($"Giữ nguyên ({PanelWindow.ExpiryText(item, now)})", true);
        _duration = Radio("Số ngày, tính lại từ lần nhập key kế tiếp:");
        _deadline = Radio("Hết hạn vào lúc (ngày/tháng/năm giờ:phút):");
        _forever = Radio("Không giới hạn");
        _days.GotKeyboardFocus += (_, _) => _duration.IsChecked = true;
        _until.GotKeyboardFocus += (_, _) => _deadline.IsChecked = true;
        var modes = new Grid();
        modes.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        modes.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var rows = new (UIElement Radio, UIElement? Box)[] { (_keep, null), (_duration, _days), (_deadline, _until), (_forever, null) };
        for (var i = 0; i < rows.Length; i++)
        {
            modes.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(rows[i].Radio, i);
            modes.Children.Add(rows[i].Radio);
            if (rows[i].Box is not { } box) continue;
            Grid.SetRow(box, i);
            Grid.SetColumn(box, 1);
            modes.Children.Add(box);
        }
        var form = new StackPanel { Margin = new Thickness(16, 14, 16, 14) };
        form.Children.Add(Label("SỬA THÔNG TIN KEY", Theme.Text, 11, true));
        form.Children.Add(Row("Ghi chú", _note));
        form.Children.Add(Row("Số máy mở cùng lúc", _devices));
        form.Children.Add(Row("Thời hạn", modes));
        form.Children.Add(_message);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var cancel = FlatButton("Huỷ", PanelWindow.Button, PanelWindow.ButtonHover, Theme.Muted, Theme.Text, Close);
        cancel.Margin = new Thickness(0, 0, 8, 0);
        buttons.Children.Add(cancel);
        buttons.Children.Add(FlatButton("Lưu", PanelWindow.Button, PanelWindow.ButtonHover, PanelWindow.Sky, PanelWindow.Sky, Save));
        form.Children.Add(buttons);
        Content = form;
        KeyDown += (_, e) => { if (e.Key == Key.Enter) Save(); };
        Loaded += (_, _) => _note.Focus();
    }

    /// <summary>Dòng "nhãn | ô" của biểu mẫu.</summary>
    public static Grid Row(string label, UIElement field)
    {
        var grid = new Grid { Margin = new Thickness(0, 6, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var text = Label(label, Theme.Muted);
        text.VerticalAlignment = VerticalAlignment.Top;
        text.Margin = new Thickness(0, 5, 0, 0);
        grid.Children.Add(text);
        Grid.SetColumn(field, 1);
        grid.Children.Add(field);
        return grid;
    }

    private void Save()
    {
        var note = _note.Text.Trim();
        var devices = int.TryParse(_devices.Text.Trim(), out var n) ? Math.Clamp(n, 1, 100) : _item.MaxDevices;
        var changes = new KeyChanges(Note: note.Length > 200 ? note[..200] : note, MaxDevices: devices);
        if (_duration.IsChecked == true)
        {
            var seconds = double.TryParse(_days.Text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? (long)(d * 86400) : 0;
            if (seconds <= 0)
            {
                _message.Text = "Số ngày phải lớn hơn 0";
                return;
            }
            changes = changes with { Duration = seconds, Expires = 0, Activated = 0 };
        }
        else if (_deadline.IsChecked == true)
        {
            if (!DateTime.TryParseExact(_until.Text.Trim(), "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var until))
            {
                _message.Text = "Ngày giờ phải viết dạng 31/12/2026 23:59";
                return;
            }
            changes = changes with { Duration = 0, Expires = new DateTimeOffset(until).ToUnixTimeSeconds() };
        }
        else if (_forever.IsChecked == true) changes = changes with { Duration = 0, Expires = 0 };
        _changes = changes;
        Close();
    }

    /// <summary>Mở hộp; trả thay đổi (null = huỷ).</summary>
    public static KeyChanges? Ask(Window owner, KeyItem item)
    {
        var dialog = new KeyEditDialog(owner, item);
        dialog.ShowDialog();
        return dialog._changes;
    }
}
