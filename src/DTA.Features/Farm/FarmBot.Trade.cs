using DTA.Engine.Bots;
using DTA.Game.Actions;
using DTA.Game.Invoke;
using DTA.Game.Ui;
using DTA.Runtime.Core;

namespace DTA.Features.Farm;

/// <summary>Phần thu hoạch / bán / mua hạt của <see cref="FarmBot"/>.</summary>
public sealed partial class FarmBot
{
    /// <summary>
    /// Thu hoạch: đủ số quả chín khớp lọc thì mở bảng "Thu hoạch trái" rồi gọi DialogMyFarmHarvest.OnClick_HarvestAll (thu hết 1 lần - bản cũ
    /// bấm từng dòng tới khi hết, kết quả như nhau); không được thì nút "Thu hoạch" dòng đầu. 3 lần game không thu thì dừng (túi đầy?).
    /// </summary>
    private string? Harvest()
    {
        var misses = 0;
        while (Running)
        {
            if (!_game.InFarm()) return "Nhân vật đã rời nông trại";
            var top = Session.Open.Top();
            if (!top.Is(FarmGame.HarvestDialog))
            {
                if (top.Addr != 0)
                {
                    if (!Session.Dialogs.HandleCommon(top.Addr, top.Names) && !Session.Dialogs.CloseObstruction(top.Addr, top.Names)) Idle("Game đang mở bảng khác - chờ đóng bảng...", 1);
                    continue;
                }
                var info = Report();
                if (options.HarvestFruits.Count == 0)
                {
                    Idle("Chưa chọn loại cây thu hoạch trên bảng - hãy tick chọn loại quả cần hái", 5);
                    continue;
                }
                var ripe = info.Ripe.Count(options.Harvestable);
                if (ripe < Math.Max(1, options.HarvestMin))
                {
                    var soon = info.Growing.Count > 0 ? info.Growing.Min(f => f.End) : 0;
                    Idle($"{ripe} quả chín khớp lọc, chờ đủ {options.HarvestMin}{(soon > 0 ? $", quả kế chín sau {Math.Max(soon - FarmIds.Now, 0):F0} giây" : "")}", RipeWait);
                    continue;
                }
                Events.Status($"Mở bảng thu hoạch ({ripe} quả chín)...", Level.Info);
                PressFarmButton("ButtonFarmHarvest");
                if (WaitUntil(() => Session.Open.Top().Is(FarmGame.HarvestDialog), 3)) Sleep(OpenTime);
                continue;
            }
            var (count, buttons) = _game.HarvestRows(top.Addr);
            if (count == 0)
            {
                Close(top.Addr);
                continue;
            }
            var before = _game.Bag(FarmIds.FruitGroup);
            Events.Status($"Đang thu hoạch {count} quả chín...", Level.Info);
            var called = Session.Invoker.Call(Fn.FarmHarvestAll, top.Addr) || buttons.Count > 0 && Session.Invoker.Call(Fn.FarmHarvestRow, RowOf(buttons[0]));
            if (!called || !WaitUntil(() => Session.Open.Top().Addr != top.Addr || _game.HarvestRows(top.Addr).Count < count, ReapWait))
            {
                if (++misses >= ReapMisses) return $"Đã thu hoạch {ReapMisses} lần mà game không thu - kiểm tra trong game (túi nông sản đầy?)";
                continue;
            }
            misses = 0;
            RecordChange(before, _game.Bag(FarmIds.FruitGroup), harvest: true);
            var after = Session.Open.Top();
            if (after.Addr != 0 && after.Addr != top.Addr && !after.Is(FarmGame.HarvestDialog)) Session.Dialogs.Handle(after.Addr);
        }
        return null;
    }

    /// <summary>Script MyFarmHarvestItem chứa nút (nút -> GameObject -> script); 0 nếu không thấy.</summary>
    private long RowOf(long button)
    {
        var transform = Session.World.TransformOf(button).Transform;
        return transform != 0 ? Session.World.Scripts([transform]).GetValueOrDefault(transform)?.GetValueOrDefault(FarmGame.HarvestRow) ?? 0 : 0;
    }

