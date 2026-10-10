using DTA.App.Shell;
using DTA.Ui;

namespace DTA.App.Views.Settings;

/// <summary>Cài đặt chung: ghi nhớ tuỳ chọn, thời gian chờ trước khi tự đổi bản đồ (1 - 30 s).</summary>
public sealed class SettingsView(MainWindow app) : PageView(app)
{
    public override string Name => "Cài đặt";
    public override string Icon => "gear";
    public override string Subtitle => "Cài đặt chung của tool";
    public override IReadOnlyList<(string Key, string Label)> Tabs => [("settings", "Cài đặt")];

    public override void BuildTab(string tab)
    {
        const double left = Layout.Left, right = Layout.Right;
        var top = Top;
        Board.Cards((left, top, right - left, 64), (left, top + 76, right - left, 114));
        new CToggle(Board, left + 20, top + 22, "Ghi nhớ cài đặt", on =>
        {
            App.Settings.RememberOptions = on;
            App.Settings.Save();
        }, App.Settings.RememberOptions);
        new CSlider(Board, left + 24, top + 92, right - left - 48, 1, 30, App.Settings.HopWaitSeconds, "Thời gian chờ đổi map",
            "Khoảng thời gian chờ (1 - 30 giây) trước khi tự động chuyển sang bản đồ kế tiếp khi hết mục tiêu", "giây", value =>
            {
                App.Settings.HopWait = value;
                App.Settings.Save();
            });
    }

    public override void BuildFooter() => Board.Text(Layout.Left, Layout.FootTop + 26, "Cài đặt được áp dụng ngay lập tức cho các chức năng tự động.", Anchor.W, Theme.Muted, 8);
}
