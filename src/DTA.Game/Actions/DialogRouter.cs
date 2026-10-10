using DTA.Game.Actions.Handlers;
using DTA.Game.Session;
using DTA.Runtime.Core;

namespace DTA.Game.Actions;

/// <summary>
/// Điểm vào duy nhất xử lý mọi bảng game: chọn bộ xử lý theo class bảng (bộ riêng trước, đoán chung sau cùng) rồi gọi hàm game.
/// Không chạm màn hình. Dùng chung theo phiên.
/// </summary>
public sealed class DialogRouter(GameSession session)
{
    private static readonly Logger L = Log.For("dialog");
    private static readonly IDialogHandler[] Handlers =
    [
        new RewardHandler(), new QuestionHandler(), new MessageHandler(), new ItemListHandler(), new ItemViewHandler(),
        new AchievementHandler(), new EventBoxHandler(), new FishResultHandler(), new RepairHandler(), new ShopHandler(), new FallbackHandler(),
    ];

    /// <summary>
    /// Bảng CHUNG luồng nền được tự xử lý khi không bot nào chạy (không phụ thuộc menu): nhận thưởng, OK, xác nhận, màn nhận đồ,
    /// danh sách đồ, lên cấp, sự kiện. Chỉ nhận đúng tên - đoán theo từ khoá có thể trúng màn chờ tải / thông báo sự kiện.
    /// </summary>
    public static readonly string[] CommonClasses =
    [
        "DialogRewardPopup", "DialogBoxMessageNgui", "DialogBoxMessage", "DialogBoxQuestionNgui", "DialogBoxQuestion", "DialogBoxQuestionIconBtn",
        "DialogResultGetItemView", "DialogResultGetItemList", "DialogAchievementLvUp", "DialogEventBox",
    ];

    /// <summary>
    /// Xử lý bảng chung (xem <see cref="CommonClasses"/>); bảng kết quả cá / đồ chỉ mở khi là hộp (bán / giữ là việc của menu);
    /// sửa đồ, cửa hàng, bảng lạ: không tự bấm.
    /// </summary>
    public bool HandleCommon(long dialog, IReadOnlyList<string> names)
    {
        if (names.Contains("DialogFishingGetItem")) return FishResultHandler.IsBox(Context, dialog) && Handle(dialog, DialogAction.Open);
        return names.Any(CommonClasses.Contains) && Handle(dialog, DialogAction.Ok);
    }

    /// <summary>
    /// Bảng lỡ mở khi chạm trúng người chơi khác (thẻ thông tin người chơi): chặn mọi bot nhưng không phải popup - chỉ bot ĐANG CHẠY mới
    /// đóng (người dùng tự mở xem thì để yên).
    /// </summary>
    public static readonly string[] Obstructions = ["DialogPlayerSimpleInfo", "DialogPlayerDetailInfo"];

    /// <summary>Đóng bảng chắn đường (xem <see cref="Obstructions"/>) bằng hàm đóng của game; false nếu không phải loại đó.</summary>
    public bool CloseObstruction(long dialog, IReadOnlyList<string> names) => names.Any(Obstructions.Contains) && Handle(dialog, DialogAction.Close);

    public DialogContext Context { get; } = new(session);

    /// <summary>Làm <paramref name="action"/> với bảng <paramref name="dialog"/>; true = đã xử lý (hoặc vừa xử lý, đang chờ game).</summary>
    public bool Handle(long dialog, DialogAction action = DialogAction.Ok)
    {
        if (!Bin.IsPtr(dialog)) return false;
        var names = Context.Names(dialog);
        var handler = Handlers.First(h => h.Matches(names));
        Context.SkipCloseAnimation(dialog);
        var done = handler.Handle(Context, dialog, action);
        Context.FlushPending();
        L.Debug($"{(names.Count > 0 ? names[0] : "?")} {action} -> {(done ? "xong" : "chưa")} ({handler.GetType().Name})");
        return done;
    }
}