    /// <summary>
    /// Bán: tới quầy Manseok (nút "Bán" cụm nông trại - game tự dời nhân vật), nói chuyện -> "Bán nông sản" -> bảng bán -> "Chọn tự động" của
    /// game; CHỈ bán khi mọi món game chọn đều khớp lọc của người dùng.
    /// </summary>
    private string? Sell()
    {
        while (Running)
        {
            if (!_game.InFarm()) return "Nhân vật đã rời nông trại";
            if (options.SellFruits.Count == 0)
            {
                Idle("Chưa chọn loại nông sản cần bán trên bảng - hãy tick chọn loại quả cần bán", 5);
                continue;
            }
            var wanted = _game.Bag(FarmIds.FruitGroup).Where(i => options.Sellable(i, Session.Tables.Item(i.ItemId).Grade)).Select(i => i.Uid).ToHashSet();
            if (wanted.Count == 0)
            {
                Idle("Không có nông sản nào khớp lọc để bán - chờ nông sản mới...", 10);
                continue;
            }
            if (Session.Open.Top().Addr != 0)
            {
                Events.Status("Hãy đóng các bảng đang mở trong game để bán...", Level.Warn);
                Sleep(2);
                continue;
            }
            if (!PressFarmButton("ButtonMoveToSellShop"))
            {
                Events.Status("Không thấy nút Bán của cụm nút nông trại", Level.Warn);
                Sleep(3);
                continue;
            }
            Events.Status($"Tới quầy bán ({wanted.Count} món khớp lọc)...", Level.Info);
            if (OpenNpc(0, FarmGame.SellDialog) is not { } dialog) continue;
            var error = AutoSell(dialog, wanted);
            if (Session.Open.Top().Addr == dialog) Close(dialog);
            var talk = Session.Open.Top();
            if (talk.Is(DialogReader.Talk) && Session.HeadUps.Talk(talk.Addr) is { Answers.Count: 0, Next: { } next }) Touch.Tap(next.X, next.Y);
            if (error != null) Events.Status(error, Level.Warn);
            Sleep(10);
        }
        return null;
    }

    /// <summary>
    /// Đã được game dời tới quầy: bấm bong bóng NPC gần nhất, chọn câu trả lời loại <paramref name="answer"/> rồi chờ bảng <paramref name="dialogClass"/>.
    /// Bong bóng + câu trả lời là widget NGUI không có hàm riêng nên bấm màn hình theo vị trí đọc từ game.
    /// </summary>
    private long? OpenNpc(int answer, string dialogClass)
    {
        ScreenPoint? bubble = null;
        var deadline = Now + ShopMoveWait;
        while (Running && bubble == null && Now < deadline)
        {
            Thread.Sleep(200);
            bubble = _game.NearestBubble(BubbleRange);
        }
        if (bubble is not { } b)
        {
            Events.Status("Không thấy bong bóng của NPC - thử lại...", Level.Warn);
            Sleep(2);
            return null;
        }
        Touch.Tap(b.X, b.Y);
        if (!WaitUntil(() => Session.Open.Top().Is(DialogReader.Talk), 3)) return Retry();
        var talk = Session.Open.Top().Addr;
        Sleep(OpenTime);
        var choice = Session.HeadUps.Talk(talk)?.Answers.FirstOrDefault(a => a.Kind == answer);
        if (choice is not { Point: var p } || p == default) return Retry();
        Touch.Tap(p.X, p.Y);
        if (!WaitUntil(() => Session.Open.Top().Is(dialogClass), 3)) return Retry();
        var dialog = Session.Open.Top().Addr;
        Sleep(OpenTime);
        return dialog;

        long? Retry()
        {
            Sleep(2);
            return null;
        }
    }

    /// <summary>"Chọn tự động" rồi kiểm từng món game chọn: có món người dùng muốn giữ thì KHÔNG bán; khớp hết thì bấm Bán + xác nhận.</summary>
    private string? AutoSell(long dialog, HashSet<long> wanted)
    {
        if (!Click(Session.Managed.Ptr(dialog, "ButtonCheckAll"))) return "Không thấy nút \"Chọn tự động\"";
        WaitUntil(() => _game.SellSelected(dialog) is { Count: > 0 }, 1.5);
        var selected = _game.SellSelected(dialog);
        if (selected == null) return "Chưa đọc được danh sách món đang chọn trong bảng bán";
        if (selected.Count == 0)
        {
            Events.Status("\"Chọn tự động\" không chọn món nào", Level.Ok);
            return null;
        }
        var extra = selected.Count(uid => !wanted.Contains(uid));
        if (extra > 0) return $"\"Chọn tự động\" của game chọn cả {extra} món bạn muốn giữ - tool KHÔNG bán. Nới lọc hoặc bán tay";
        var before = _game.Bag(FarmIds.FruitGroup);
        if (!Click(Session.Managed.Ptr(dialog, "ButtonSell"))) return "Không thấy nút Bán";
        WaitUntil(() => Session.Open.Top().Addr != dialog || _game.SellSelected(dialog) is not { Count: > 0 }, 3);
        var after = _game.Bag(FarmIds.FruitGroup);
        RecordChange(before, after, harvest: false);
        if (Session.Open.Top() is { Addr: not 0 } top && top.Addr != dialog) Session.Dialogs.Handle(top.Addr);
        var sold = before.Count - after.Count;
        Events.Status(sold > 0 ? $"Đã bán {sold} món" : "Đã bấm Bán nhưng túi không đổi - kiểm tra trong game", sold > 0 ? Level.Ok : Level.Warn);
        return null;
    }

