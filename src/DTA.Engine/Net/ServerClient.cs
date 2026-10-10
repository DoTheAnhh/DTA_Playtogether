using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Nodes;
using DTA.Runtime.Core;

namespace DTA.Engine.Net;

/// <summary>
/// Gọi máy chủ key qua HTTPS, CHỈ chấp nhận đúng chứng chỉ đã ghim (nhúng trong tool) - không máy chủ giả nào trả lời thay được. Mọi API là
/// POST JSON, trả {ok, message, ...}; không tới được máy chủ thì ok = null.
/// </summary>
public sealed class ServerClient
{
    public const string Activate = "/api/activate", Free = "/api/free", Check = "/api/check", Release = "/api/release", Ui = "/api/ui", TelePositions = "/api/tele_positions";
    public const string Unreachable = "Không kết nối được máy chủ key - kiểm tra mạng rồi thử lại";
    private static readonly (string Host, int Port) DefaultServer = ("127.0.0.1", 28445);
    private static readonly Logger L = Log.For("server");
    private readonly HttpClient _http;

    public static ServerClient Default { get; } = new();

    public string Key { get; set; } = "";
    public string Token { get; set; } = "";
    public string Device { get; } = DeviceId.Value;

    private ServerClient()
    {
        var pinned = LoadCert();
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, cert, _, _) => pinned != null && cert != null && cert.RawData.AsSpan().SequenceEqual(pinned.RawData),
        };
        var (host, port) = Address();
        _http = new HttpClient(handler) { BaseAddress = new Uri($"https://{host}:{port}"), Timeout = TimeSpan.FromSeconds(10) };
    }

    /// <summary>
    /// Địa chỉ máy chủ: ghi lúc build (build.ps1 -Server host:port -> AssemblyMetadata "DtaServer"), không có thì mặc định. Bản Debug cho đổi qua
    /// biến môi trường DTA_SERVER=host:port để chạy thử với máy chủ trên máy.
    /// </summary>
    private static (string, int) Address()
    {
        var built = typeof(ServerClient).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "DtaServer")?.Value ?? "";
#if DEBUG
        built = Environment.GetEnvironmentVariable("DTA_SERVER") is { Length: > 0 } test ? test : built;
#endif
        var cut = built.LastIndexOf(':');
        return cut > 0 && int.TryParse(built[(cut + 1)..], out var port) ? (built[..cut], port) : DefaultServer;
    }

    private static X509Certificate2? LoadCert()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("DTA.Engine.server_cert.pem");
        if (stream == null) return null;
        using var reader = new StreamReader(stream);
        return X509Certificate2.CreateFromPem(reader.ReadToEnd());
    }

    /// <summary>Gửi 1 yêu cầu; lỗi mạng / chứng chỉ sai thì {ok: null, message: không kết nối được}.</summary>
    public async Task<JsonObject> CallAsync(string path, object payload)
    {
        try
        {
            using var response = await _http.PostAsJsonAsync(path, payload);
            return await response.Content.ReadFromJsonAsync<JsonObject>() ?? NoServer();
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            L.Debug($"{path}: {e.Message}");
            return NoServer();
        }
    }

    private static JsonObject NoServer() => new() { ["ok"] = null, ["message"] = Unreachable };

    public Task<JsonObject> ActivateAsync(string key) => CallAsync(Activate, new { key, device = Device, build = "" });
    public Task<JsonObject> FreeAsync() => CallAsync(Free, new { device = Device, build = "" });
    public Task<JsonObject> CheckAsync() => CallAsync(Check, new { key = Key, device = Device, token = Token });
    public Task<JsonObject> ReleaseAsync() => CallAsync(Release, new { key = Key, device = Device, token = Token });
    public Task<JsonObject> FeaturesAsync() => CallAsync(Ui, new { });
    public Task<JsonObject> TelePositionsAsync(int version) => CallAsync(TelePositions, new { version });
}

/// <summary>Mã máy (băm từ MachineGuid của Windows) để máy chủ gắn key với máy đang dùng.</summary>
public static class DeviceId
{
    public static string Value { get; } = Compute();

    private static string Compute()
    {
        var machine = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography")?.GetValue("MachineGuid") as string
                      ?? Environment.MachineName + Environment.UserName;
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("dta:" + machine));
        return Convert.ToHexString(hash).ToLowerInvariant()[..32];
    }
}
