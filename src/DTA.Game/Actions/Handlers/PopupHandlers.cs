using DTA.Game.Invoke;

namespace DTA.Game.Actions.Handlers;

/// <summary>
/// Bảng nhận thưởng / "Hoàn tất bán hàng" (DialogRewardPopup). Hàm native trước: nút NGUI của bảng gắn sự kiện qua delegate nên
/// UIButton.OnClick "thành công" mà không làm gì (R012). Mỗi bảng chỉ nhận 1 lần / giây.
/// </summary>
public sealed class RewardHandler() : ClassHandler("DialogRewardPopup")
{
    private static readonly TimeSpan OnceWindow = TimeSpan.FromSeconds(1);
    public static readonly string[] ConfirmFields =
    [
        "button_Yes", "buttonYes", "Button_Yes", "ButtonYes", "btnYes", "buttonOk", "ButtonOk", "button_Ok", "ButtonOK", "btnOk",
        "OpenButton", "buttonOpen", "ButtonOpen", "button_Open", "btnOpen", "buttonConfirm", "ButtonConfirm", "button_Close", "buttonClose",
        "buttonGet", "ButtonGet", "btnGet", "buttonClaim", "ButtonClaim", "btnClaim", "buttonReceive", "ButtonReceive", "btnReceive",
        "buttonReward", "ButtonReward",
    ];
    public static readonly string[] ConfirmKeywords = ["yes", "ok", "get", "claim", "receive", "reward", "open", "confirm"];
    private static readonly string[] NoFields = ["button_No", "buttonNo", "Button_No", "ButtonNo", "btnNo"];

    /// <summary>Nhận thưởng (mặc định) hoặc từ chối (No / Close).</summary>
    public override bool Handle(DialogContext ctx, long dialog, DialogAction action) => action is DialogAction.No
        ? DialogContext.FirstOf(() => ctx.Call(Fn.RewardNo, dialog), () => ctx.ClickAny(dialog, NoFields))
        : Confirm(ctx, dialog, true);

    /// <summary>Nhận thưởng bảng bất kỳ: native (nếu đúng class) -> nút xác nhận -> đóng an toàn.</summary>
    public static bool Confirm(DialogContext ctx, long dialog, bool native)
    {
        if (!ctx.Once("reward", dialog, OnceWindow)) return true;
        return DialogContext.FirstOf(
            () => native && ctx.Call(Fn.RewardYes, dialog),
            () => native && ctx.Call(Fn.RewardFinish, dialog),
            () => native && ctx.Call(Fn.RewardClose, dialog),
            () => ctx.ClickAny(dialog, ConfirmFields, ConfirmKeywords),
            () => ctx.CloseBack(dialog));
    }
}

/// <summary>Hộp hỏi Yes/No (DialogBoxQuestion, ví dụ "Xác nhận bán" cá vương miện): đồng ý trừ khi yêu cầu No / Close.</summary>
public sealed class QuestionHandler() : ClassHandler("DialogBoxQuestion")
{
    private static readonly string[] YesFields = ["buttonYes", "ButtonYes", "btnYes", "buttonConfirm", "ButtonConfirm"];
    private static readonly string[] NoFields = ["buttonNo", "ButtonNo", "btnNo", "buttonCancel", "ButtonCancel"];

    public override bool Handle(DialogContext ctx, long dialog, DialogAction action) => Answer(ctx, dialog, action is not (DialogAction.No or DialogAction.Close));

    /// <summary>Trả lời hộp hỏi: hàm native -> nút -> đóng an toàn.</summary>
    public static bool Answer(DialogContext ctx, long dialog, bool accept) => DialogContext.FirstOf(
        () => ctx.Call(accept ? Fn.QuestionOk : Fn.QuestionCancel, dialog),
        () => ctx.ClickAny(dialog, accept ? YesFields : NoFields),
        () => ctx.CloseBack(dialog));
}

/// <summary>Hộp thông báo nút OK (DialogBoxMessage).</summary>
public sealed class MessageHandler() : ClassHandler("DialogBoxMessage")
{
    private static readonly string[] OkFields = ["ButtonOK", "buttonOk", "ButtonOk", "buttonOK", "btnOk", "button_Ok", "buttonConfirm", "ButtonConfirm"];

    public override bool Handle(DialogContext ctx, long dialog, DialogAction action) => action is DialogAction.Close
        ? ctx.Call(Fn.MessageClose, dialog)
        : Ok(ctx, dialog);

    /// <summary>Bấm OK: OnClick_OK -> OnClick_Close -> nút -> đóng an toàn.</summary>
    public static bool Ok(DialogContext ctx, long dialog) => DialogContext.FirstOf(
        () => ctx.Call(Fn.MessageOk, dialog),
        () => ctx.Call(Fn.MessageClose, dialog),
        () => ctx.ClickAny(dialog, OkFields),
        () => ctx.CloseBack(dialog));
}

/// <summary>Bảng lên cấp thành tích (DialogAchievementLvUp): đóng.</summary>
public sealed class AchievementHandler() : ClassHandler("DialogAchievementLvUp")
{
    private static readonly string[] CloseFields = ["buttonClose", "ButtonClose", "btnClose"];

    public override bool Handle(DialogContext ctx, long dialog, DialogAction action) => DialogContext.FirstOf(
        () => ctx.Call(Fn.AchievementClose, dialog),
        () => ctx.ClickAny(dialog, CloseFields),
        () => ctx.CloseBack(dialog));
}

/// <summary>Bảng thông tin sự kiện (DialogEventBox): đóng an toàn, không bấm gì trong đó.</summary>
public sealed class EventBoxHandler() : ClassHandler("DialogEventBox")
{
    public override bool Handle(DialogContext ctx, long dialog, DialogAction action) => ctx.CloseBack(dialog);
}

/// <summary>Cửa hàng trong game (DialogShopInGame): mua 1 khi yêu cầu Buy, còn lại đóng.</summary>
public sealed class ShopHandler() : ClassHandler("DialogShopInGame")
{
    public override bool Handle(DialogContext ctx, long dialog, DialogAction action) =>
        ctx.Call(action is DialogAction.Buy ? Fn.ShopBuy1 : Fn.ShopClose, dialog);
}

/// <summary>Bảng sửa dụng cụ (DialogItemRepair). RepairButton là PurchaseButton - bấm UIButton không làm gì, nên nút chỉ là dự phòng.</summary>
public sealed class RepairHandler() : ClassHandler("DialogItemRepair")
{
    private static readonly string[] RepairFields = ["RepairButton", "btnRepair", "buttonRepair", "ButtonRepair"];
    private static readonly string[] CloseFields = ["CloseButton", "btnClose"];

    public override bool Handle(DialogContext ctx, long dialog, DialogAction action) => action is DialogAction.Repair or DialogAction.Ok
        ? DialogContext.FirstOf(() => ctx.Call(Fn.RepairRepair, dialog), () => ctx.ClickAny(dialog, RepairFields))
        : DialogContext.FirstOf(() => ctx.Call(Fn.RepairClose, dialog), () => ctx.ClickAny(dialog, CloseFields));
}
