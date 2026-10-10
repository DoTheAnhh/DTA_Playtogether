using DTA.Game.Data;
using DTA.Game.Session;
using DTA.Runtime.Core;

namespace DTA.Game.Ui;

/// <summary>Bảng đang mở: địa chỉ + tên class (kèm class cha).</summary>
public readonly record struct OpenDialog(long Addr, IReadOnlyList<string> Names)
{
    public static readonly OpenDialog None = new(0, []);
    public bool Is(string className) => Names.Contains(className);
    public string Name => Names.Count > 0 ? Names[0] : "";
}

/// <summary>Bảng nào đang mở trên cùng, bảng kết quả cá / đồ và chữ trên bảng - dùng chung theo phiên.</summary>
public sealed class DialogReader(GameSession session)
{
    public const string Dialog = "DialogUnit", Repair = "DialogItemRepair", Message = "DialogBoxMessage", Reward = "DialogRewardPopup",
        ItemView = "DialogResultGetItemView", ItemList = "DialogResultGetItemList", Question = "DialogBoxQuestion", Talk = "DialogTalkBox",
        Result = "DialogFishingGetItem", MainMenu = "DialogMainMenu";
    private static readonly string[] Ignored = ["DialogGameCount", "ButtonGameMenuPopup"];
    private static readonly string[] Unescaped = [ItemView, ItemList, Reward];
    private static readonly string[][] LabelPairs = [["FishNameLabel", "FishPriceLabel"], ["ItemNameLabel", "ItemPriceLabel"]];

    private (long Self, int[] Labels, int Item)? _result;
    private bool _resultUnsupported;
    private (int Panel, int Text, int Grade)? _labelLayout;

    /// <summary>
    /// Bảng trên cùng: (1) popup thưởng toàn cục sysDialog.globalRewardPopup, (2) cuối sysEscape._escapeList (thứ nút Back sẽ đóng),
    /// (3) bảng không dùng Escape đang hiện trong sysDialog._instanceDialogs (màn nhận đồ, danh sách kết quả, popup thưởng).
    /// </summary>
    public OpenDialog Top() => session.Optional<OpenDialog?>(() =>
    {
        if (RewardPopup() is var reward and not 0) return new OpenDialog(reward, session.Managed.Names(reward));
        var escape = session.System("sysEscape");
        var items = escape != 0 ? session.Managed.ListItems(session.Managed.Ptr(escape, "_escapeList")) : [];
        items.Reverse();
        var heads = session.Memory.ReadObjects(items, 8);
        foreach (var item in items)
        {
            var names = heads.TryGetValue(item, out var h) ? session.Il2Cpp.Names(Bin.U64(h, 0)) : [];
            if (names.Contains(Dialog) && !Ignored.Contains(names[0])) return new OpenDialog(item, names);
        }
        var dialogs = session.System("sysDialog");
        var all = session.Managed.DictItems(session.Managed.Ptr(dialogs, "_instanceDialogs")).Select(p => p.Value).Where(Bin.IsPtr).ToList();
        foreach (var (addr, raw) in session.Memory.ReadObjects(all, 0xD0))
        {
            var names = session.Il2Cpp.Names(Bin.U64(raw, 0));
            if (names.Count > 0 && Unescaped.Contains(names[0]) && Visible(addr, raw)) return new OpenDialog(addr, names);
        }
        return null;
    }, "bảng trên cùng") ?? OpenDialog.None;

    /// <summary>Popup thưởng đang hiện (m_IsVisible và không m_IsHIde); 0 nếu không.</summary>
    public long RewardPopup() => session.Optional(() =>
    {
        var popup = session.Managed.Ptr(session.System("sysDialog"), "globalRewardPopup");
        var raw = Bin.IsPtr(popup) ? session.Memory.Read(popup, 0xD0) : null;
        return raw != null && Visible(popup, raw) ? popup : 0;
    }, "popup thưởng");

