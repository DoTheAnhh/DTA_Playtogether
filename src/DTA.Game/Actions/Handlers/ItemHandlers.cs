using DTA.Game.Invoke;
using DTA.Runtime.Core;

namespace DTA.Game.Actions.Handlers;

/// <summary>Bảng "KẾT QUẢ" liệt kê đồ vừa nhận (DialogResultGetItemList): OK native trước (buttonOk gắn qua delegate).</summary>
public sealed class ItemListHandler() : ClassHandler("DialogResultGetItemList")
{
    private static readonly string[] OkFields = ["buttonOk", "ButtonOk", "btnOk", "ButtonOK", "button_Ok", "btnConfirm", "ButtonConfirm"];

    public override bool Handle(DialogContext ctx, long dialog, DialogAction action)
    {
        if (!ctx.Once("itemlist", dialog, ItemViewHandler.OnceWindow)) return true;
        return DialogContext.FirstOf(() => ctx.Call(Fn.ItemListOk, dialog), () => ctx.ClickAny(dialog, OkFields), () => ctx.CloseBack(dialog));
    }
}

/// <summary>
/// Màn nhận đồ "Nhấn vào màn hình để chuyển sang phần tiếp theo" (DialogResultGetItemView) - mọi độ hiếm, mọi loại hộp / gói thẻ.
/// Giữ nguyên hình và hoạt ảnh của game, chỉ rút ngắn: hộp gacha -> OnPress_GachaBoxOpen; gói thẻ chờ vuốt -> SetJoystickSwipe
/// 1 lần; đồ ẩn chờ lật -> OnClick_ButtonRevealItem 1 lần; rồi ĐÚNG 1 LẦN OnClick_ButtonSkip khi runBT đã chạy (gọi sớm / gọi chồng
/// lúc bảng đang đóng là game ném lỗi -> văng). False = bảng chưa sẵn sàng, nhịp sau thử lại.
/// </summary>
public sealed class ItemViewHandler() : ClassHandler("DialogResultGetItemView")
{
    public static readonly TimeSpan OnceWindow = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan StepOnce = TimeSpan.FromSeconds(30), StepWait = TimeSpan.FromSeconds(0.6);
    private static readonly string[] Fields = ["runBT", "gachaBoxView", "finishGachaOpen", "_currentGachaAniCtrl", "ButtonOpenRevealItem", "ButtonPressPackDrag"];
    private static readonly Logger L = Log.For("dialog");

    public override bool Handle(DialogContext ctx, long dialog, DialogAction action)
    {
        if (ctx.Locked("itemview", dialog, OnceWindow)) return true;
        var s = ctx.Session;
        var klass = s.Managed.ClassOf(dialog);
        var at = Fields.Select(f => s.Il2Cpp.Field(klass, f)).ToArray();
        var raw = s.Memory.Read(dialog, at.Max() + 8);
        if (raw == null) return false;
        int runBt = at[0], box = at[1], boxDone = at[2], boxCtrl = at[3], reveal = at[4], pack = at[5];
        var view = Bin.U64(raw, box);
        if (view != 0 && raw[boxDone] == 0 && s.World.IsShown(view) == true)
        {
            if (Bin.U64(raw, boxCtrl) == 0) return false;
            if (!ctx.Once("gacha", dialog, StepWait)) return true;
            L.Debug("Mở hộp gacha");
            return ctx.Call(Fn.ItemViewGachaPress, dialog);
        }
        var drag = Bin.U64(raw, pack);
        if (drag != 0 && raw[boxDone] == 0 && s.World.IsShown(drag) == true)
        {
            if (ctx.Once("pack", dialog, StepOnce))
            {
                L.Debug("Vuốt mở gói thẻ");
                return ctx.Call(Fn.ItemViewSwipe, dialog);
            }
            if (ctx.Locked("pack", dialog, StepWait)) return true;
        }
        if (Bin.U64(raw, runBt) == 0) return false;
        var button = Bin.U64(raw, reveal);
        if (button != 0 && s.World.IsShown(button) == true)
        {
            if (ctx.Once("reveal", dialog, StepOnce))
            {
                L.Debug("Lật đồ ẩn");
                return ctx.Call(Fn.ItemViewReveal, dialog);
            }
            if (ctx.Locked("reveal", dialog, StepWait)) return false;
        }
        if (!ctx.Once("itemview", dialog, OnceWindow)) return true;
        return ctx.Call(Fn.ItemViewSkip, dialog);
    }
}

