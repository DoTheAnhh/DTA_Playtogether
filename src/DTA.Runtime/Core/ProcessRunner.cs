using System.Diagnostics;

namespace DTA.Runtime.Core;

/// <summary>Chạy chương trình ngoài không hiện cửa sổ và không bao giờ treo.</summary>
public static class ProcessRunner
{
    private static readonly Logger L = Log.For("process");

    /// <summary>Chạy và lấy stdout (đã cắt khoảng trắng). Lỗi / quá giờ -> chuỗi rỗng.</summary>
    public static string Run(string file, IEnumerable<string> args, double timeoutSeconds = 10)
    {
        try
        {
            using var p = new Process { StartInfo = Hidden(file, args) };
            p.StartInfo.RedirectStandardOutput = true;
            p.Start();
            var output = p.StandardOutput.ReadToEndAsync();
            if (!p.WaitForExit(TimeSpan.FromSeconds(timeoutSeconds)))
            {
                try { p.Kill(true); } catch (Exception e) { L.Swallowed("kill", e); }
                L.Warn($"Quá giờ: {Path.GetFileName(file)} {string.Join(' ', args)}");
                return "";
            }
            return output.Wait(TimeSpan.FromSeconds(2)) ? output.Result.Trim() : "";
        }
        catch (Exception e)
        {
            L.Swallowed($"run {Path.GetFileName(file)}", e);
            return "";
        }
    }

    /// <summary>Thông tin khởi chạy chung: ẩn cửa sổ, không dùng shell, tham số được escape đúng.</summary>
    public static ProcessStartInfo Hidden(string file, IEnumerable<string> args)
    {
        var info = new ProcessStartInfo(file) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        foreach (var a in args) info.ArgumentList.Add(a);
        return info;
    }
}
