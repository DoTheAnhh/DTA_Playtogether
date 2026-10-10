using DTA.Game.Actions;
using DTA.Game.Ui;

namespace DTA.Engine.Bots;

/// <summary>Phần sửa dụng cụ qua bảng "Thông tin sửa chữa" của <see cref="Bot"/>.</summary>
public abstract partial class Bot
{
    private const int RepairAttempts = 3;

    /// <summary>
    /// Sửa dụng cụ qua bảng sửa game vừa mở: OnClick_Repair -> đồng ý hộp hỏi dùng tiền sao -> chờ độ bền hồi -> OK bảng "Sửa chữa
    /// hoàn thành". Trả chuỗi lỗi nếu không sửa được.
    /// </summary>
    protected string? Repair(long dialog)
    {
        var price = Session.Open.Text(dialog, "labelCurrencyCount");
        Events.Status($"{char.ToUpper(ToolName[0])}{ToolName[1..]} hỏng, đang sửa{(price.Length > 0 ? $" (giá {price})" : "")}...", Level.Warn);
        (int, int)? tool = null;
        for (var attempt = 0; attempt < RepairAttempts && Running; attempt++)
        {
            if (Session.Open.Top().Addr == 0) break;
            Session.Dialogs.Handle(dialog, DialogAction.Repair);
            var deadline = DateTime.UtcNow.AddSeconds(4);
            while (Running && DateTime.UtcNow < deadline)
            {
                AnswerRepairPopups(dialog);
                tool = Durability();
                if (tool is { Item1: > 0 })
                {
                    Events.Tool(tool);
                    ConfirmRepair();
                    Events.Status($"Đã sửa xong {ToolName} thành công!", Level.Info);
                    return null;
                }
                Thread.Sleep(50);
            }
        }
        if (!Running) return null;
        return tool == null ? $"Đã bấm sửa nhưng không đọc được độ bền {ToolName} - kiểm tra trong game" : $"Không sửa được {ToolName} (kiểm tra tiền sửa)";
    }

    /// <summary>Hộp hỏi trung gian (tiền sao) -> đồng ý; thông báo / thưởng -> OK.</summary>
    private void AnswerRepairPopups(long repairDialog)
    {
        var top = Session.Open.Top();
        if (top.Addr == 0 || top.Addr == repairDialog) return;
        if (top.Names.Any(n => n.Contains("Question"))) Session.Dialogs.Handle(top.Addr, DialogAction.Ok);
        else if (top.Names.Any(n => n.Contains("Message") || n.Contains("Reward"))) Session.Dialogs.Handle(top.Addr, DialogAction.Ok);
    }

    /// <summary>Đóng dứt điểm các bảng còn lại sau khi sửa (bảng sửa -> đóng, hỏi -> đồng ý, khác -> OK); không hiện gì trong 2 s thì thôi.</summary>
    private void ConfirmRepair()
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        var appearBy = DateTime.UtcNow.AddSeconds(2);
        while (Running && DateTime.UtcNow < deadline)
        {
            var top = Session.Open.Top();
            if (top.Addr == 0)
            {
                if (DateTime.UtcNow > appearBy) return;
                Thread.Sleep(30);
                continue;
            }
            var action = top.Is(DialogReader.Repair) ? DialogAction.Close : DialogAction.Ok;
            Session.Dialogs.Handle(top.Addr, action);
            if (WaitUntil(() => Session.Open.Top().Addr != top.Addr, 1.2) && Session.Open.Top().Addr == 0) return;
        }
    }
}
