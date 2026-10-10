using System.Runtime.InteropServices;

namespace DTA.Runtime.Device;

/// <summary>1 tab giả lập đang chạy: loại giả lập + serial ADB + tên tab.</summary>
public sealed record EmulatorInstance(IEmulator Emulator, string Serial, string Name)
{
    public override string ToString() => $"{Name}  ({Serial})";
}

/// <summary>1 loại giả lập (LDPlayer, MEmu...): tìm chỗ cài, liệt kê tab, khung hình trên màn hình, tự sửa cài đặt thiếu.</summary>
public interface IEmulator
{
    string Name { get; }
    /// <summary>adb.exe đi kèm giả lập; rỗng nếu chưa cài.</summary>
    string AdbPath { get; }
    /// <summary>Các tab đang chạy (đã vào Android).</summary>
    List<EmulatorInstance> Instances();
    /// <summary>Khung hình Android của tab trên màn hình máy tính; null nếu không xác định / đang thu nhỏ.</summary>
    (int X, int Y, int Width, int Height, nint Window)? ScreenRect(string serial);
    /// <summary>Tự bật ADB + root nếu đang tắt; trả lời nhắc cho người dùng (rỗng nếu không có gì).</summary>
    string SetupProblem(string serial);
}

/// <summary>Danh sách giả lập hỗ trợ + phần dùng chung giữa chúng.</summary>
public static class Emulators
{
    public static IReadOnlyList<IEmulator> All { get; } = [LDPlayer.Instance, MEmu.Instance];

    public static IEmulator? ByName(string name) => All.FirstOrDefault(e => e.Name == name);

    /// <summary>Khung client của cửa sổ <paramref name="view"/> trên màn hình (bỏ qua nếu <paramref name="top"/> ẩn / thu nhỏ).</summary>
    internal static (int X, int Y, int Width, int Height, nint Window)? ClientRect(nint top, nint view)
    {
        if (view == 0 || !IsWindow(view) || !IsWindowVisible(top) || IsIconic(top)) return null;
        var corner = new POINT();
        if (!GetClientRect(view, out var rect) || !ClientToScreen(view, ref corner)) return null;
        int width = rect.Right - rect.Left, height = rect.Bottom - rect.Top;
        return width > 50 && height > 50 ? (corner.X, corner.Y, width, height, top) : null;
    }

    /// <summary>Cửa sổ con lớn nhất (khung hiển thị Android nằm trong cửa sổ giả lập).</summary>
    internal static nint LargestChild(nint top)
    {
        nint best = 0;
        var bestArea = 0;
        EnumChildWindows(top, (child, _) =>
        {
            if (IsWindowVisible(child) && GetClientRect(child, out var r) && (r.Right - r.Left) * (r.Bottom - r.Top) > bestArea)
            {
                bestArea = (r.Right - r.Left) * (r.Bottom - r.Top);
                best = child;
            }
            return true;
        }, 0);
        return best;
    }

    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }
    private delegate bool EnumProc(nint hWnd, nint lParam);
    [DllImport("user32.dll")] private static extern bool IsWindow(nint hWnd);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hWnd);
    [DllImport("user32.dll")] private static extern bool IsIconic(nint hWnd);
    [DllImport("user32.dll")] private static extern bool GetClientRect(nint hWnd, out RECT rect);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(nint hWnd, ref POINT point);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(nint parent, EnumProc proc, nint lParam);
}
