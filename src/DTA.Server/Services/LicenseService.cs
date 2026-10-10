using System.Security.Cryptography;
using System.Text;
using DTA.Server.Store;

namespace DTA.Server.Services;

/// <summary>
/// Nghiệp vụ key: kích hoạt, bản miễn phí, nhịp tim, trả chỗ. Phiên của 1 máy = HMAC-SHA256(secret, "session:key:máy") - máy chủ không phải lưu
/// phiên nào. Chìa giải mã gói tool cũ = HMAC(secret, "bundle:" + mã bản build) (bản C# không có gói mã hoá nên mã build rỗng, chìa rỗng).
/// </summary>
public sealed class LicenseService(KeyStore store, byte[] secret)
{
    private static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private string Hmac(byte[] data) => Convert.ToHexString(HMACSHA256.HashData(secret, data)).ToLowerInvariant();

    public string SessionToken(string key, string device) => Hmac(Encoding.UTF8.GetBytes($"session:{key}:{device}"));

    private bool VerifyToken(string key, string device, string token) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes(SessionToken(key, device)));

    private string CodeKey(string build)
    {
        if (build.Length != 32) return "";
        try
        {
            return Hmac([.. "bundle:"u8.ToArray(), .. Convert.FromHexString(build)]);
        }
        catch (FormatException)
        {
            return "";
        }
    }

    private static bool ValidDevice(string device) => device.Length is >= 8 and <= 64;

    /// <summary>Nhập key: giữ chỗ cho máy, trả phiên + hạn dùng. (được, lý do, dữ liệu trả thêm).</summary>
    public (bool Ok, string Message, Dictionary<string, object> Payload) Activate(string key, string device, string address, string build)
    {
        if (key.Length == 0 || !ValidDevice(device)) return (false, KeyStore.Invalid, []);
        var (problem, item) = store.Use(key, device, address);
        if (problem.Length > 0) return (false, problem, []);
        return (true, "", new()
        {
            ["token"] = SessionToken(item!.Key, device), ["code_key"] = CodeKey(build), ["expires"] = item.Expires, ["note"] = item.Note, ["now"] = Now,
        });
    }

    /// <summary>Bản miễn phí: không cần key, phiên gắn với máy; tool tự giới hạn chức năng.</summary>
    public (bool Ok, string Message, Dictionary<string, object> Payload) Free(string device, string build)
    {
        if (!ValidDevice(device)) return (false, KeyStore.Invalid, []);
        var code = CodeKey(build);
        if (code.Length == 0 && build.Length > 0) return (false, KeyStore.Invalid, []);
        return (true, "", new()
        {
            ["token"] = SessionToken("free", device), ["code_key"] = code, ["free"] = true, ["expires"] = 0, ["note"] = "Bản miễn phí", ["now"] = Now,
        });
    }

    /// <summary>Nhịp tim 60 s của tool: phiên đúng + key còn dùng được thì gia hạn chỗ.</summary>
    public (bool Ok, string Message, Dictionary<string, object> Payload) Check(string key, string device, string token, string address)
    {
        if (!VerifyToken(key, device, token)) return (false, KeyStore.Invalid, []);
        var (problem, item) = store.Use(key, device, address);
        return problem.Length > 0 ? (false, problem, []) : (true, "", new() { ["expires"] = item!.Expires, ["now"] = Now });
    }

    /// <summary>Tool tắt: trả chỗ ngay cho máy khác.</summary>
    public void Release(string key, string device, string token)
    {
        if (VerifyToken(key, device, token)) store.Release(key, device);
    }
}

/// <summary>Chống dò key: 1 địa chỉ sai quá <c>limit</c> lần trong <c>window</c> giây thì chặn tới khi bớt.</summary>
public sealed class RateLimiter(int limit = 10, double window = 60)
{
    private readonly Dictionary<string, List<double>> _failures = [];

    private static double Now => Environment.TickCount64 / 1000.0;

    public bool Blocked(string address)
    {
        lock (_failures)
        {
            var recent = _failures.GetValueOrDefault(address, []).Where(t => Now - t < window).ToList();
            _failures[address] = recent;
            return recent.Count >= limit;
        }
    }

    public void Failed(string address)
    {
        lock (_failures)
        {
            if (!_failures.TryGetValue(address, out var list)) _failures[address] = list = [];
            list.Add(Now);
            if (_failures.Count <= 10000) return;
            _failures.Clear();
            _failures[address] = list;
        }
    }
}
