using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DTA.Server.Store;

/// <summary>JSON kiểu snake_case, giữ nguyên chữ có dấu (như json.dumps(ensure_ascii=False) của bản Python) + ghi file nguyên tử.</summary>
public static class Json
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private static readonly JsonSerializerOptions Indented = new(Options) { WriteIndented = true };

    /// <summary>Ghi file tạm rồi thay thế (không bao giờ để lại file hỏng); lỗi ghi thì bỏ qua (file chỉ là bản sao để xem / sửa tay).</summary>
    public static void Save<T>(string path, T value)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(value, Indented));
            File.Move(temp, path, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }
}
