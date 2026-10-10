using DTA.Features.Esp;
using DTA.Runtime.Device;

/// <summary>Chạy máy quét ESP không giao diện N giây: in danh sách nhóm / vật gần nhất + nhịp đọc camera: esp giây.</summary>
public static class EspRun
{
    public static void Run(EmulatorDevice device, int seconds, Action<string> step)
    {
        var scanner = new EspScanner(device, new EspOptions { Radius = 0 });
        var lists = 0;
        scanner.Status += (text, error) => step($"[status{(error ? " lỗi" : "")}] {text}");
        scanner.Listed += (rows, counts) =>
        {
            if (lists++ % 10 != 0) return;
            step($"[list] {rows.Count} vật: {string.Join(", ", counts.Select(c => $"{c.Key} {c.Value}"))}");
            foreach (var r in rows.OrderBy(r => r.Distance).Take(4)) step($"   {r.Group} | {r.Kind} {r.Distance:F1} m {r.Note}");
        };
        scanner.Start();
        var end = DateTime.UtcNow.AddSeconds(seconds);
        var looks = 0;
        Look? last = null;
        while (DateTime.UtcNow < end)
        {
            if (scanner.LookAt(EspScanner.Clock) is { } look && look.Time != last?.Time) looks++;
            last = scanner.LookAt(EspScanner.Clock) ?? last;
            Thread.Sleep(16);
        }
        scanner.Stop();
        step($"Khung camera khác nhau ~{looks / (double)seconds:F0}/s, vật trong bộ nhớ {scanner.Things.Count}, camera cuối {last?.Camera} fov {last?.Fov:F1}");
    }
}
