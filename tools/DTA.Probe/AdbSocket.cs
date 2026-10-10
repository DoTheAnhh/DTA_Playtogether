using System.Diagnostics;
using System.Net.Sockets;
using System.Text;

/// <summary>Thử: nói chuyện thẳng với ADB server qua socket (TCP_NODELAY) bằng exec:su, đo 1 vòng lệnh: adbsock serial.</summary>
public static class AdbSocket
{
    public static void Run(string serial, Action<string> step)
    {
        using var tcp = new TcpClient { NoDelay = true };
        tcp.Connect("127.0.0.1", 5037);
        var s = tcp.GetStream();
        void Send(string req)
        {
            var b = Encoding.ASCII.GetBytes($"{req.Length:X4}{req}");
            s.Write(b);
            var ok = new byte[4];
            s.ReadExactly(ok);
            step($"{req} -> {Encoding.ASCII.GetString(ok)}");
        }
        Send($"host:transport:{serial}");
        Send(Environment.GetEnvironmentVariable("ADBX") ?? "exec:sh");
        var buffer = new byte[65536];
        var pending = new StringBuilder();
        string Round(string cmd, string marker)
        {
            s.Write(Encoding.ASCII.GetBytes($"{cmd}; echo {marker}\n"));
            while (!pending.ToString().Contains(marker))
            {
                var n = s.Read(buffer);
                if (n <= 0) return "EOF";
                pending.Append(Encoding.ASCII.GetString(buffer, 0, n));
            }
            var text = pending.ToString();
            pending.Clear();
            return text.Trim();
        }
        for (var i = 0; i < 6; i++)
        {
            var sw = Stopwatch.StartNew();
            var r = Round("true", $"@@{i}@@");
            step($"vòng {i}: {sw.Elapsed.TotalMilliseconds:F1} ms ({r})");
        }
    }
}
