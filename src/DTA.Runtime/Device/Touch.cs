using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using DTA.Runtime.Core;

namespace DTA.Runtime.Device;

internal sealed record TouchParams(int Width, int Height, bool Wide, string Node);

/// <summary>
/// Bấm / giữ màn hình giả lập: ghi sự kiện cảm ứng thô vào /dev/input (game nhận sau vài ms). Không xác định được thiết bị
/// cảm ứng / màn đang xoay thì dùng `input tap` (chậm ~200 ms, không giữ / kéo được - xem <see cref="CanHold"/>).
/// Mỗi thao tác dùng 1 "ngón ảo" riêng (slot 7-9) nên không cắt ngang thao tác chuột của người dùng (slot 0).
/// </summary>
public sealed partial class Touch : IDisposable
{
    private const ushort EvSyn = 0, EvKey = 1, EvAbs = 3;
    private const ushort Slot = 0x2F, PosX = 0x35, PosY = 0x36, TrackingId = 0x39, Pressure = 0x3A, BtnTouch = 0x14A;
    private const int TapSlot = 9, HoldSlot = 8, PressSlot = 7;

    private static readonly Logger L = Log.For("touch");
    private readonly EmulatorDevice _device;
    private readonly TouchParams _params;
    private Process? _proc;
    private int _touchId;

    public int Width => _params.Width;
    public int Height => _params.Height;
    public bool CanHold => _params.Node != "";
    public bool Holding { get; private set; }
    public bool Pressing { get; private set; }

    public Touch(EmulatorDevice device)
    {
        _device = device;
        _params = device.TouchParams ??= Probe(device);
        Open();
    }

    public void Tap(int x, int y) => Write(() => CanHold ? TapPacket(x, y) : Encoding.ASCII.GetBytes($"input tap {x} {y}\n"));

    /// <summary>Đặt ngón giữ xuống (x, y); đang giữ thì kéo tới đó (cần điều khiển).</summary>
    public void Hold(int x, int y)
    {
        Write(() => Finger(HoldSlot, x, y, start: !Holding));
        Holding = true;
    }

    public void Release()
    {
        if (!Holding) return;
        Write(() => Lift(HoldSlot));
        Holding = false;
    }

    /// <summary>Đè giữ 1 nút bằng ngón thứ 3 tới khi <see cref="Unpress"/>.</summary>
    public void Press(int x, int y)
    {
        if (Pressing) return;
        Write(() => Finger(PressSlot, x, y, start: true));
        Pressing = true;
    }

    public void Unpress()
    {
        if (!Pressing) return;
        Write(() => Lift(PressSlot));
        Pressing = false;
    }

    public void Dispose()
    {
        try { _proc?.Kill(); } catch (Exception e) { L.Swallowed("touch kill", e); }
        _proc?.Dispose();
        _proc = null;
    }

    /// <summary>Mở (lại) kênh ghi sự kiện; kênh cũ đóng thì giả lập tự nhấc mọi ngón của kênh đó.</summary>
    private void Open()
    {
        Dispose();
        Holding = Pressing = false;
        _proc = _device.OpenShell(CanHold ? $"cat > {_params.Node}" : "");
    }

