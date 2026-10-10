using System.Collections.Concurrent;
using DTA.Game.Ui;
using DTA.Runtime.Core;

namespace DTA.Game.World;

/// <summary>Phần kiểm chứng bố cục native + chiếu object giao diện lên màn hình của <see cref="WorldReader"/>.</summary>
public sealed partial class WorldReader
{
    private NativeLayout? _layout;
    private readonly ConcurrentDictionary<long, (float X, float Y)> _uiCameras = new();

    /// <summary>Hình / chữ của bảng đang mở, dùng làm vật đối chiếu khi màn chơi bị che.</summary>
    public List<long> ReferenceWidgets { get; set; } = [];

    /// <summary>Đã kiểm xong bố cục (được hoặc không hỗ trợ).</summary>
    public bool LayoutKnown => _layout != null;

    /// <summary>
    /// Bố cục native đã kiểm chứng. Thử từng bộ <see cref="NativeLayout.Candidates"/> trên 1 nút đã biết vị trí từ NGUI: đi đúng tới
    /// Transform, cờ hiện = 1, đúng lớp vẽ, và đổi ra màn hình qua ma trận panel / qua camera ra cùng 1 chỗ.
    /// Null = chưa có gì để đối chiếu (lần sau thử lại); <see cref="NativeLayout.Unsupported"/> = bản Unity này không đọc được.
    /// </summary>
    public NativeLayout? Layout()
    {
        if (_layout != null) return _layout;
        var memory = session.Memory;
        var il2cpp = session.Il2Cpp;
        var widgets = session.Widgets;
        var (widget, data) = session.Ui.Hud().Values.Concat(ReferenceWidgets)
            .Select(w => (w, d: widgets.Data(w))).FirstOrDefault(p => p.d != null);
        if (data == null || widgets.Point(widget) == null) return null;
        float scaleX = Bin.F32(data.Matrix, 0), scaleY = Bin.F32(data.Matrix, 20), offsetX = Bin.F32(data.Matrix, 48), offsetY = Bin.F32(data.Matrix, 52);
        var klass = (long)memory.U64(widget);
        var cached = il2cpp.Field(klass, "m_CachedPtr");
        var transform = (long)memory.U64((long)memory.U64(widget + il2cpp.Field(klass, "mTrans")) + cached);
        var camera = (long)memory.U64(widget + il2cpp.Field(klass, "mCam"));
        var layer = memory.Read(data.Panel + il2cpp.Field((long)memory.U64(data.Panel), "mLayer"), 4);
        var (width, height) = session.Screen;
        var recognized = false;
        foreach (var (componentGo, goComponents, goActive, hierarchy) in NativeLayout.Candidates)
        {
            var gameObject = (long)memory.U64((long)memory.U64(widget + cached) + componentGo);
            if (transform == 0 || (long)memory.U64((long)memory.U64(gameObject + goComponents) + 8) != transform) continue;
            var flags = memory.Read(gameObject + goActive - 7, 8);
            if (flags == null || layer == null || !flags.AsSpan(0, 4).SequenceEqual(layer) || flags[6] != 1 || flags[7] != 1) continue;
            recognized = true;
            var position = TransformMath.Position(memory, transform, hierarchy);
            var cameraObject = camera != 0 ? (long)memory.U64((long)memory.U64(camera + cached) + componentGo) : 0;
            var center = TransformMath.Position(memory, (long)memory.U64((long)memory.U64(cameraObject + goComponents) + 8), hierarchy);
            if (position is not { } p || center is not { } c) continue;
            float localX = p.X * scaleX + offsetX, localY = p.Y * scaleY + offsetY;
            var x = width / 2f + (p.X - c.X) * height / 2f;
            var y = height / 2f - (p.Y - c.Y) * height / 2f;
            var sameX = Math.Abs(x - (width / 2f + localX * (height / 2f) / scaleX)) <= 2;
            var sameY = Math.Abs(y - (height / 2f - localY * (height / 2f) / scaleY)) <= 2;
            var inside = localX >= data.Left - 1 && localX <= data.Right + 1 && localY >= data.Bottom - 1 && localY <= data.Top + 1;
            if (!inside || !sameX || !sameY) continue;
            _layout = new NativeLayout(cached, componentGo, goComponents, goActive, hierarchy, c.X, c.Y);
            L.Info($"Bố cục Unity: GO 0x{componentGo:X}, Transform 0x{hierarchy:X}");
            return _layout;
        }
        if (recognized) return null;
        L.Warn("Bản Unity này có bố cục lạ - không đọc được vị trí object native");
        return _layout = NativeLayout.Unsupported;
    }

