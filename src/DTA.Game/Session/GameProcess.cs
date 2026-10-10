using DTA.Runtime.Core;
using DTA.Runtime.Memory;

namespace DTA.Game.Session;

/// <summary>Tiến trình game đang chạy trên 1 tab: package, pid, mã phiên bản.</summary>
public sealed record GameProcess(string Package, int Pid, string VersionCode)
{
    /// <summary>Các bản Play Together hỗ trợ.</summary>
    public static readonly string[] Packages = ["com.vng.playtogether", "com.haegin.playtogether"];
    private static readonly Logger L = Log.For("game");
    private static readonly Dictionary<string, string> Versions = new();

    /// <summary>
    /// Tìm tiến trình game. Game tự tách 1 tiến trình con cùng tên để canh chính nó: tiến trình thật là tiến trình không phải
    /// con của tiến trình nào trong số đó.
    /// </summary>
    public static GameProcess Find(RootShell shell)
    {
        foreach (var package in Packages)
        {
            var pids = shell.Run($"pidof {package}").FirstOrDefault()?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];
            if (pids.Length == 0) continue;
            if (pids.Length > 1)
            {
                var parents = shell.Run(string.Join("; ", pids.Select(p => $"grep PPid: /proc/{p}/status")));
                var children = pids.Zip(parents).Where(p => pids.Contains(p.Second.Split('\t', ' ').LastOrDefault())).Select(p => p.First).ToHashSet();
                pids = pids.Where(p => !children.Contains(p)).DefaultIfEmpty(pids[0]).ToArray();
            }
            var process = new GameProcess(package, int.Parse(pids[0]), VersionOf(shell, package));
            L.Info($"Game {process.Package} pid {process.Pid} bản {process.VersionCode}");
            return process;
        }
        throw new GameError("Chưa mở game Play Together trên giả lập này");
    }

    private static string VersionOf(RootShell shell, string package)
    {
        lock (Versions)
        {
            if (Versions.TryGetValue(package, out var cached)) return cached;
            var line = string.Join(' ', shell.Run($"dumpsys package {package} | grep versionCode"));
            var code = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(p => p.StartsWith("versionCode="))?["versionCode=".Length..] ?? "0";
            return Versions[package] = code;
        }
    }
}
