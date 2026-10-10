using System.IO.Compression;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace DTA.Runtime.Storage;

/// <summary>Đọc / ghi JSON an toàn: ghi file tạm rồi thay thế (không bao giờ để lại file hỏng), mỗi file 1 khoá. Đuôi .gz -> nén.</summary>
public static class JsonStore
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        IncludeFields = true,
    };

    private static readonly Dictionary<string, object> Locks = new(StringComparer.OrdinalIgnoreCase);

    public static T Load<T>(string path, T fallback, JsonSerializerOptions? options = null)
    {
        lock (LockOf(path))
        {
            try
            {
                if (!File.Exists(path)) return fallback;
                using var stream = Open(path);
                return JsonSerializer.Deserialize<T>(stream, options ?? Options) ?? fallback;
            }
            catch
            {
                return fallback;
            }
        }
    }

    public static void Save<T>(string path, T data, JsonSerializerOptions? options = null)
    {
        lock (LockOf(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temp = path + ".tmp";
            using (var file = File.Create(temp))
            using (Stream output = path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) ? new GZipStream(file, CompressionLevel.Fastest) : file)
            {
                JsonSerializer.Serialize(output, data, options ?? Options);
            }
            File.Move(temp, path, overwrite: true);
        }
    }

    private static object LockOf(string path)
    {
        lock (Locks)
        {
            if (!Locks.TryGetValue(path, out var l)) Locks[path] = l = new object();
            return l;
        }
    }

    private static Stream Open(string path)
    {
        Stream file = File.OpenRead(path);
        return path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) ? new GZipStream(file, CompressionMode.Decompress) : file;
    }
}
