using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DTA.Server.Store;
using DTA.Ui;

namespace DTA.ServerPanel;

/// <summary>Thêm / sửa 1 vị trí TELE: tên, bản đồ, X Y Z, góc nhìn (độ, ví dụ 91, hoặc quaternion [x, y, z, w]), mô tả, thứ tự.</summary>
public sealed class TeleEditDialog : DarkDialog
{
    private static readonly (int Id, string Name)[] Maps = [(1001, "Plaza"), (1301, "Khu nghỉ dưỡng"), (1201, "Khu cắm trại"), (1101, "Khu trung tâm")];
    private readonly TelePosition? _item;
    private readonly TeleStore _store;
    private readonly TextBox _name, _x, _y, _z, _rotation, _description, _order;
    private readonly ComboBox _map = new() { Width = 220 };
    private readonly TextBlock _message = Label("", Theme.Warn);
    private bool _saved;

    private TeleEditDialog(Window owner, TelePosition? item, TeleStore store, string title) : base(owner, title, 560, 440 + SystemParameters.WindowCaptionHeight)
    {
        (_item, _store) = (item, store);
        Background = Theme.Brush(PanelWindow.Card);
        var f = CultureInfo.InvariantCulture;
        _name = Inputs.Box(300, item?.Name ?? "");
        foreach (var (id, name) in Maps) _map.Items.Add($"{name} ({id})");
        _map.SelectedIndex = item != null && Array.FindIndex(Maps, m => m.Id == item.MapId && m.Name == item.MapName) is var at and >= 0 ? at : 0;
        _x = Inputs.Box(80, item?.X.ToString("F2", f) ?? "0.0");
        _y = Inputs.Box(80, item?.Y.ToString("F2", f) ?? "1.0");
        _z = Inputs.Box(80, item?.Z.ToString("F2", f) ?? "0.0");
        _rotation = Inputs.Box(300, item?.Rotation is { Length: > 0 } r ? JsonSerializer.Serialize(r) : "");
        _description = Inputs.Box(300, item?.Description ?? "");
        _order = Inputs.Box(80, (item?.OrderIndex ?? 0).ToString());
        var xyz = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var box in new[] { _x, _y, _z })
        {
            box.Margin = new Thickness(0, 0, 6, 0);
            xyz.Children.Add(box);
        }
        var rotation = new StackPanel();
        rotation.Children.Add(_rotation);
        var hint = Label("Ví dụ: 91° hoặc [0.0, 0.70711, 0.0, 0.70711]", Theme.Muted, 8);
        hint.Margin = new Thickness(0, 2, 0, 0);
        rotation.Children.Add(hint);
        var form = new StackPanel { Margin = new Thickness(16, 14, 16, 14) };
        form.Children.Add(Label(title, Theme.Text, 12, true));
        form.Children.Add(KeyEditDialog.Row("Tên vị trí", _name));
        form.Children.Add(KeyEditDialog.Row("Bản đồ", _map));
        form.Children.Add(KeyEditDialog.Row("Tọa độ X, Y, Z", xyz));
        form.Children.Add(KeyEditDialog.Row("Góc nhìn nhân vật", rotation));
        form.Children.Add(KeyEditDialog.Row("Mô tả", _description));
        form.Children.Add(KeyEditDialog.Row("Thứ tự sắp xếp", _order));
        _message.Margin = new Thickness(0, 6, 0, 0);
        form.Children.Add(_message);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var cancel = FlatButton("Huỷ", PanelWindow.Button, PanelWindow.ButtonHover, Theme.Muted, Theme.Text, Close);
        cancel.Margin = new Thickness(0, 0, 8, 0);
        buttons.Children.Add(cancel);
        buttons.Children.Add(FlatButton("Lưu vị trí", PanelWindow.Button, PanelWindow.ButtonHover, PanelWindow.Good, PanelWindow.Good, Save));
        form.Children.Add(buttons);
        Content = form;
        KeyDown += (_, e) => { if (e.Key == Key.Enter) Save(); };
        Loaded += (_, _) => _name.Focus();
    }

    /// <summary>"91" / "91°" = xoay quanh trục dọc (quaternion [0, sin, 0, cos] của nửa góc); 4 số (có / không ngoặc) = quaternion. Rỗng = không có.</summary>
    private static double[]? ParseRotation(string raw)
    {
        raw = raw.Replace("°", "").Replace("deg", "").Trim();
        if (raw.Length == 0) return [];
        var cut = raw.IndexOf('[');
        if (cut >= 0 && raw.LastIndexOf(']') > cut) raw = raw[(cut + 1)..raw.LastIndexOf(']')];
        var parts = raw.Replace("(", "").Replace(")", "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var values = parts.Select(p => double.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : double.NaN).ToArray();
        if (values.Any(double.IsNaN)) return null;
        if (values.Length == 1)
        {
            var half = values[0] * Math.PI / 360;
            return [0, Math.Round(Math.Sin(half), 5), 0, Math.Round(Math.Cos(half), 5)];
        }
        return values.Length == 4 ? values : null;
    }

    private void Save()
    {
        var name = _name.Text.Trim();
        if (name.Length == 0)
        {
            _message.Text = "Chưa nhập tên vị trí!";
            return;
        }
        double Number(TextBox box) => double.TryParse(box.Text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : double.NaN;
        var (x, y, z) = (Number(_x), Number(_y), Number(_z));
        if (double.IsNaN(x) || double.IsNaN(y) || double.IsNaN(z))
        {
            _message.Text = "Tọa độ X, Y, Z phải là số hợp lệ!";
            return;
        }
        if (ParseRotation(_rotation.Text) is not { } rotation)
        {
            _message.Text = "Góc nhìn không hợp lệ! Ví dụ: 91 hoặc [0.0, 0.707, 0.0, 0.707]";
            return;
        }
        var map = Maps[Math.Max(0, _map.SelectedIndex)];
        var error = _store.Save(_item?.Id, name, map.Id, map.Name, x, y, z, rotation, _description.Text.Trim(), int.TryParse(_order.Text.Trim(), out var o) ? o : 0);
        if (error.Length > 0)
        {
            _message.Text = $"Lỗi: {error}";
            return;
        }
        _saved = true;
        Close();
    }

    /// <summary>Mở hộp; true nếu đã lưu.</summary>
    public static bool Ask(Window owner, TelePosition? item, TeleStore store)
    {
        var dialog = new TeleEditDialog(owner, item, store, item == null ? "Thêm Vị trí Teleport Server Mới" : "Sửa Vị trí Teleport Server");
        dialog.ShowDialog();
        return dialog._saved;
    }
}
