using System.Diagnostics;
using DTA.Runtime.Core;
using Microsoft.Win32;

namespace DTA.Runtime.Device;

/// <summary>Dò thư mục cài 1 giả lập: registry -> chỗ cài thường gặp trên mọi ổ -> tiến trình đang chạy. Dừng ngay khi thấy.</summary>
internal static class InstallLocator
{
    private static readonly Logger L = Log.For("device");

    public static string Find(IEnumerable<string> registryKeys, IEnumerable<string> folders, IEnumerable<string> processes, Func<string, bool> valid)
    {
        return Candidates(registryKeys, folders, processes).FirstOrDefault(valid) ?? "";
    }

    private static IEnumerable<string> Candidates(IEnumerable<string> registryKeys, IEnumerable<string> folders, IEnumerable<string> processes)
    {
        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        foreach (var key in registryKeys)
        {
            string? value = null;
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, view);
                using var k = root.OpenSubKey(key);
                value = (k?.GetValue("InstallDir") ?? k?.GetValue("InstallPath") ?? k?.GetValue("Path") ?? k?.GetValue(""))?.ToString();
            }
            catch (Exception e)
            {
                L.Swallowed($"registry {key}", e);
            }
            if (!string.IsNullOrEmpty(value)) yield return value;
        }
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed))
        foreach (var folder in folders)
            yield return Path.Combine(drive.Name, folder);
        foreach (var name in processes)
        foreach (var p in Process.GetProcessesByName(name))
        {
            string? dir = null;
            try { dir = Path.GetDirectoryName(p.MainModule?.FileName); } catch (Exception e) { L.Swallowed($"process {name}", e); }
            if (dir != null) yield return dir;
        }
    }
}
