using DTA.Engine.Bots;
using DTA.Engine.Movement;
using DTA.Game.Actions;
using DTA.Runtime.Core;

namespace DTA.Features.Excavation;

/// <summary>Phần Hòn đảo bị mất của <see cref="ExcavationBot"/>.</summary>
public sealed partial class ExcavationBot
{
    private const string StarMoney = "Tiền sao", Shop = "DialogShopInGame";
    private const double ChestWait = 0.3, IslandPoll = 0.03, ClaimTake = 0.2, IslandStall = 5, BuyWait = 1.5, OpenTime = 0.45;
    private const float IslandDig = 0.75f, ChestNear = 1.5f;
    private const int IslandWalks = 3, BuyTries = 2;
    private string _dug = "";

    /// <summary>
    /// Đào 1 điểm trên đảo: gõ xẻng dồn từ nhịp đầu; nút nhận quà hiện = xong chắc chắn (bấm rồi đi); điểm biến mất mà chưa thấy rương =
    /// quà về thẳng túi. Lọc rương: nhát dò ăn mà sau 0,3 s không phải rương thì bỏ ngay; là rương thì đào tới khi có nút nhận quà. Bị đẩy
    /// lệch tâm (> 0,75 m) thì đi lại (tối đa 3 lần); 5 s không tiến triển thì bỏ. Hết xẻng giữa chừng thì mua gói mới.
    /// </summary>
    private bool DigIsland(Spot spot)
    {
        if (!_game.IslandAlive(spot))
        {
            LastAction = 0;
            Events.Status("Điểm này người khác vừa đào - sang điểm khác", Level.Ok);
            return true;
        }
        var uses = Durability()?.Item1;
        var name = Label(spot);
        Source = spot.Kind != 0 ? _game.KindName(spot.Kind) : "";
        _dug = "";
        double hit = 0, changed = Now, polled = 0;
        var chest = false;
        var walks = 0;
        Spot? fresh = spot;
        Tapper.Start();
        try
        {
            while (Running)
            {
                var now = Now;
                if (now - polled >= IslandPoll)
                {
                    polled = now;
                    fresh = _game.Reload(spot);
                    if (Durability() is var (left, _))
                    {
                        if (uses is { } u && left < u) hit = changed = now;
                        uses = left;
                        if (left == 0)
                        {
                            Tapper.Pause();
                            if (!Options.AutoRepair || BuyShovels() != null) return true;
                            uses = Durability()?.Item1;
                            changed = now;
                            if (!Dig.OnlyChest || chest) Tapper.Start();
                        }
                    }
                }
                if (_game.ClaimPoint(fresh ?? spot) != null)
                {
                    Tapper.Pause();
                    return Finish(fresh ?? spot);
                }
                if (Session.Open.Top().Addr != 0) return true;
                if (fresh == null && !chest)
                {
                    _dug = Source.Length > 0 ? Source : StarMoney;
                    LastAction = 0;
                    return true;
                }
                if (fresh is { Kind: not 0 })
                {
                    Source = _game.KindName(fresh.Kind);
                    if ((fresh.Kind, fresh.Hp) != (spot.Kind, spot.Hp))
                    {
                        changed = now;
                        name = Label(fresh);
                        Events.Status($"Đang đào {name}{(fresh.MaxHp > 1 ? $" ({fresh.Hp}/{fresh.MaxHp})" : "")}...", Level.Ok);
                    }
                }
                spot = fresh ?? spot;
                if (Dig.OnlyChest && !chest)
                {
                    var box = _game.DiggingBoxType(spot.Uid);
                    if (_game.IsChest(spot.Kind) || _game.IsChest(box) || ChestHere(spot)) chest = true;
                    else if (box == 1 || (spot.Kind != 0 && !_game.IsChest(spot.Kind))) return NotChest($"{(Source.Length > 0 ? Source : "Đá")} - không phải rương, sang điểm khác");
                    else if (hit != 0 && now - hit >= ChestWait) return NotChest("Không phải rương - sang điểm khác");
                }
                if (!Tapper.Active) Tapper.Start();
                if (Session.Player.Position() is { } here)
                {
                    var distance = NavMap.Dist(spot.X, spot.Z, here.X, here.Z);
                    Gather.Target(distance, name);
                    if (distance > IslandDig)
                    {
                        Tapper.Pause();
                        if (++walks > IslandWalks) return Give(null);
                        var result = MoveCloser(spot, IslandStand);
                        if (result == WalkResult.Lost) throw new GameError("Không đọc được vị trí nhân vật hoặc hướng camera", true);
                        if (result == WalkResult.Stuck) return Give(null);
                        if (result != WalkResult.Arrived) return true;
                        changed = Now;
                        if (!Dig.OnlyChest || chest) Tapper.Start();
                    }
                }
                if (now - changed > IslandStall) return Give("Đào mà không ăn - sang điểm khác...");
                Thread.Sleep(15);
            }
        }
        finally
        {
            Tapper.Pause();
        }
        return true;
    }