    /// <summary>Bảng kết quả đang mở (DialogFishingGetItem.Self); 0 nếu không mở / bản game không có.</summary>
    public long ResultDialog()
    {
        if (_resultUnsupported) return 0;
        _result ??= session.Optional<(long, int[], int)?>(() =>
        {
            var klass = session.Class(Result);
            if (klass == 0) return null;
            var labels = LabelPairs.Where(pair => pair.All(f => session.Il2Cpp.HasField(klass, f))).SelectMany(p => p).Select(f => session.Il2Cpp.Field(klass, f)).ToArray();
            return labels.Length > 0 ? (session.Il2Cpp.StaticFieldPtr(klass, "Self"), labels, session.Il2Cpp.Field(klass, "_item")) : null;
        }, "bảng kết quả");
        if (_result == null) _resultUnsupported = true;
        return _result is { Self: not 0 } r ? (long)session.Memory.U64(r.Self) : 0;
    }

    /// <summary>
    /// Món trong bảng kết quả đang mở: tên, giá, cấp nền, là cá không. Nhãn UILabel: chữ ở mText, panel ≠ 0 khi đang hiện; cặp nhãn
    /// đầu là kiểu "cá". Null nếu bảng chưa điền chữ.
    /// </summary>
    public CatchInfo? Catch() => session.Optional(() =>
    {
        var dialog = ResultDialog();
        if (dialog == 0 || _result is not var (_, labelsAt, itemAt)) return null;
        var head = session.Memory.Read(dialog, labelsAt.Append(itemAt).Max() + 8);
        if (head == null) return null;
        var labels = labelsAt.Select(o => Bin.U64(head, o)).ToArray();
        var item = Bin.U64(head, itemAt);
        if (item == 0 || labels.All(l => l == 0)) return null;
        _labelLayout ??= (session.Il2Cpp.Field(session.Managed.ClassOf(labels.First(l => l != 0)), "panel"),
            session.Il2Cpp.Field(session.Managed.ClassOf(labels.First(l => l != 0)), "mText"), session.Il2Cpp.Field(session.Managed.ClassOf(item), "Grade"));
        var (panel, text, grade) = _labelLayout.Value;
        int low = Math.Min(panel, text), high = Math.Max(panel, text);
        var parts = session.Memory.ReadMany(labels.Select(l => (l + low, high - low + 8)).Append((item + grade, 4)).ToList());
        if (parts[^1] is not { } gradeRaw) return null;
        var pointers = parts[..^1].Select(p => p != null && Bin.U64(p, panel - low) != 0 ? Bin.U64(p, text - low) : 0).ToArray();
        var texts = session.Memory.Strings(pointers.Where(p => p != 0));
        for (var i = 0; i + 1 < pointers.Length; i += 2)
            if (texts.GetValueOrDefault(pointers[i]) is { Length: > 0 } name)
                return new CatchInfo(name, texts.GetValueOrDefault(pointers[i + 1], ""), Bin.I32(gradeRaw, 0), i == 0);
        return null;
    }, "món vừa nhận");

    /// <summary>ID món đang trình ra ở màn mở hộp (currentItemID); 0 nếu chưa có.</summary>
    public int OpenedItem(long dialog) => session.Optional(() =>
        session.Il2Cpp.HasField(session.Managed.ClassOf(dialog), "currentItemID") ? session.Managed.I32(dialog, "currentItemID") ?? 0 : 0, "món đang mở");

    /// <summary>Chữ đang hiện trên nhãn <paramref name="field"/> của bảng; rỗng nếu không đọc được.</summary>
    public string Text(long dialog, string field) => session.Optional(() =>
    {
        var text = session.Managed.Ptr(session.Managed.Ptr(dialog, field), "mText");
        return text != 0 ? session.Memory.Strings([text]).GetValueOrDefault(text, "") : "";
    }, $"chữ {field}") ?? "";

    /// <summary>Bảng đang hiện: còn sống phía Unity, m_IsVisible, không m_IsHIde, chưa bị hệ thống xoá.</summary>
    private bool Visible(long addr, byte[] raw)
    {
        var klass = Bin.U64(raw, 0);
        int F(string name) => session.Il2Cpp.Field(klass, name);
        return Bin.U64(raw, F("m_CachedPtr")) != 0 && raw[F("m_IsVisible")] != 0 && raw[F("m_IsHIde")] == 0 && raw[F("IsDeleteFromSystem")] == 0;
    }
}
