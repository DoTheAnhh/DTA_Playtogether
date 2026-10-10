using System.Text.RegularExpressions;
using DTA.Runtime.Core;

namespace DTA.Runtime.Device;

/// <summary>
/// MEmu Play. Tab thứ n (index n trong memuc) có ADB qua mạng 127.0.0.1:(21503 + 10n) - phải `adb connect` trước khi dùng.
/// Cấu hình root của từng tab nằm trong MemuHyperv VMs\(tên)\(tên).memu (thuộc tính enable_su).
/// </summary>
public sealed partial class MEmu : IEmulator
{
    public static MEmu Instance { get; } = new();

    private const int BasePort = 21503, PortStep = 10;
    private static readonly string[] RegistryKeys =
    [
        @"SOFTWARE\Microvirt\MEmu", @"SOFTWARE\WOW6432Node\Microvirt\MEmu",
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\MEmu",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\MEmu",
    ];
    private static readonly string[] Folders = [@"Program Files\Microvirt\MEmu", @"Microvirt\MEmu", @"MEmu\MEmu", "MEmu"];
    private readonly Dictionary<string, nint> _windows = new();
    private readonly Dictionary<string, string> _vmNames = new();
    private string _dir = "";

    private MEmu() { }

    public string Name => "MEmu";

    public string InstallDir => Valid(_dir) ? _dir : _dir = Normalize(InstallLocator.Find(RegistryKeys, Folders, ["MEmu", "MEmuHeadless"], d => Valid(Normalize(d))));

    public string AdbPath => InstallDir == "" ? "" : Path.Combine(InstallDir, "adb.exe");

    /// <summary>Tab đang chạy theo `memuc listvms` (index,tên,hwnd,đang chạy,pid); mỗi tab được `adb connect` sẵn.</summary>
    public List<EmulatorInstance> Instances()
    {
        var result = new List<EmulatorInstance>();
        var console = Path.Combine(InstallDir, "memuc.exe");
        if (InstallDir == "" || !File.Exists(console)) return result;
        foreach (var line in ProcessRunner.Run(console, ["listvms"]).Split('\n'))
        {
            var parts = line.Trim().Split(',');
            if (parts.Length < 4 || !int.TryParse(parts[0], out var index) || parts[3] != "1") continue;
            var serial = $"127.0.0.1:{BasePort + PortStep * index}";
            ProcessRunner.Run(AdbPath, ["connect", serial], 5);
            result.Add(new EmulatorInstance(this, serial, parts[1]));
            _vmNames[serial] = index == 0 ? "MEmu" : $"MEmu_{index}";
            if (long.TryParse(parts[2], out var hwnd)) _windows[serial] = (nint)hwnd;
        }
        return result;
    }

    public (int X, int Y, int Width, int Height, nint Window)? ScreenRect(string serial)
    {
        if (!_windows.TryGetValue(serial, out var top) || top == 0) return null;
        var view = Emulators.LargestChild(top);
        return Emulators.ClientRect(top, view == 0 ? top : view);
    }

    public string SetupProblem(string serial)
    {
        if (!_vmNames.TryGetValue(serial, out var vm)) return "";
        var config = Path.Combine(InstallDir, "MemuHyperv VMs", vm, $"{vm}.memu");
        if (!File.Exists(config)) return "Hãy bật Root trong Cài đặt MEmu > Kỹ thuật";
        try
        {
            var text = File.ReadAllText(config);
            var fixedText = RootRegex().Replace(text, "name=\"enable_su\" value=\"1\"");
            if (fixedText == text) return "";
            File.WriteAllText(config, fixedText);
            return "Đã tự động bật Root cho tab MEmu này - hãy khởi động lại tab giả lập một lần!";
        }
        catch
        {
            return "Hãy bật Root trong Cài đặt MEmu > Kỹ thuật";
        }
    }

    /// <summary>Registry của MEmu có thể trỏ thư mục gốc (Microvirt) thay vì thư mục chứa adb (Microvirt\MEmu).</summary>
    private static string Normalize(string dir) => dir != "" && !File.Exists(Path.Combine(dir, "memuc.exe")) && File.Exists(Path.Combine(dir, "MEmu", "memuc.exe"))
        ? Path.Combine(dir, "MEmu") : dir;

    private static bool Valid(string dir) => dir != "" && File.Exists(Path.Combine(dir, "adb.exe")) && File.Exists(Path.Combine(dir, "memuc.exe"));

    [GeneratedRegex("name=\"enable_su\" value=\"0\"")] private static partial Regex RootRegex();
}