    private void Write(Func<byte[]> packet)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                if (_proc == null || _proc.HasExited) throw new IOException("kênh bấm đã đóng");
                var data = packet();
                _proc.StandardInput.BaseStream.Write(data);
                _proc.StandardInput.BaseStream.Flush();
                return;
            }
            catch (Exception e) when (e is IOException or InvalidOperationException or ObjectDisposedException)
            {
                L.Warn($"Kênh bấm {_device.Serial} bị ngắt ({e.Message}) - mở lại");
                Open();
            }
        }
        throw new DeviceError("Mất kết nối ADB với giả lập", true);
    }

    /// <summary>Nhấn + nhả trong 1 lần ghi; không nhả BTN_TOUCH (làm đứt thao tác người dùng đang giữ); trả slot về 0.</summary>
    private byte[] TapPacket(int x, int y)
    {
        using var ms = new MemoryStream();
        ms.Write(Event(EvAbs, Slot, TapSlot)); ms.Write(Event(EvAbs, TrackingId, NextId()));
        ms.Write(Event(EvAbs, PosX, Safe(x))); ms.Write(Event(EvAbs, PosY, Safe(y)));
        ms.Write(Event(EvAbs, Pressure, 1)); ms.Write(Event(EvKey, BtnTouch, 1)); ms.Write(Event(EvSyn, 0, 0));
        ms.Write(Event(EvAbs, TrackingId, -1)); ms.Write(Event(EvSyn, 0, 0));
        ms.Write(Event(EvAbs, Slot, 0)); ms.Write(Event(EvSyn, 0, 0));
        return ms.ToArray();
    }

    private byte[] Finger(int slot, int x, int y, bool start)
    {
        using var ms = new MemoryStream();
        ms.Write(Event(EvAbs, Slot, slot));
        if (start) ms.Write(Event(EvAbs, TrackingId, NextId()));
        ms.Write(Event(EvAbs, PosX, Safe(x))); ms.Write(Event(EvAbs, PosY, Safe(y)));
        ms.Write(Event(EvAbs, Pressure, 1)); ms.Write(Event(EvKey, BtnTouch, 1)); ms.Write(Event(EvSyn, 0, 0));
        ms.Write(Event(EvAbs, Slot, 0)); ms.Write(Event(EvSyn, 0, 0));
        return ms.ToArray();
    }

    private byte[] Lift(int slot)
    {
        using var ms = new MemoryStream();
        ms.Write(Event(EvAbs, Slot, slot)); ms.Write(Event(EvAbs, TrackingId, -1)); ms.Write(Event(EvSyn, 0, 0));
        ms.Write(Event(EvAbs, Slot, 0)); ms.Write(Event(EvSyn, 0, 0));
        return ms.ToArray();
    }

    private int NextId() => _touchId = Safe(_touchId % 60000 + 1);

    /// <summary>1 struct input_event: timeval (2 x long) rồi type, code, value.</summary>
    private byte[] Event(ushort type, ushort code, int value)
    {
        var buffer = new byte[_params.Wide ? 24 : 16];
        var at = _params.Wide ? 16 : 8;
        BitConverter.TryWriteBytes(buffer.AsSpan(at), type);
        BitConverter.TryWriteBytes(buffer.AsSpan(at + 2), code);
        BitConverter.TryWriteBytes(buffer.AsSpan(at + 4), value);
        return buffer;
    }

    /// <summary>adb.exe trên Windows đọc stdin kiểu văn bản: 0x1A là hết dữ liệu, 0D 0A bị gộp -> nhích giá trị tránh chúng.</summary>
    private static int Safe(int value)
    {
        while (true)
        {
            var b = BitConverter.GetBytes(value);
            if (Array.IndexOf(b, (byte)0x1A) < 0 && !(Contains(b, 0x0D, 0x0A))) return value;
            value++;
        }
    }

    private static bool Contains(byte[] b, byte first, byte second)
    {
        for (var i = 0; i + 1 < b.Length; i++) if (b[i] == first && b[i + 1] == second) return true;
        return false;
    }

    private static TouchParams Probe(EmulatorDevice device)
    {
        var info = device.Shell("dumpsys window displays | grep cur=; dumpsys input | grep SurfaceOrientation; uname -m; getevent -p 2>/dev/null");
        var size = EmulatorDevice.SizeRegex().Match(info);
        int width = size.Success ? int.Parse(size.Groups[1].Value) : EmulatorDevice.BaseWidth;
        int height = size.Success ? int.Parse(size.Groups[2].Value) : EmulatorDevice.BaseHeight;
        var upright = OrientationRegex().Match(info) is { Success: true } m && m.Groups[1].Value == "0";
        var wide = ArchRegex().IsMatch(info);
        return new TouchParams(width, height, wide, upright ? FindNode(info, width, height) : "");
    }

    private static string FindNode(string getevent, int width, int height)
    {
        foreach (var block in getevent.Split("add device").Skip(1))
        {
            var path = NodeRegex().Match(block);
            var maxX = MaxRegex("0035").Match(block);
            var maxY = MaxRegex("0036").Match(block);
            if (path.Success && maxX.Success && maxY.Success
                && int.Parse(maxX.Groups[1].Value) + 1 == width && int.Parse(maxY.Groups[1].Value) + 1 == height)
                return path.Groups[1].Value;
        }
        return "";
    }

    private static Regex MaxRegex(string code) => new(code + @"\s+: value -?\d+, min 0, max (\d+)");
    [GeneratedRegex(@"SurfaceOrientation: (\d)")] private static partial Regex OrientationRegex();
    [GeneratedRegex(@"x86_64|aarch64|arm64")] private static partial Regex ArchRegex();
    [GeneratedRegex(@"(/dev/input/event\d+)")] private static partial Regex NodeRegex();
}
