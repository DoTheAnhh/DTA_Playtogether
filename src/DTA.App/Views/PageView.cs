using System.Windows.Media;
using DTA.App.Shell;
using DTA.Ui;
using DTA.Runtime.Device;

namespace DTA.App.Views;

/// <summary>
/// 1 trang chức năng vẽ lên khung chung (như các *View của bản Tkinter): tên + biểu tượng menu, dòng mô tả, các tab, dựng từng tab / chân
/// trang, nhận tab giả lập đang chọn, báo 1 dòng, dừng khi chức năng khác bật / phiên key bị dừng, dọn khi đóng.
/// </summary>
public abstract class PageView(MainWindow app)
{
    protected MainWindow App { get; } = app;
    protected Board Board => App.Board;

    public abstract string Name { get; }
    public abstract string Icon { get; }
    public abstract string Subtitle { get; }

    /// <summary>(mã tab, tên tab); 1 tab thì không hiện hàng tab.</summary>
    public virtual IReadOnlyList<(string Key, string Label)> Tabs => [(Name, Name)];

    public abstract void BuildTab(string tab);

    /// <summary>Phần chân trang dùng chung cho mọi tab của trang.</summary>
    public virtual void BuildFooter() { }

    /// <summary>Người dùng vừa chọn tab giả lập khác (null = không có).</summary>
    public virtual void OnDevice(EmulatorDevice? device) { }

    /// <summary>Khung muốn báo 1 dòng (ví dụ không tìm thấy giả lập).</summary>
    public virtual void ShowMessage(string text, Color color) { }

    /// <summary>Chức năng khác sắp bật (điều khiển nhân vật): dừng chức năng của trang này.</summary>
    public virtual void Halt() { }

    /// <summary>Phiên key bị dừng: đưa nút / trạng thái về "đã dừng".</summary>
    public virtual void OnAuthStop(string reason) => Halt();

    public virtual void OnClose() { }

    /// <summary>Mép trên vùng nội dung của trang (thấp hơn khi hàng tab phải nằm riêng 1 dòng).</summary>
    protected double Top => App.ContentTop(this);
}

/// <summary>Trang giữ chỗ: ghi 2 dòng ở giữa thẻ (Mod chưa có mod nào, chức năng chưa phát triển).</summary>
public class NoticeView(MainWindow app, string name, string icon, string subtitle, string title, string line) : PageView(app)
{
    public override string Name => name;
    public override string Icon => icon;
    public override string Subtitle => subtitle;

    public override void BuildTab(string tab)
    {
        var (top, bottom) = (Top, Layout.ContentBottom);
        Board.Cards((Layout.Left, top, Layout.Right - Layout.Left, bottom - top));
        var center = (Layout.Left + Layout.Right) / 2;
        Board.Text(center, (top + bottom) / 2 - 14, title, Anchor.Center, Theme.Text, 15, true);
        Board.Text(center, (top + bottom) / 2 + 20, line, Anchor.Center, Theme.Muted, 10);
    }
}

/// <summary>Menu Mod: chỗ dành cho các công tắc chỉnh game chạy nền (hiện chưa có mod nào).</summary>
public sealed class ModView(MainWindow app) : NoticeView(app, "Mod", "bolt", "Chỉnh game chạy nền, không cần bật chức năng nào khác", "Chưa có mod nào", "Sẽ có trong bản cập nhật sau.");

/// <summary>Chức năng có tên trên menu nhưng chưa làm.</summary>
public sealed class UpcomingView(MainWindow app, string name, string icon)
    : NoticeView(app, name, icon, "Chức năng chưa phát triển", $"{name}: chức năng chưa phát triển", "Sẽ có trong bản cập nhật sau.")
{
    public override IReadOnlyList<(string Key, string Label)> Tabs => [($"upcoming-{Icon}-0", Name)];
}
