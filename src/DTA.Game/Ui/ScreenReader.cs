using DTA.Game.Session;
using DTA.Runtime.Core;

namespace DTA.Game.Ui;

/// <summary>
/// Đọc giao diện game: các "bảng" DialogSystem đang giữ, 2 nút hành động + cần điều khiển trên màn chơi, vị trí nút trong bảng.
/// Dùng chung theo phiên.
/// </summary>
public sealed class ScreenReader(GameSession session)
{
    private static readonly Logger L = Log.For("ui");
    private Dictionary<string, long> _hud = [];

    /// <summary>Cần điều khiển trên màn chơi (tìm cùng lúc với HUD).</summary>
    public long Stick { get; private set; }

    /// <summary>Các bảng game đang giữ sẵn: {tên class: object}.</summary>
    public Dictionary<string, long> Screens()
    {
        var system = session.System("sysDialog");
        if (system == 0) return [];
        var memory = session.Memory;
        var dict = (long)memory.U64(system + session.Il2Cpp.Field(session.Managed.ClassOf(system), "_instanceDialogs"));
        var heads = memory.ReadObjects(session.Managed.DictItems(dict).Select(p => p.Value), 8);
        var classes = heads.ToDictionary(p => p.Key, p => Bin.U64(p.Value, 0));
        session.Il2Cpp.LoadClasses(classes.Values);
        var result = new Dictionary<string, long>();
        foreach (var (addr, klass) in classes) result[session.Il2Cpp.FullName(klass)] = addr;
        return result;
    }

    /// <summary>
    /// Hình 2 nút hành động: "cast" = nút dùng món đang cầm (DialogActionButtons, buttonType 0), "reel" = nút to bên phải
    /// (DialogJoyStick.jumpButton). Nhớ lại sau lần đọc đầu thành công.
    /// </summary>
    public IReadOnlyDictionary<string, long> Hud()
    {
        if (_hud.Count > 0) return _hud;
        var screens = Screens();
        var hud = new Dictionary<string, long>();
        var joystick = screens.GetValueOrDefault("DialogJoyStick");
        var jump = Ptr(joystick, "jumpButton");
        if (jump != 0) hud["reel"] = Ptr(jump, "spriteButtonIcon");
        Stick = Ptr(joystick, "moveJoystickNGUI");
        var buttons = Ptr(screens.GetValueOrDefault("DialogActionButtons"), "actionButtons");
        foreach (var info in session.Managed.ArrayItems(buttons).Take(16))
        {
            if (session.Managed.I32(info, "buttonType") != 0) continue;
            var klass = session.Managed.ClassOf(info);
            var button = session.Il2Cpp.HasField(klass, "button") ? Ptr(info, "button") : 0;
            hud["cast"] = button != 0 ? button : Ptr(info, "spriteIcon");
            break;
        }
        _hud = hud.Where(p => p.Value != 0).ToDictionary();
        if (_hud.Count > 0) L.Debug($"HUD: {string.Join(", ", _hud.Keys)}");
        return _hud;
    }

    /// <summary>Quên HUD đã nhớ (đổi cảnh).</summary>
    public void ResetHud() => _hud = [];

    /// <summary>
    /// Điểm nút "cast" (dùng món đang cầm) / "reel" (nút to bên phải). Game dựng lại màn chơi khi chuyển cảnh: không thấy thì tìm lại 1 lần.
    /// Nút đang chạy hiệu ứng thì lệch - nơi dùng chỉ tin khi 2 lần đọc liền trùng nhau.
    /// </summary>
    public ScreenPoint? HudButton(string name) => session.Optional<ScreenPoint?>(() =>
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var target = Hud().GetValueOrDefault(name);
            var point = target != 0 ? session.Widgets.Point(target) ?? session.World.ComponentPoint(target) : null;
            if (point != null) return point;
            ResetHud();
        }
        return null;
    }, $"nút {name}");

    /// <summary>Cần điều khiển: (tâm x, y, tầm kéo hết tốc lực) điểm ảnh; bán kính theo đơn vị camera giao diện (nửa chiều cao màn hình = 1).</summary>
    public (int X, int Y, int Reach)? Joystick() => session.Optional<(int, int, int)?>(() =>
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            Hud();
            if (Stick != 0 && session.World.ComponentPoint(Stick) is { } p)
            {
                var radius = session.Managed.F32(Stick, "radius") ?? 0;
                return (p.X, p.Y, (int)((radius is >= 0.05f and <= 0.5f ? radius : 0.25f) * session.Screen.Height / 2));
            }
            ResetHud();
        }
        return null;
    }, "cần điều khiển");

    /// <summary>
    /// Điểm màn hình của 1 thành phần giao diện bất kỳ: widget = tâm nó; nút = tâm hình nền (mWidget), không nền thì vị trí GameObject;
    /// object thường = thành phần to nhất trong các field của nó (nền của nút).
    /// </summary>
    public ScreenPoint? Point(long obj, bool nested = true)
    {
        var names = session.Managed.Names(obj);
        if (names.Contains(UiClasses.Widget)) return session.Widgets.Point(obj);
        if (names.Contains(UiClasses.Button))
        {
            var widget = Ptr(obj, "mWidget");
            return widget != 0 ? session.Widgets.Point(widget) : session.World.ComponentPoint(obj);
        }
        if (names.Count == 0 || !nested) return null;
        if (names.Contains(UiClasses.Component)) return session.World.ComponentPoint(obj);
        return Children(obj).Select(c => Point(c, false)).Where(p => p != null).MaxBy(p => p!.Value.Area);
    }

    /// <summary>Object con (con trỏ) trong mọi field của <paramref name="obj"/>, đã nạp class.</summary>
    public List<long> Children(long obj)
    {
        var memory = session.Memory;
        var offsets = session.Il2Cpp.AllFields(session.Managed.ClassOf(obj)).Values.Where(o => o >= 0x10).Distinct().Order().ToList();
        var data = offsets.Count > 0 ? memory.Read(obj, offsets[^1] + 8) : null;
        if (data == null) return [];
        var children = offsets.Where(o => o + 8 <= data.Length).Select(o => Bin.U64(data, o)).Where(c => c > 0x10000 && c % 8 == 0 && c != obj).Distinct();
        var heads = memory.ReadObjects(children, 8);
        session.Il2Cpp.LoadClasses(heads.Values.Select(h => Bin.U64(h, 0)));
        return [.. heads.Keys];
    }

    /// <summary>Con trỏ tại field của object; 0 nếu object rỗng.</summary>
    public long Ptr(long obj, string field) => session.Managed.Ptr(obj, field);
}
