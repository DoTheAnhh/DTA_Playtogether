using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DTA.Server.Services;
using DTA.Server.Store;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace DTA.Server.Http;

/// <summary>
/// API HTTPS cho tool (Kestrel, chứng chỉ tự ký - tool ghim đúng chứng chỉ): mọi route POST JSON trả {ok, message, ...}. /api/activate, /free,
/// /check, /release, /ui (danh sách chức năng), /config, /tele_positions (cả GET). Thân tối đa 64 KB.
/// </summary>
public static class ApiHost
{
    private const int MaxBody = 64 * 1024;
    private const string TooMany = "Thử quá nhiều lần, chờ 1 phút rồi thử lại";

    /// <summary>Danh sách chức năng gửi tool (Server-Driven UI): mã, tên, bật / tắt.</summary>
    private static readonly object[] Features =
    [
        new { id = "mod", name = "Mod", icon = "mod", enabled = true }, new { id = "esp", name = "ESP", icon = "esp", enabled = true },
        new { id = "fishing", name = "Câu cá", icon = "fishing", enabled = true }, new { id = "excavation", name = "Cổ vật", icon = "excavation", enabled = true },
        new { id = "mining", name = "Khai khoáng", icon = "mining", enabled = true }, new { id = "insect", name = "Bắt bọ", icon = "insect", enabled = true },
        new { id = "collect", name = "Thu lượm", icon = "collect", enabled = true }, new { id = "monster", name = "Quái vật", icon = "monster", enabled = false, upcoming = true },
        new { id = "farm", name = "Làm vườn", icon = "farm", enabled = true }, new { id = "teleport", name = "Dịch chuyển", icon = "teleport", enabled = true },
        new { id = "log", name = "Log", icon = "log", enabled = true }, new { id = "settings", name = "Cài đặt", icon = "settings", enabled = true },
    ];

    private static readonly object Config = new
    {
        version = "2.4.0", hop_wait_default = 6.0, map_cycle = new[] { 1001, 1301, 1201 }, map_names = TeleStore.MapNames, min_heartbeat_interval = 60, max_idle_seconds = 150,
    };

    /// <summary>Mở cổng HTTPS (chạy nền); lỗi mở cổng / chứng chỉ thì ném lỗi.</summary>
    public static async Task<WebApplication> StartAsync(ServerFiles files, KeyStore keys, TeleStore tele, string host, int port, Action<string> log)
    {
        var license = new LicenseService(keys, files.Secret());
        var limiter = new RateLimiter();
        var certificate = files.Certificate();
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(k =>
        {
            k.Limits.MaxRequestBodySize = MaxBody;
            k.Listen(host is "0.0.0.0" or "" ? IPAddress.Any : IPAddress.Parse(host), port, o => o.UseHttps(certificate));
        });
        var app = builder.Build();
        app.MapGet("/api/tele_positions", () => Raw(tele.Response(0)));
        app.MapPost("/{**path}", async (HttpContext context, string? path) =>
        {
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "";
            var data = await Body(context.Request);
            try
            {
                return Route("/" + path, data, ip, license, limiter, tele);
            }
            catch (Exception e)
            {
                log($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {ip,-15}  LỖI: {e.GetType().Name}: {e.Message}");
                return Reply(false, "Lỗi máy chủ");
            }
        });
        app.MapFallback(() => Reply(false, "Endpoint không tồn tại", status: 404));
        await app.StartAsync();
        return app;
    }

    private static async Task<JsonObject> Body(HttpRequest request)
    {
        try
        {
            if (request.ContentLength is 0 or > MaxBody) return [];
            return await JsonNode.ParseAsync(request.Body) as JsonObject ?? [];
        }
        catch (Exception e) when (e is JsonException or IOException or BadHttpRequestException)
        {
            return [];
        }
    }

    private static string Text(JsonObject data, string name) => data[name] is JsonValue value ? value.ToString() : "";

    private static IResult Route(string path, JsonObject data, string ip, LicenseService license, RateLimiter limiter, TeleStore tele)
    {
        var key = Text(data, "key").Trim().ToLowerInvariant();
        var (device, token, build) = (Text(data, "device"), Text(data, "token"), Text(data, "build"));
        switch (path)
        {
            case "/api/activate":
            case "/api/free":
                if (limiter.Blocked(ip)) return Reply(false, TooMany);
                var (ok, message, payload) = path == "/api/free" ? license.Free(device, build) : license.Activate(key, device, ip, build);
                if (!ok) limiter.Failed(ip);
                return Reply(ok, message, ok ? payload : null);
            case "/api/check":
                var check = license.Check(key, device, token, ip);
                return Reply(check.Ok, check.Message, check.Ok ? check.Payload : null);
            case "/api/release":
                license.Release(key, device, token);
                return Reply(true, "");
            case "/api/ui":
                return Reply(true, "", new() { ["features"] = Features });
            case "/api/config":
                return Reply(true, "", new() { ["config"] = Config });
            case "/api/tele_positions":
                return Raw(tele.Response(int.TryParse(Text(data, "version"), out var version) ? version : 0));
            default:
                return Reply(false, "Endpoint không tồn tại", status: 404);
        }
    }

    /// <summary>{ok, message, ...thêm} dạng JSON UTF-8, không lưu đệm.</summary>
    private static IResult Reply(bool ok, string message, Dictionary<string, object>? extra = null, int status = 200)
    {
        var body = new Dictionary<string, object> { ["ok"] = ok, ["message"] = message };
        foreach (var (name, value) in extra ?? []) body[name] = value;
        return Raw(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(body, Json.Options)), status);
    }

    private static IResult Raw(byte[] json, int status = 200) => new JsonBytes(json, status);

    /// <summary>Trả bytes JSON có sẵn (vị trí TELE dựng sẵn) kèm Cache-Control: no-store.</summary>
    private sealed class JsonBytes(byte[] body, int status) : IResult
    {
        public Task ExecuteAsync(HttpContext context)
        {
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.Headers.CacheControl = "no-store";
            context.Response.ContentLength = body.Length;
            return context.Response.Body.WriteAsync(body).AsTask();
        }
    }
}
