using DTA.Game.Session;

namespace DTA.Game.Ui;

/// <summary>Tìm vị trí nút trong 1 bảng đang mở (OK, đóng, bán, giữ...) - dùng chung cho mọi bộ xử lý bảng.</summary>
public sealed class DialogButtons(GameSession session)
{
    /// <summary>Nút cần bấm = các field giữ nút đó (thử lần lượt, lấy cái đầu đang hiện).</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Fields = new Dictionary<string, string[]>
    {
        ["repair"] = ["RepairButton", "labelCurrencyCount", "btnRepair"],
        ["ok"] = ["ButtonOK", "buttonOk", "button_Ok", "button_OK", "buttonConfirm", "ButtonConfirm", "btnOk", "btn_ok", "btnConfirm", "Button_OK", "Button_Ok", "OKButton", "okButton", "button_confirm", "MembershipFishOKButton"],
        ["exit"] = ["buttonExit", "buttonClose", "ButtonClose", "btnClose", "button_Close"],
        ["skip"] = ["buttonSkip", "ButtonSkip", "btnSkip"],
        ["confirm"] = ["buttonYes", "ButtonYes", "btnYes", "buttonConfirm", "ButtonConfirm"],
        ["yes"] = ["button_Yes", "buttonYes", "ButtonYes", "buttonOk", "ButtonOK"],
        ["no"] = ["button_No", "buttonNo", "ButtonNo", "buttonCancel", "ButtonCancel"],
        ["keep"] = ["MembershipFishOKButton", "OKButton", "MembershipBoxOpenButton", "OpenButton", "buttonClose", "ButtonClose"],
        ["sell"] = ["MembershipFishSellButton", "MembershipBoxSellButton", "buttonSell", "ButtonSell"],
        ["open"] = ["MembershipBoxOpenButton", "OpenButton", "buttonOpen", "ButtonOpen"],
    };

    private static readonly string[] OkHints = ["ok", "confirm"];

    /// <summary>
    /// Vị trí nút <paramref name="name"/> (khoá của <see cref="Fields"/>) trong bảng <paramref name="dialog"/>; null nếu đang ẩn / không đọc được.
    /// Bố cục native chưa kiểm được (tool bật lúc bảng che màn chơi) thì lấy hình / chữ của chính bảng làm vật đối chiếu.
    /// </summary>
    public ScreenPoint? Find(long dialog, string name) => session.Optional<ScreenPoint?>(() =>
    {
        var klass = session.Managed.ClassOf(dialog);
        if (klass == 0) return null;
        if (!session.World.LayoutKnown) UseAsReference(dialog);
        var il2cpp = session.Il2Cpp;
        foreach (var field in Fields[name].Where(f => il2cpp.HasField(klass, f)))
            if (PointAt(dialog, il2cpp.Field(klass, field)) is { } p) return p;
        if (name != "ok") return null;
        foreach (var (field, offset) in il2cpp.AllFields(klass))
            if (OkHints.Any(h => field.Contains(h, StringComparison.OrdinalIgnoreCase)) && PointAt(dialog, offset) is { } p) return p;
        return null;
    }, $"nút {name}");

    /// <summary>Nút đóng (X) của bảng bất kỳ: field tên chứa "close"/"exit" (trừ callback) đang hiện.</summary>
    public ScreenPoint? Close(long dialog) => session.Optional<ScreenPoint?>(() =>
    {
        var fields = session.Il2Cpp.AllFields(session.Managed.ClassOf(dialog));
        foreach (var (field, offset) in fields.OrderBy(p => p.Value))
        {
            var lower = field.ToLowerInvariant();
            if (offset < 0x10 || !(lower.Contains("close") || lower.Contains("exit")) || lower.Contains("cb") || lower.Contains("callback")) continue;
            var target = (long)session.Memory.U64(dialog + offset);
            var names = session.Managed.Names(target);
            if ((names.Contains(UiClasses.Button) || names.Contains(UiClasses.GameObject)) && session.Ui.Point(target) is { } p) return p;
        }
        return null;
    }, "nút đóng");

    /// <summary>Vị trí nút <paramref name="field"/> trên bảng màn chơi <paramref name="screen"/> (ví dụ nút mở túi đồ).</summary>
    public ScreenPoint? OnScreen(string screen, string field) => session.Optional<ScreenPoint?>(() =>
    {
        var dialog = session.Ui.Screens().GetValueOrDefault(screen);
        var klass = session.Managed.ClassOf(dialog);
        var button = klass != 0 && session.Il2Cpp.HasField(klass, field) ? session.Managed.Ptr(dialog, field) : 0;
        return button != 0 ? session.Ui.Point(button) : null;
    }, $"nút {screen}.{field}");

    /// <summary>Điểm của object ở field offset trong bảng.</summary>
    private ScreenPoint? PointAt(long dialog, int offset)
    {
        var target = (long)session.Memory.U64(dialog + offset);
        return target != 0 ? session.Ui.Point(target) : null;
    }

    /// <summary>Lấy hình / chữ trong field của bảng (hoặc hình nền nút) làm vật đối chiếu bố cục native.</summary>
    private void UseAsReference(long dialog)
    {
        var widgets = new List<long>();
        foreach (var child in session.Ui.Children(dialog))
        {
            var names = session.Managed.Names(child);
            var widget = names.Contains(UiClasses.Widget) ? child : names.Contains(UiClasses.Button) ? session.Managed.Ptr(child, "mWidget") : 0;
            if (widget != 0) widgets.Add(widget);
        }
        session.World.ReferenceWidgets = widgets;
        session.World.Layout();
    }
}
