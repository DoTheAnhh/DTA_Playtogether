using DTA.Game.Ui;

namespace DTA.Game.Actions.Handlers;

/// <summary>
/// Bảng không có bộ riêng (popup sự kiện, quà...): đoán loại theo tên class (reward / box / message / question), không đoán được thì
/// bấm nút theo tên hành động; chỉ đóng khi được yêu cầu đóng rõ ràng.
/// </summary>
public sealed class FallbackHandler : IDialogHandler
{
    private static readonly (string[] Words, Func<DialogContext, long, bool> Run)[] Guesses =
    [
        (["reward", "gift", "gain", "claim"], (c, d) => RewardHandler.Confirm(c, d, false)),
        (["box", "package", "chest", "open"], (c, d) => OpenBox.Open(c, d)),
        (["notice", "message", "toast", "alert"], MessageHandler.Ok),
        (["question", "confirm", "ask"], (c, d) => QuestionHandler.Answer(c, d, true)),
    ];

    private static readonly Dictionary<DialogAction, string[]> Keywords = new()
    {
        [DialogAction.Ok] = ["buttonok", "btnok", "button_ok", "btn_ok", "buttonconfirm", "button_yes", "btnyes"],
        [DialogAction.Open] = ["buttonopen", "btnopen", "openbutton", "membershipboxopenbutton"],
        [DialogAction.Sell] = ["sellbutton", "buttonsell", "membershipfishsellbutton", "membershipboxsellbutton"],
        [DialogAction.Keep] = ["okbutton", "buttonclose", "membershipfishokbutton"],
    };

    public bool Matches(IReadOnlyList<string> names) => true;

    public bool Handle(DialogContext ctx, long dialog, DialogAction action)
    {
        if (action is DialogAction.Close) return ctx.CloseBack(dialog);
        foreach (var name in ctx.Names(dialog))
            foreach (var (words, run) in Guesses)
                if (words.Any(w => name.Contains(w, StringComparison.OrdinalIgnoreCase))) return run(ctx, dialog);
        var key = action.ToString().ToLowerInvariant();
        var fields = DialogButtons.Fields.GetValueOrDefault(key) ?? [];
        return ctx.ClickAny(dialog, fields, Keywords.GetValueOrDefault(action));
    }
}
