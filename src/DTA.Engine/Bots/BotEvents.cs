namespace DTA.Engine.Bots;

/// <summary>Mức độ 1 dòng trạng thái (giao diện tự chọn màu).</summary>
public enum Level { Info, Ok, Warn, Quiet }

/// <summary>
/// Cầu nối bot -> giao diện (gọi từ luồng của bot): diễn biến, độ bền dụng cụ (lượt còn, tối đa), bot đã dừng (lỗi hoặc null).
/// Bot không biết gì về giao diện.
/// </summary>
public sealed class BotEvents
{
    public Action<string, Level> Status { get; init; } = (_, _) => { };
    public Action<(int Remaining, int Limit)?> Tool { get; init; } = _ => { };
    public Action<string?> Stopped { get; init; } = _ => { };
}

/// <summary>Các bot đang chạy (mọi tab) - luồng nền nhường bot cùng tab; dừng tất cả khi hết hạn key / đóng tool.</summary>
public static class BotRegistry
{
    private static readonly HashSet<Bot> Active = [];

    public static void Add(Bot bot)
    {
        lock (Active) Active.Add(bot);
    }

    public static void Remove(Bot bot)
    {
        lock (Active) Active.Remove(bot);
    }

    /// <summary>Có bot đang chạy trên tab <paramref name="serial"/> không.</summary>
    public static bool Running(string serial)
    {
        lock (Active) return Active.Any(b => b.Running && b.Device.Serial == serial);
    }

    /// <summary>Dừng mọi bot (trừ <paramref name="except"/>).</summary>
    public static void StopAll(Bot? except = null)
    {
        List<Bot> bots;
        lock (Active) bots = Active.Where(b => b != except).ToList();
        foreach (var bot in bots) bot.Stop();
    }
}
