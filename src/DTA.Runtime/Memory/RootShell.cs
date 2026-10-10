using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using DTA.Runtime.Core;
using DTA.Runtime.Device;

namespace DTA.Runtime.Memory;

/// <summary>
/// 1 phiên `su` giữ nguyên trên giả lập: mỗi lệnh ~1-6 ms vì không mở lại adb.exe. Lệnh được bọc giữa 2 dòng đánh dấu
/// (@@B n@@ / @@E n@@) để tách đúng kết quả. Mất kết nối thì tự mở lại 1 lần.
/// </summary>
public sealed class RootShell : IDisposable
{
    private static readonly Logger L = Log.For("shell");
    private readonly EmulatorDevice _device;
    private readonly object _lock = new();
    private Process _proc = null!;
    private BlockingCollection<string?> _lines = null!;
    private int _seq;

    public RootShell(EmulatorDevice device)
    {
        _device = device;
        try
        {
            Start();
            if (!IsRoot(12)) throw new GameError("no root");
            QuickAck();
        }
        catch (Exception e) when (e is not GameError || e.Message == "no root")
        {
            Dispose();
            if (device.IsOnline()) throw new GameError(device.SetupProblem() is { Length: > 0 } p ? p : "Giả lập chưa bật quyền root (su)");
            throw new GameError(device.SetupProblem() is { Length: > 0 } q ? q : $"Chưa kết nối được giả lập ({device.Serial})", true);
        }
    }

    /// <summary>Chạy 1 lệnh, trả các dòng kết quả. Mất kết nối -> mở lại phiên 1 lần rồi chạy lại.</summary>
    public List<string> Run(string command, double timeout = 5)
    {
        lock (_lock)
        {
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    return Execute(command, timeout);
                }
                catch (GameError e) when (attempt == 0 && e.Retry)
                {
                    L.Warn($"Phiên su {_device.Serial} lỗi ({e.Message}) - mở lại");
                    try
                    {
                        Start();
                        if (!IsRoot(8)) throw new GameError("Mất quyền root sau khi khôi phục ADB", true);
                        QuickAck();
                    }
                    catch
                    {
                        throw e;
                    }
                }
            }
        }
    }

    public void Dispose()
    {
        try { _proc?.Kill(); } catch (Exception e) { L.Swallowed("shell kill", e); }
        _proc?.Dispose();
    }

    private void Start()
    {
        Dispose();
        _proc = _device.OpenShell("su");
        _lines = new BlockingCollection<string?>(new ConcurrentQueue<string?>());
        _seq = 0;
        var proc = _proc;
        var lines = _lines;
        new Thread(() => Pump(proc, lines)) { IsBackground = true, Name = $"RootShell {_device.Serial}" }.Start();
    }

    /// <summary>
    /// Bật quickack cho mọi tuyến mạng của giả lập (ip route change ... quickack 1): mặc định Linux trong giả lập trì hoãn ACK ~40 ms nên
    /// MỖI lượt lệnh qua ADB chờ thêm ~40 ms (Nagle phía ADB đợi ACK) - bật lên 1 lượt đọc còn 1-3 ms. Chỉ đổi thuộc tính ACK của tuyến
    /// có sẵn, không đổi đường đi; giả lập khởi động lại thì mất, mỗi phiên bật lại. Lỗi thì bỏ qua.
    /// </summary>
    private void QuickAck()
    {
        try
        {
            Execute("ip route show table all 2>/dev/null | grep -v -e '^local' -e '^broadcast' -e '^unreachable' -e '^multicast' -e quickack "
                    + "| while read r; do ip route change $r quickack 1 2>/dev/null; done", 5);
        }
        catch (GameError e)
        {
            L.Swallowed("quickack", e);
        }
    }

    private bool IsRoot(double timeout)
    {
        try { return Execute("id -u", timeout) is ["0"]; }
        catch (GameError) { return false; }
    }

    private List<string> Execute(string command, double timeout)
    {
        var seq = ++_seq;
        string begin = $"@@B{seq}@@", end = $"@@E{seq}@@";
        try
        {
            var bytes = Encoding.UTF8.GetBytes($"echo {begin}; {command}; echo; echo {end}\n");
            _proc.StandardInput.BaseStream.Write(bytes);
            _proc.StandardInput.BaseStream.Flush();
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or ObjectDisposedException)
        {
            throw new GameError("Mất kết nối ADB với giả lập", true);
        }
        var output = new List<string>();
        var started = false;
        var deadline = Environment.TickCount64 + (long)(timeout * 1000);
        while (true)
        {
            var wait = (int)Math.Max(10, deadline - Environment.TickCount64);
            if (!_lines.TryTake(out var line, wait)) throw new GameError("Giả lập không phản hồi", true);
            if (line == null) throw new GameError("Mất kết nối ADB với giả lập", true);
            if (line == begin) started = true;
            else if (line == end && started)
            {
                if (output.Count > 0 && output[^1] == "") output.RemoveAt(output.Count - 1);
                return output;
            }
            else if (started) output.Add(line);
        }
    }

    private static void Pump(Process proc, BlockingCollection<string?> lines)
    {
        try
        {
            var stream = proc.StandardOutput.BaseStream;
            var buffer = new byte[64 * 1024];
            var line = new MemoryStream();
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (var i = 0; i < read; i++)
                {
                    if (buffer[i] == (byte)'\n')
                    {
                        var text = Encoding.UTF8.GetString(line.GetBuffer(), 0, (int)line.Length).TrimEnd('\r');
                        lines.Add(text);
                        line.SetLength(0);
                    }
                    else line.WriteByte(buffer[i]);
                }
            }
        }
        catch (Exception e)
        {
            L.Swallowed("shell pump", e);
        }
        lines.Add(null);
    }
}
