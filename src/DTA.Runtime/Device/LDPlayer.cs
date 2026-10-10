using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using DTA.Runtime.Core;

namespace DTA.Runtime.Device;

/// <summary>LDPlayer 9. Tab thứ n (index n trong ldconsole) có cổng ADB emulator-(5554 + 2n).</summary>
public sealed partial class LDPlayer : IEmulator
{
    public static LDPlayer Instance { get; } = new();

    private static readonly string[] RegistryKeys =
    [
        @"SOFTWARE\XuanZhi\LDPlayer9", @"SOFTWARE\XuanZhi\LDPlayer", @"SOFTWARE\XuanZhi\LDPlayerBox",
        @"SOFTWARE\WOW6432Node\XuanZhi\LDPlayer9", @"SOFTWARE\WOW6432Node\XuanZhi\LDPlayer",
    ];
    private static readonly string[] Folders =
    [
        @"LDPlayer\LDPlayer9", @"Program Files\LDPlayer\LDPlayer9", @"XuanZhi\LDPlayer9", @"LDPlayer\LDPlayer",
        @"Program Files\LDPlayer\LDPlayer", @"XuanZhi\LDPlayer", "LDPlayer9", "LDPlayer",
    ];
    private readonly Dictionary<string, (nint Top, nint View)> _windows = new();
    private string _dir = "";

    private LDPlayer() { }

    public string Name => "LDPlayer";

    public string InstallDir => Valid(_dir) ? _dir : _dir = InstallLocator.Find(RegistryKeys, Folders, ["dnplayer", "ldplayer", "ldbox"], Valid);

    public string AdbPath => InstallDir == "" ? "" : Path.Combine(InstallDir, "adb.exe");

    /// <summary>Tab đang chạy theo `ldconsole list2` (index,tên,hwnd ngoài,hwnd trong,đã vào Android,pid) + `adb devices`.</summary>
    public List<EmulatorInstance> Instances()
    {
        var result = new List<EmulatorInstance>();
        if (InstallDir == "") return result;
        foreach (var console in new[] { "ldconsole.exe", "ld.exe", "dnconsole.exe" })
        {
            var path = Path.Combine(InstallDir, console);
            if (!File.Exists(path)) continue;
            foreach (var line in ProcessRunner.Run(path, ["list2"]).Split('\n'))
            {
                var parts = line.Trim().Split(',');
                if (parts.Length < 5 || !int.TryParse(parts[0], out var index) || parts[4] != "1") continue;
                var serial = $"emulator-{5554 + 2 * index}";
                result.Add(new EmulatorInstance(this, serial, parts[1]));
                if (long.TryParse(parts[2], out var top) && long.TryParse(parts[3], out var view)) _windows[serial] = ((nint)top, (nint)view);
            }
            break;
        }
        foreach (var line in ProcessRunner.Run(AdbPath, ["devices"]).Split('\n'))
        {
            var parts = line.Split((char[])['\t', ' '], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && parts[1] == "device" && parts[0].StartsWith("emulator-") && result.All(r => r.Serial != parts[0]))
                result.Add(new EmulatorInstance(this, parts[0], $"Tab ({parts[0]})"));
        }
        return result;
    }

    public (int X, int Y, int Width, int Height, nint Window)? ScreenRect(string serial) =>
        _windows.TryGetValue(serial, out var w) ? Emulators.ClientRect(w.Top, w.View) : null;

    public string SetupProblem(string serial)
    {
        var match = SerialRegex().Match(serial);
        if (!match.Success) return "";
        var config = Path.Combine(InstallDir, "vms", "config", $"leidian{(int.Parse(match.Groups[1].Value) - 5554) / 2}.config");
        if (!File.Exists(config)) return "";
        try
        {
            var settings = JsonNode.Parse(File.ReadAllText(config))!.AsObject();
            var changed = false;
            if (settings["basicSettings.adbDebug"]?.GetValue<int>() == 0) { settings["basicSettings.adbDebug"] = 1; changed = true; }
            if (settings["basicSettings.rootMode"]?.GetValue<bool>() == false) { settings["basicSettings.rootMode"] = true; changed = true; }
            if (!changed) return "";
            File.WriteAllText(config, settings.ToJsonString(new() { WriteIndented = true }));
            return "Đã tự động bật Root và ADB cho tab này - hãy khởi động lại tab giả lập một lần!";
        }
        catch
        {
            return "Hãy bật Gỡ lỗi ADB và Quyền Root trong Cài đặt LDPlayer > Khác";
        }
    }

    private static bool Valid(string dir) => dir != "" && File.Exists(Path.Combine(dir, "adb.exe")) && File.Exists(Path.Combine(dir, "ldconsole.exe"));

    [GeneratedRegex(@"^emulator-(\d+)$")] private static partial Regex SerialRegex();
}
