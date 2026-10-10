using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DTA.Server;
using DTA.Ui;

namespace DTA.ServerPanel;

/// <summary>Đăng nhập quản trị (tài khoản + mật khẩu); sai thì xoá ô mật khẩu và báo.</summary>
public sealed class SignInWindow : Window
{
    private readonly TextBox _user = Inputs.Box(220);
    private readonly PasswordBox _password = new();
    private readonly TextBlock _message = DarkDialog.Label("", Theme.Warn);

    public bool SignedIn { get; private set; }

    public SignInWindow()
    {
        Title = "DTA Playtogether - Đăng nhập quản lý";
        (ResizeMode, SizeToContent, WindowStartupLocation) = (ResizeMode.NoResize, SizeToContent.WidthAndHeight, WindowStartupLocation.CenterScreen);
        Background = Theme.Brush(Theme.Hex("#111a2c"));
        DarkTitle.Apply(this);
        (_password.Width, _password.Background, _password.Foreground, _password.CaretBrush) = (220, Theme.Brush(Theme.Field), Theme.Brush(Theme.Text), Theme.Brush(Theme.Text));
        (_password.BorderBrush, _password.Padding, _password.FontSize) = (Theme.Brush(Theme.Line), new Thickness(6, 4, 6, 4), Theme.Pt(10));
        var grid = new Grid { Margin = new Thickness(22, 18, 22, 18) };
        foreach (var _ in Enumerable.Range(0, 5)) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        void Put(UIElement element, int row, int column, int span = 1)
        {
            Grid.SetRow(element, row);
            Grid.SetColumn(element, column);
            Grid.SetColumnSpan(element, span);
            grid.Children.Add(element);
        }
        var title = DarkDialog.Label("Quản lý Server DTA", Theme.Text, 14, true);
        title.Margin = new Thickness(0, 0, 0, 8);
        Put(title, 0, 0, 2);
        foreach (var (text, row) in new[] { ("Tài khoản", 1), ("Mật khẩu", 2) })
        {
            var label = DarkDialog.Label(text, Theme.Muted);
            (label.Margin, label.VerticalAlignment) = (new Thickness(0, 6, 10, 6), VerticalAlignment.Center);
            Put(label, row, 0);
        }
        _user.Margin = _password.Margin = new Thickness(0, 6, 0, 6);
        Put(_user, 1, 1);
        Put(_password, 2, 1);
        Put(_message, 3, 0, 2);
        var button = DarkDialog.FlatButton("Đăng nhập", Theme.Hex("#38bdf8"), Theme.Hex("#7dd3fc"), Theme.Hex("#06121f"), Theme.Hex("#06121f"), Submit);
        (button.HorizontalAlignment, button.Margin) = (HorizontalAlignment.Right, new Thickness(0, 8, 0, 0));
        Put(button, 4, 1);
        Content = grid;
        KeyDown += (_, e) => { if (e.Key == Key.Enter) Submit(); };
        Loaded += (_, _) => _user.Focus();
    }

    private void Submit()
    {
        if (Admin.Check(_user.Text, _password.Password))
        {
            SignedIn = true;
            Close();
            return;
        }
        _password.Clear();
        _message.Text = "Sai tài khoản hoặc mật khẩu";
    }
}
