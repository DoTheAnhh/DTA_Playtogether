using System.Diagnostics;
using System.Text.RegularExpressions;
using DTA.Runtime.Core;

namespace DTA.Runtime.Device;

/// <summary>1 tab giả lập (LDPlayer / MEmu...), điều khiển qua ADB.</summary>
public sealed partial class EmulatorDevice(IEmulator emulator, string serial)
{
    /// <summary>Màn hình gốc dùng khi đo toạ độ nút.</summary>
    public const int BaseWidth = 1600, BaseHeight = 900;

    public IEmulator Emulator { get; } = emulator;
    public string Serial { get; } = serial;
    public string AdbPath => Emulator.AdbPath;

    /// <summary>Thông số cảm ứng đọc 1 lần cho tab này (xem <see cref="Touch"/>).</summary>
    internal TouchParams? TouchParams { get; set; }

    public static EmulatorDevice Of(EmulatorInstance instance) => new(instance.Emulator, instance.Serial);

    public string Adb(IEnumerable<string> args, double timeout = 10) => ProcessRunner.Run(AdbPath, ["-s", Serial, .. args], timeout);

    public string Shell(string command, double timeout = 10) => Adb(["shell", command], timeout);

    public bool IsOnline() => Shell("echo ok", 5) == "ok";

    public string SetupProblem() => Emulator.SetupProblem(Serial);

    public (int X, int Y, int Width, int Height, nint Window)? ScreenRect() => Emulator.ScreenRect(Serial);

    public (int Width, int Height) ScreenSize()
    {
        var m = SizeRegex().Match(Shell("dumpsys window displays | grep cur="));
        return m.Success ? (int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value)) : (BaseWidth, BaseHeight);
    }

    /// <summary>1 phiên shell giữ nguyên, dữ liệu nhị phân, không PTY (mở adb.exe tốn ~400 ms nên giữ lại dùng mãi).</summary>
    public Process OpenShell(string command = "")
    {
        var args = new List<string> { "-s", Serial, "shell", "-T" };
        if (command != "") args.Add(command);
        var info = ProcessRunner.Hidden(AdbPath, args);
        info.RedirectStandardInput = info.RedirectStandardOutput = true;
        return Process.Start(info) ?? throw new DeviceError($"Không mở được ADB tới {Serial}", true);
    }

    /// <summary>Đổi toạ độ gốc 1600x900 sang màn w x h, neo ngang theo chỗ game neo món đó ("right", "center", "left").</summary>
    public static (int X, int Y) Scale(int x, int y, int w, int h, string anchor = "right")
    {
        var s = (double)h / BaseHeight;
        return anchor switch
        {
            "center" => ((int)(w / 2.0 + (x - BaseWidth / 2.0) * s), (int)(y * s)),
            "left" => ((int)(x * s), (int)(y * s)),
            _ => ((int)(w - (BaseWidth - x) * s), (int)(y * s)),
        };
    }

    [GeneratedRegex(@"cur=(\d+)x(\d+)")] internal static partial Regex SizeRegex();
}
