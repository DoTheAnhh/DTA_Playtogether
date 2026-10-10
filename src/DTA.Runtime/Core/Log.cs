using System.Collections.Concurrent;
using DTA.Runtime.Storage;

namespace DTA.Runtime.Core;

public enum LogLevel { Debug, Info, Warn, Error }

/// <summary>Ghi log cho 1 kênh (tên chức năng / tầng). Dùng chung ở mọi lớp thay cho tự viết log.</summary>
public readonly record struct Logger(string Channel)
{
    public void Debug(string message) => Log.Debug(Channel, message);
    public void Info(string message) => Log.Info(Channel, message);
    public void Warn(string message) => Log.Warn(Channel, message);
    public void Error(string message) => Log.Error(Channel, message);
    /// <summary>Ghi lỗi kèm ngoại lệ ở mức debug (lỗi đã xử lý, chỉ để tra).</summary>
    public void Swallowed(string where, Exception e) => Log.Debug(Channel, $"{where}: {e.GetType().Name}: {e.Message}");
}

public sealed record LogEntry(DateTime Time, LogLevel Level, string Channel, string Message)
{
    public override string ToString() => $"{Time:HH:mm:ss.fff} {Level.ToString().ToUpperInvariant(),-5} [{Channel}] {Message}";
}

/// <summary>
/// Nhật ký trung tâm: ghi file runtime/logs/all.log trên luồng nền (không chặn) + giữ <see cref="Keep"/> dòng gần nhất trong RAM
/// cho trang Log. Kênh = tên chức năng (fishing, teleport...).
/// </summary>
public static class Log
{
    /// <summary>Logger theo kênh để từng lớp dùng: <c>private static readonly Logger L = Log.For("memory");</c>.</summary>
    public static Logger For(string channel) => new(channel);

    public const int Keep = 2000;
    private const long MaxFileBytes = 4 << 20;
    private static readonly ConcurrentQueue<LogEntry> Recent = new();
    private static readonly BlockingCollection<LogEntry> Pending = new(new ConcurrentQueue<LogEntry>());

    public static LogLevel MinLevel { get; set; } = LogLevel.Info;
    public static event Action<LogEntry>? Added;

    static Log() => new Thread(WriteLoop) { IsBackground = true, Name = "LogWriter" }.Start();

    public static void Debug(string channel, string message) => Add(LogLevel.Debug, channel, message);
    public static void Info(string channel, string message) => Add(LogLevel.Info, channel, message);
    public static void Warn(string channel, string message) => Add(LogLevel.Warn, channel, message);
    public static void Error(string channel, string message) => Add(LogLevel.Error, channel, message);

    public static IReadOnlyList<LogEntry> Snapshot() => Recent.ToArray();

    private static void Add(LogLevel level, string channel, string message)
    {
        if (level < MinLevel) return;
        var entry = new LogEntry(DateTime.Now, level, channel, message);
        Recent.Enqueue(entry);
        while (Recent.Count > Keep && Recent.TryDequeue(out _)) { }
        Pending.Add(entry);
        Added?.Invoke(entry);
    }

    private static void WriteLoop()
    {
        var path = Path.Combine(Paths.LogDir, "all.log");
        foreach (var entry in Pending.GetConsumingEnumerable())
        {
            try
            {
                if (File.Exists(path) && new FileInfo(path).Length > MaxFileBytes) File.Move(path, path + ".1", overwrite: true);
                File.AppendAllText(path, entry + Environment.NewLine);
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine($"LogWriter: {e.Message}");
            }
        }
    }
}
