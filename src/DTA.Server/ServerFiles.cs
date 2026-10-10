using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace DTA.Server;

/// <summary>
/// File của máy chủ trong thư mục dữ liệu (mặc định cạnh file chạy): server.db, cert.pem + key.pem (TLS), secret.key (khoá ký phiên), port.txt,
/// data/tele_positions.json. cert / key / secret KHÔNG nằm trong mã nguồn - chép từ máy chủ cũ sang.
/// </summary>
public sealed class ServerFiles(string dir)
{
    public const int DefaultPort = 28445;
    public string Dir { get; } = Path.GetFullPath(dir);
    public string Database => Path.Combine(Dir, "server.db");
    public string TeleJson => Path.Combine(Dir, "data", "tele_positions.json");

    /// <summary>Cổng ghi trong port.txt (nếu có).</summary>
    public int Port => int.TryParse(Read("port.txt")?.Trim(), out var port) ? port : DefaultPort;

    private string? Read(string name) => File.Exists(Path.Combine(Dir, name)) ? File.ReadAllText(Path.Combine(Dir, name)) : null;

    /// <summary>Khoá ký phiên (hex trong secret.key).</summary>
    public byte[] Secret() => Convert.FromHexString(Read("secret.key")?.Trim() ?? throw new FileNotFoundException("thiếu secret.key"));

    /// <summary>Chứng chỉ TLS từ cert.pem + key.pem (xuất lại PKCS#12 để Windows dùng được khoá riêng).</summary>
    public X509Certificate2 Certificate()
    {
        using var pem = X509Certificate2.CreateFromPemFile(Path.Combine(Dir, "cert.pem"), Path.Combine(Dir, "key.pem"));
        return new X509Certificate2(pem.Export(X509ContentType.Pkcs12));
    }
}

/// <summary>Tài khoản quản trị mở panel (mật khẩu lưu dạng băm PBKDF2-SHA256 200 000 vòng, giống bản Python).</summary>
public static class Admin
{
    private const string User = "dotheanh";
    private const string PasswordHash = "0768b26370e11f901ad12b349d3af9ec:b883a805e89d1ac3077d610e320ea469b14f5c41f2d4cc4903c009965a038315";

    public static bool Check(string user, string password)
    {
        var salt = PasswordHash[..PasswordHash.IndexOf(':')];
        var digest = salt + ":" + Convert.ToHexString(Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), Convert.FromHexString(salt), 200000, HashAlgorithmName.SHA256, 32)).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(user.Trim().ToLowerInvariant()), Encoding.UTF8.GetBytes(User))
               & CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(digest), Encoding.UTF8.GetBytes(PasswordHash));
    }
}
