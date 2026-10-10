using DTA.Server;
using DTA.Server.Http;
using DTA.Server.Store;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;

// Máy chủ key chạy không giao diện (VPS / dịch vụ): DTA_Server [--dir thư_mục] [--host 127.0.0.1 | 0.0.0.0 khi lên VPS] [--port 28445]. Có panel quản lý thì dùng DTA_ServerPanel.
Console.OutputEncoding = System.Text.Encoding.UTF8;
var options = args.Select((a, i) => (a, i)).Where(p => p.a.StartsWith("--") && p.i + 1 < args.Length).ToDictionary(p => p.a[2..], p => args[p.i + 1]);
var files = new ServerFiles(options.GetValueOrDefault("dir", AppContext.BaseDirectory));
var host = options.GetValueOrDefault("host", "127.0.0.1");
var port = int.TryParse(options.GetValueOrDefault("port"), out var p) ? p : files.Port;
using var keys = new KeyStore(files.Database);
using var db = KeyStore.Open(files.Database);
var tele = new TeleStore(db, files.TeleJson);
try
{
    var app = await ApiHost.StartAsync(files, keys, tele, host, port, Console.WriteLine);
    Console.WriteLine($"Máy chủ đang chạy tại cổng {port} (TLS). Dữ liệu: {files.Dir}");
    await app.WaitForShutdownAsync();
}
catch (Exception e) when (e is IOException or System.Security.Cryptography.CryptographicException or FormatException or SqliteException)
{
    Console.WriteLine($"Không thể mở máy chủ ở cổng {port}: {e.Message}");
    return 2;
}
return 0;
