namespace DTA.App.Shell;

/// <summary>Khung chung (px, y hệt bản Tkinter): cửa sổ, cột menu, mép trang, hàng tab, vùng nội dung, chân trang.</summary>
public static class Layout
{
    public const string AppName = "DTA Playtogether";
    public const double WinW = 1240, WinH = 700, NavW = 196;
    public const double Left = NavW + 24, Right = WinW - 24;
    public const double BarTop = 76, ContentTop = 124, ContentBottom = 630, FootTop = 646;
    /// <summary>Bề rộng 1 ô tab; chỗ trống cho hàng tab trước ô chọn giả lập.</summary>
    public const double TabW = 100, TabRoom = 440;
}