    /// <summary>(Transform, GameObject đang hiện) của GameObject chứa component.</summary>
    public (long Transform, bool Shown) TransformOf(long component)
    {
        if (Layout() is not { Supported: true } l || component == 0) return (0, false);
        var memory = session.Memory;
        var gameObject = (long)memory.U64((long)memory.U64(component + l.Cached) + l.ComponentGo);
        var head = gameObject != 0 ? memory.Read(gameObject + l.GoComponents, l.GoActive + 1 - l.GoComponents) : null;
        return head != null ? ((long)memory.U64(Bin.U64(head, 0) + 8), head[^1] == 1) : (0, false);
    }

    /// <summary>
    /// Điểm màn hình của GameObject chứa component (nút không có hình nền): vị trí Transform chiếu qua camera giao diện trực giao
    /// (nửa chiều cao màn hình = 1 đơn vị). <paramref name="camera"/> ≠ 0 = camera riêng (nút trên đầu vật thể), đọc tâm 1 lần.
    /// </summary>
    public ScreenPoint? ComponentPoint(long component, long camera = 0)
    {
        if (Layout() is not { Supported: true } l) return null;
        var (transform, shown) = TransformOf(component);
        if (!shown || TransformMath.Position(session.Memory, transform, l.Hierarchy) is not { } p) return null;
        var (cx, cy) = (l.CameraX, l.CameraY);
        if (camera != 0)
        {
            if (!_uiCameras.TryGetValue(camera, out var center))
            {
                if (TransformMath.Position(session.Memory, TransformOf(camera).Transform, l.Hierarchy) is not { } c) return null;
                _uiCameras[camera] = center = (c.X, c.Y);
            }
            (cx, cy) = center;
        }
        var (width, height) = session.Screen;
        var x = width / 2f + (p.X - cx) * height / 2f;
        var y = height / 2f - (p.Y - cy) * height / 2f;
        return x >= 0 && x < width && y >= 0 && y < height ? new ScreenPoint((int)x, (int)y, 0) : null;
    }

    /// <summary>{object: đang hiện} cho cả loạt object (component hoặc GameObject) trong 3 lượt đọc; object không đọc được thì vắng mặt.</summary>
    public Dictionary<long, bool> ShownFlags(IReadOnlyList<long> objects)
    {
        if (objects.Count == 0 || Layout() is not { Supported: true } l) return [];
        var memory = session.Memory;
        var isObject = session.Managed.Names(objects[0]).Contains(UiClasses.GameObject);
        var natives = memory.ReadObjects(objects, l.Cached + 8).ToDictionary(p => p.Key, p => Bin.U64(p.Value, l.Cached));
        var gameObjects = isObject ? natives : memory.ReadObjects(natives.Values.Where(n => n != 0), l.ComponentGo + 8) is var raws
            ? natives.Where(p => raws.ContainsKey(p.Value)).ToDictionary(p => p.Key, p => Bin.U64(raws[p.Value], l.ComponentGo))
            : [];
        var flags = memory.ReadObjects(gameObjects.Values.Where(g => g != 0), l.GoActive + 1);
        return gameObjects.Where(p => flags.ContainsKey(p.Value)).ToDictionary(p => p.Key, p => flags[p.Value][l.GoActive] == 1);
    }

    /// <summary>GameObject (hoặc GameObject chứa component) có đang hiện không; null nếu không đọc được.</summary>
    public bool? IsShown(long obj)
    {
        if (Layout() is not { Supported: true } l) return null;
        var names = session.Managed.Names(obj);
        if (names.Count == 0) return null;
        var native = (long)session.Memory.U64(obj + l.Cached);
        var gameObject = names.Contains(UiClasses.GameObject) ? native : names.Contains(UiClasses.Component) ? (long)session.Memory.U64(native + l.ComponentGo) : 0;
        var flag = gameObject != 0 ? session.Memory.Read(gameObject + l.GoActive, 1) : null;
        return flag != null ? flag[0] == 1 : null;
    }
}