    private bool NotChest(string message)
    {
        Tapper.Pause();
        LastAction = 0;
        Events.Status(message, Level.Ok);
        return false;
    }

    /// <summary>Có rương đang trồi lên NGAY tại điểm này (rương người khác gần đó, đá xanh không tính).</summary>
    private bool ChestHere(Spot spot) =>
        _game.ActiveTreasure(spot) is { } act && NavMap.Dist(act.Position.X, act.Position.Z, spot.X, spot.Z) <= ChestNear
        && _game.IsChest(act.Type > 0 ? act.Type : _game.DiggingBoxType(spot.Uid));

    /// <summary>Bấm nhận quà trên đầu vật đảo vừa xong rồi đi luôn: nút biến mất / có bảng = đã nhận; sau 0,2 s còn nút thì bấm lại 1 lần.</summary>
    private bool Finish(Spot spot)
    {
        Gather.Target(null, "");
        _dug = Source.Length > 0 ? Source : spot.Kind != 0 ? _game.KindName(spot.Kind) : _game.KindName(1);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            if (_game.ClaimButton(spot) is var button and not 0) Session.Tools.PressHeadButton(button);
            var tapped = LastAction = Now;
            while (Running && Now - tapped < ClaimTake)
            {
                Thread.Sleep(20);
                if (Session.Open.Top().Addr != 0 || _game.ClaimPoint(spot) == null) return true;
            }
        }
        return true;
    }

    /// <summary>
    /// Mua gói xẻng trên đảo (thay cho sửa): mở cửa hàng (nút xẻng góc trên phải) -> DialogShopInGame.OnClick_ItemBuy1 -> đóng. Chỉ bấm mua
    /// lại khi chắc cú trước không ăn (độ bền vẫn 0) để không mua 2 gói. Chuỗi = lỗi.
    /// </summary>
    private string? BuyShovels()
    {
        bool Fixed() => Durability() is { Item1: > 0 };
        bool ShopOpen() => Session.Open.Top().Is(Shop);
        Events.Status("Hết xẻng - đang mua xẻng mới...", Level.Warn);
        if (!ShopOpen())
        {
            if (_game.ShopButton() is var button and not 0) Session.Invoker.Call(DTA.Game.Invoke.Fn.UiButtonClick, button);
            if (!WaitUntil(ShopOpen, 1.5)) return "Không mở được cửa hàng mua xẻng trên đảo";
        }
        var opened = Now;
        var shop = Session.Open.Top().Addr;
        Sleep(OpenTime - (Now - opened));
        for (var i = 0; i < BuyTries && Running && !Fixed(); i++)
        {
            Session.Dialogs.Handle(shop, DialogAction.Buy);
            WaitUntil(Fixed, BuyWait);
        }
        if (ShopOpen())
        {
            Session.Dialogs.Handle(shop, DialogAction.Close);
            WaitUntil(() => !ShopOpen(), 1);
        }
        if (Durability() is { Item1: > 0 } tool)
        {
            Events.Tool(tool);
            Events.Status($"Đã mua xẻng mới ({tool.Item1}/{tool.Item2}) - đào tiếp...", Level.Ok);
            return null;
        }
        return "Không mua được xẻng (kiểm tra tiền sao trong game)";
    }
}
