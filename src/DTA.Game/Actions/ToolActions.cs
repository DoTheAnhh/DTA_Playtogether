using DTA.Game.Invoke;
using DTA.Game.Session;
using DTA.Runtime.Core;

namespace DTA.Game.Actions;

/// <summary>
/// Thao tác dụng cụ cầm tay qua hàm game (không bấm màn hình): ưu tiên hàm của object điều khiển nhân vật, chỉ khi chưa có nó mới
/// dùng controller của dụng cụ. Hàm chính hết giờ thì KHÔNG gọi chồng hàm dự phòng (game có thể vẫn đang chạy lệnh trước -> văng).
/// </summary>
public sealed class ToolActions(GameSession session)
{
    private static readonly Logger L = Log.For("action");

    /// <summary>Quăng / giật / thu cần câu (cùng 1 nút game).</summary>
    public bool Fish() => Use(Fn.ActorFishing, Fn.FishingPole, fallbackAfterFail: true);

    /// <summary>Vung xẻng đào.</summary>
    public bool Dig() => Use(Fn.ActorExcavate, Fn.Shovel);

    /// <summary>Vung cuốc đập đá.</summary>
    public bool SwingPickaxe() => Use(Fn.ActorPickaxe, Fn.Pickaxe);

    /// <summary>Vung vợt bắt bọ.</summary>
    public bool SwingNet() => Use(Fn.ActorInsect, Fn.InsectNet);

    /// <summary>Đè giữ nút vợt (OnPressInsectNet(true)): nhân vật giơ vợt đi rón rén - bọ khó phát hiện. Thả = <see cref="ReleaseNet"/>.</summary>
    public bool HoldNet() => session.Control != 0 && session.Invoker.Call(Fn.ActorPressNet, session.Control, [1]);

    /// <summary>Thả nút vợt đang giữ (OnPressInsectNet(false)) - game vung vợt.</summary>
    public bool ReleaseNet() => session.Control != 0 && session.Invoker.Call(Fn.ActorPressNet, session.Control, [0]);

    /// <summary>Game có đang giữ vợt thật không (control._holdInsectNet); null nếu không đọc được.</summary>
    public bool? NetHeld() => session.Control == 0 ? null : session.Optional(() =>
        session.Memory.Read(session.Control + session.Il2Cpp.Field(session.ControlClass, "_holdInsectNet"), 1) is [var b] ? b != 0 : (bool?)null, "_holdInsectNet");

    /// <summary>Hạ vợt đang giữ mà không vung (OnHoldInsectNet(false)) - bỏ con đang rình.</summary>
    public bool LowerNet() => session.Control != 0 && session.Invoker.Call(Fn.ActorHoldNet, session.Control, [0]);

    /// <summary>Nhảy: DialogJoyStick.Self.OnPress_JumpButton.</summary>
    public bool Jump()
    {
        var joystick = session.Optional(() =>
        {
            var klass = session.Class("DialogJoyStick");
            return klass != 0 ? (long)session.Memory.U64(session.Il2Cpp.StaticFieldPtr(klass, "Self")) : 0;
        }, "DialogJoyStick.Self");
        return joystick != 0 && session.Invoker.Call(Fn.Jump, joystick);
    }

    /// <summary>
    /// Bấm bong bóng trên vật (nút nhận quà cổ vật / rương, bong bóng nhặt) đúng như chạm tay: hàm bấm của class bong bóng. UIButton bên
    /// trong gắn sự kiện qua delegate nên UIButton.OnClick không tác dụng (R012).
    /// </summary>
    public bool PressHeadButton(long head)
    {
        if (!Bin.IsPtr(head)) return false;
        var klass = session.Managed.ClassOf(head);
        if (session.Il2Cpp.Is(klass, Fn.HeadUpSelect.Owner)) return session.Invoker.Call(Fn.HeadUpSelect, head);
        if (session.Il2Cpp.Is(klass, Fn.HeadUpBoxOpen.Owner)) return session.Invoker.Call(Fn.HeadUpBoxOpen, head);
        return session.Invoker.Call(Fn.UiButtonClick, head);
    }

    /// <summary>Nhặt vật trên sân: bấm bong bóng bàn tay của nó; chưa có bong bóng thì OnPickFieldObject(uid).</summary>
    public bool Pick(int uid, long thing = 0)
    {
        var button = thing != 0 ? session.HeadUps.PickButton(thing) : 0;
        if (button != 0) return session.Invoker.Call(Fn.HeadUpSelect, button);
        return session.Invoker.CallActor(Fn.ActorPickFieldObject, (ulong)(uint)uid);
    }

    /// <summary>Controller dụng cụ đang cầm (control._equipHandItem); 0 nếu không có.</summary>
    public long HeldTool() => session.Optional(() =>
        session.Control != 0 && session.ControlClass != 0 ? (long)session.Memory.U64(session.Control + session.Il2Cpp.Field(session.ControlClass, "_equipHandItem")) : 0,
        "_equipHandItem");

    /// <summary>
    /// Hàm nhân vật nếu có object điều khiển; không thì OnClick_Button(0) của dụng cụ (Invoker kiểm đúng class).
    /// <paramref name="fallbackAfterFail"/> = cho phép thử dụng cụ khi hàm nhân vật thất bại (câu cá: 2 hàm cùng 1 nút, an toàn).
    /// </summary>
    private bool Use(GameFunction actorFunction, GameFunction toolFunction, bool fallbackAfterFail = false)
    {
        if (session.Control != 0)
        {
            if (session.Invoker.Call(actorFunction, session.Control)) return true;
            if (!fallbackAfterFail) return false;
        }
        var tool = HeldTool();
        if (tool == 0) L.Debug($"{toolFunction.Owner}: không cầm dụng cụ");
        return tool != 0 && session.Invoker.Call(toolFunction, tool, [0]);
    }
}
