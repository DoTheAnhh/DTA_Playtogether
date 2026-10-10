using System.Globalization;

namespace DTA.App.Views;

/// <summary>Định dạng chữ dùng chung của các bảng lịch sử.</summary>
public static class Formats
{
    private static readonly CultureInfo Dots = new("vi-VN");

    /// <summary>Giờ ghi "2026-10-02 16:42:25" hiện kiểu "16:42:25 02/10/2026"; chuỗi lạ giữ nguyên.</summary>
    public static string HistoryTime(string stamp)
    {
        var cut = stamp.IndexOf(' ');
        var parts = cut > 0 ? stamp[..cut].Split('-') : [];
        return parts.Length == 3 ? $"{stamp[(cut + 1)..]} {parts[2]}/{parts[1]}/{parts[0]}" : stamp;
    }

    /// <summary>Giá có dấu chấm ngăn nghìn; không có giá thì "---".</summary>
    public static string Price(int? price) => price is > 0 ? price.Value.ToString("#,0", Dots) : "---";
}