/// <summary>Bảng kết quả cá / đồ đào / bọ (DialogFishingGetItem) - DUY NHẤT bảng theo cấu hình bot: bán / giữ / mở hộp.</summary>
public sealed class FishResultHandler() : ClassHandler("DialogFishingGetItem")
{
    private static readonly TimeSpan SellOnce = TimeSpan.FromSeconds(2);
    /// <summary>Nút Mở chỉ bấm 1 lần / bảng: vật phẩm nhặt được dùng chung giao diện hộp mà nút Mở (gắn delegate) bấm không ăn - lần sau đóng bằng hàm gốc.</summary>
    private static readonly TimeSpan OpenOnce = TimeSpan.FromSeconds(30);
    private static readonly string[] SellFields = ["MembershipFishSellButton", "SellButton", "buttonSell", "ButtonSell", "btnSell"];
    private static readonly string[] KeepFields = ["MembershipFishOKButton", "OKButton", "buttonClose", "ButtonClose", "Button_Close"];

    public override bool Handle(DialogContext ctx, long dialog, DialogAction action) => action switch
    {
        _ when IsBox(ctx, dialog) || action == DialogAction.Open =>
            ctx.Once("open", dialog, OpenOnce) ? OpenBox.Open(ctx, dialog, () => ctx.Call(Fn.FishOpenPackage, dialog)) : Keep(ctx, dialog),
        DialogAction.Sell => Sell(ctx, dialog),
        _ => Keep(ctx, dialog),
    };

    /// <summary>
    /// Món vừa nhận là hộp / lon / gói - luôn mở, không bán / giữ. Phải thấy cả nút OpenButton LẪN khối nút hộp (Membership / NotMembership
    /// BoxButtonRoot) đang hiện: cờ hiện chỉ của riêng nút vẫn bật khi khối cha đã tắt (kết quả cá / tôm / gỗ), bấm nút đó là văng game.
    /// </summary>
    public static bool IsBox(DialogContext ctx, long dialog)
    {
        bool Shown(string field) => ctx.Button(dialog, field) is var obj and not 0 && ctx.Session.World.IsShown(obj) == true;
        return Shown("OpenButton") && (Shown("MembershipBoxButtonRoot") || Shown("NotMembershipBoxButtonRoot"));
    }

    /// <summary>Bán nhanh: OnClick_Selling -> nút bán; mỗi bảng 1 lần / 2 giây (đang chờ server).</summary>
    private static bool Sell(DialogContext ctx, long dialog) =>
        !ctx.Once("sell", dialog, SellOnce) || DialogContext.FirstOf(() => ctx.Call(Fn.FishSell, dialog), () => ctx.ClickAny(dialog, SellFields));

    /// <summary>Bảo quản: các hàm đóng native -> nút OK -> đóng chung.</summary>
    private static bool Keep(DialogContext ctx, long dialog) => DialogContext.FirstOf(
        () => ctx.Call(Fn.FishClose, dialog),
        () => ctx.Call(Fn.FishCloseFromBack, dialog),
        () => ctx.Call(Fn.FishDelete, dialog),
        () => ctx.Call(Fn.FishHide, dialog, 1),
        () => ctx.ClickAny(dialog, KeepFields),
        () => ctx.CloseBack(dialog),
        () => ctx.Call(Fn.DialogDelete, dialog));
}

/// <summary>Mở hộp / lon / quà: nút mở bất kỳ -> hàm native riêng của bảng -> đóng an toàn.</summary>
public static class OpenBox
{
    private static readonly string[] Fields =
    [
        "OpenButton", "buttonOpen", "ButtonOpen", "button_Open", "btnOpen", "MembershipBoxOpenButton", "buttonOk", "ButtonOk", "ButtonOK",
        "button_Ok", "buttonNext", "ButtonNext", "buttonConfirm", "ButtonConfirm",
    ];
    private static readonly string[] Keywords = ["open", "next", "reveal", "box", "package", "ok", "confirm"];

    /// <summary>Mở; <paramref name="native"/> = hàm mở riêng của loại bảng (null nếu không có).</summary>
    public static bool Open(DialogContext ctx, long dialog, Func<bool>? native = null) => DialogContext.FirstOf(
        () => ctx.ClickAny(dialog, Fields, Keywords),
        () => native?.Invoke() == true,
        () => ctx.CloseBack(dialog));
}
