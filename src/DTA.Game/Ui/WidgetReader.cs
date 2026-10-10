using System.Collections.Concurrent;
using DTA.Game.Session;
using DTA.Runtime.Core;

namespace DTA.Game.Ui;

/// <summary>Khung 1 widget NGUI trong hệ toạ độ panel (gốc giữa màn hình, y hướng lên) + ma trận world->panel + panel.</summary>
public sealed record WidgetData(float Left, float Bottom, float Right, float Top, byte[] Matrix, long Panel);

/// <summary>Điểm trên màn hình (điểm ảnh) + diện tích (0 = đo qua Transform).</summary>
public readonly record struct ScreenPoint(int X, int Y, float Area);

/// <summary>Tên class gốc NGUI / Unity dùng chung để nhận dạng thành phần giao diện.</summary>
public static class UiClasses
{
    public const string Widget = "UIWidget", Button = "UIButtonColor", Component = "UnityEngine.Component", GameObject = "UnityEngine.GameObject";
}

/// <summary>Đọc vị trí widget NGUI (hình / chữ) trên màn hình - dùng chung theo phiên.</summary>
public sealed class WidgetReader(GameSession session)
{
    private static readonly string[] WidgetFields = ["m_CachedPtr", "panel", "mOldV0", "mOldV1"];
    private readonly ConcurrentDictionary<long, int[]?> _layouts = new();

    /// <summary>Khung + ma trận của widget đang hiện; null nếu đã huỷ / đang ẩn / chưa dựng xong / không phải widget.</summary>
    public WidgetData? Data(long widget)
    {
        var memory = session.Memory;
        var klass = widget != 0 ? (long)memory.U64(widget) : 0;
        var layout = klass == 0 ? null : _layouts.GetOrAdd(klass, k => session.Il2Cpp.Names(k).Contains(UiClasses.Widget)
            ? WidgetFields.Select(f => session.Il2Cpp.Field(k, f)).ToArray()
            : null);
        var data = layout != null ? memory.Read(widget, layout.Max() + 8) : null;
        if (data == null) return null;
        var panel = Bin.U64(data, layout![1]);
        if (Bin.U64(data, layout[0]) == 0 || panel == 0) return null;
        float left = Bin.F32(data, layout[2]), bottom = Bin.F32(data, layout[2] + 4), right = Bin.F32(data, layout[3]), top = Bin.F32(data, layout[3] + 4);
        var matrix = memory.Read(panel + session.Il2Cpp.Field((long)memory.U64(panel), "worldToLocal"), 64);
        return matrix == null || right - left < 1 || top - bottom < 1 ? null : new WidgetData(left, bottom, right, top, matrix, panel);
    }

    /// <summary>
    /// Tâm widget trên màn hình. Ma trận lưu theo cột: ô 0/5 = tỉ lệ (nửa chiều cao màn hình = bao nhiêu đơn vị panel), ô 1/4 ≠ 0 = panel
    /// bị xoay; panel chưa dựng còn ma trận đơn vị. Null nếu ngoài màn hình hoặc panel chưa sẵn sàng.
    /// </summary>
    public ScreenPoint? Point(long widget)
    {
        if (Data(widget) is not { } d) return null;
        float scaleX = Bin.F32(d.Matrix, 0), skewY = Bin.F32(d.Matrix, 4), skewX = Bin.F32(d.Matrix, 16), scaleY = Bin.F32(d.Matrix, 20);
        if (Math.Min(scaleX, scaleY) < 8 || Math.Abs(skewX) > 0.001f * scaleX || Math.Abs(skewY) > 0.001f * scaleY) return null;
        var (width, height) = session.Screen;
        var x = width / 2f + (d.Left + d.Right) / 2 * (height / 2f) / scaleX;
        var y = height / 2f - (d.Bottom + d.Top) / 2 * (height / 2f) / scaleY;
        return x >= 0 && x < width && y >= 0 && y < height ? new ScreenPoint((int)x, (int)y, (d.Right - d.Left) * (d.Top - d.Bottom)) : null;
    }
}
