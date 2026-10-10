namespace DTA.Runtime.Storage;

/// <summary>Mọi đường dẫn của tool, tính từ thư mục chứa file chạy (chép cả thư mục đi đâu cũng chạy được).</summary>
public static class Paths
{
    public static string AppDir { get; } = Environment.GetEnvironmentVariable("DTA_APP_DIR") ?? AppContext.BaseDirectory;
    /// <summary>Cài đặt + lịch sử của người dùng (giữ khi cập nhật tool).</summary>
    public static string DataDir { get; } = Ensure(Path.Combine(AppDir, "data"));
    /// <summary>Bộ đệm theo phiên bản game, log, khoá - xoá được bất cứ lúc nào.</summary>
    public static string RuntimeDir { get; } = Ensure(Path.Combine(AppDir, "runtime"));
    public static string CacheDir { get; } = Ensure(Path.Combine(RuntimeDir, "cache"));
    public static string LogDir { get; } = Ensure(Path.Combine(RuntimeDir, "logs"));

    /// <summary>File riêng của 1 tab giả lập: data/(serial).(suffix).</summary>
    public static string DeviceFile(string serial, string suffix) => Path.Combine(DataDir, $"{serial}.{suffix}");

    private static string Ensure(string dir)
    {
        Directory.CreateDirectory(dir);
        return dir;
    }
}
