using System.Collections.Concurrent;
using DTA.Runtime.Core;

namespace DTA.App.Views.Logs;

/// <summary>
/// Nhật ký cho giao diện: gom bản ghi mới từ mọi luồng vào hàng chờ (trang Log xả theo lô), đếm lỗi chưa xem theo kênh (huy hiệu menu Log +
/// tiêu đề tab). Kênh của từng chức năng gom theo bảng <see cref="Tabs"/>; kênh còn lại thuộc "Hệ thống".
/// </summary>
public static class LogCenter
{
    public const string All = "all", System = "system";

    /// <summary>Tab của trang Log: (mã, tên); thứ tự như bản cũ.</summary>
    public static readonly (string Key, string Title)[] Tabs =
    [
        (All, "Tất cả"), (System, "Hệ thống"), ("collect", "Thu lượm"), ("esp", "ESP"), ("excavation", "Đào cổ vật"), ("farm", "Làm nông"), ("fishing", "Câu cá"),
        ("insect", "Bắt bọ"), ("mining", "Đập đá"), ("mod", "Mod"), ("teleport", "Dịch chuyển"),
    ];

    private static readonly ConcurrentQueue<LogEntry> Pending = new();
    private static readonly ConcurrentDictionary<string, int> Unread = new();

    private static int _started;

    /// <summary>Bật thu gom (gọi 1 lần lúc mở tool).</summary>
    public static void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) == 1) return;
        DTA.Runtime.Core.Log.Added += entry =>
        {
            Pending.Enqueue(entry);
            if (entry.Level == LogLevel.Error) Unread.AddOrUpdate(TabOf(entry.Channel), 1, (_, n) => n + 1);
        };
    }

    /// <summary>Tab chứa 1 kênh log.</summary>
    public static string TabOf(string channel) => Tabs.Any(t => t.Key == channel && channel is not All and not System) ? channel : System;

    /// <summary>Lấy hết bản ghi mới (tối đa <paramref name="max"/>).</summary>
    public static List<LogEntry> Drain(int max = 500)
    {
        var list = new List<LogEntry>();
        while (list.Count < max && Pending.TryDequeue(out var entry)) list.Add(entry);
        return list;
    }

    public static int UnreadOf(string tab) => tab == All ? UnreadTotal : Unread.GetValueOrDefault(tab);

    public static int UnreadTotal => Unread.Values.Sum();

    /// <summary>Đã xem tab: xoá đếm lỗi của tab (tab "Tất cả" xoá hết).</summary>
    public static void MarkRead(string tab)
    {
        if (tab == All) Unread.Clear();
        else Unread.TryRemove(tab, out _);
    }
}