    /// <summary>
    /// Mua hạt đã chọn tới đủ giới hạn: tới quầy Yeongman -> "Mua" -> cửa hàng -> OnClick_PurchaseButton1/2 (xu hoa / kim cương) -> bảng chọn số
    /// lượng: ghi thẳng selectCount = số còn thiếu (trong khoảng min..max của bảng) rồi OnClick_PriceButton (1 lệnh thay cho bấm "+" nhiều lần).
    /// </summary>
    private string? BuySeeds()
    {
        while (Running)
        {
            if (!_game.InFarm()) return "Nhân vật đã rời nông trại";
            if (options.BuySeeds.Count == 0)
            {
                Idle("Chưa chọn loại hạt giống cần mua trên bảng - hãy tick chọn hạt cần mua", 5);
                continue;
            }
            if (_game.SeedShop() is not var (_, goods))
            {
                Idle("Chưa đọc được dữ liệu cửa hàng hạt - chờ...", 5);
                continue;
            }
            var inBag = _game.Bag(FarmIds.SeedGroup).GroupBy(i => i.ItemId).ToDictionary(g => g.Key, g => g.Sum(i => i.Count));
            var wanted = goods.Where(g => options.BuySeeds.Contains(g.ItemId) && g.Stock > 0 && inBag.GetValueOrDefault(g.ItemId) < options.BuyLimit).ToList();
            var top = Session.Open.Top();
            if (wanted.Count == 0)
            {
                if (top.Is(FarmGame.SeedShopDialog)) Session.Invoker.Call(Fn.SeedShopClose, top.Addr);
                Idle("Đã đủ số lượng hạt giống hoặc cửa hàng hết hàng - chờ...", 10);
                continue;
            }
            if (top.Is(FarmGame.SelectCountDialog))
            {
                var need = Math.Max(options.BuyLimit - inBag.GetValueOrDefault(wanted[0].ItemId), 1);
                Events.Status($"Mua {need} hạt cho đủ {options.BuyLimit}...", Level.Info);
                ConfirmCount(top.Addr, need);
                WaitUntil(() => !Session.Open.Top().Is(FarmGame.SelectCountDialog), 2);
                Report();
                continue;
            }
            if (top.Is(FarmGame.SeedShopDialog))
            {
                Session.Invoker.Call(options.BuyWithDiamond ? Fn.SeedBuyDiamond : Fn.SeedBuyFlower, top.Addr);
                if (!WaitUntil(() => Session.Open.Top().Is(FarmGame.SelectCountDialog), 2.5)) Sleep(1);
                continue;
            }
            if (top.Is(DialogReader.Talk))
            {
                var buy = Session.HeadUps.Talk(top.Addr)?.Answers.FirstOrDefault(a => a.Kind == 4);
                if (buy is { Point: var p } && p != default) Touch.Tap(p.X, p.Y);
                if (WaitUntil(() => Session.Open.Top().Is(FarmGame.SeedShopDialog), 3)) Sleep(OpenTime);
                continue;
            }
            if (!PressFarmButton("ButtonMoveToSeedShop"))
            {
                Events.Status("Không thấy nút di chuyển tới quầy hạt giống", Level.Warn);
                Sleep(3);
                continue;
            }
            Events.Status($"Di chuyển tới quầy hạt giống ({wanted.Count} loại cần mua)...", Level.Info);
            OpenNpc(4, FarmGame.SeedShopDialog);
        }
        return null;
    }

    /// <summary>Đặt số lượng trong bảng chọn số lượng (kẹp min..max của bảng) rồi xác nhận bằng hàm của chính bảng.</summary>
    private void ConfirmCount(long dialog, int need)
    {
        var m = Session.Managed;
        int min = m.I32(dialog, "itemMinCount") ?? 1, max = m.I32(dialog, "itemMaxCount") ?? need;
        var count = Math.Clamp(need, Math.Max(min, 1), Math.Max(max, 1));
        Session.Memory.Write(dialog + Session.Il2Cpp.Field(m.ClassOf(dialog), "selectCount"), Bin.Pack(count));
        if (!Session.Invoker.Call(Fn.SelectCountPrice, dialog)) Session.Invoker.Call(Fn.SelectCountOk, dialog);
    }
}
