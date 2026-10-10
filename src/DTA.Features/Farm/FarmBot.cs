using DTA.Engine.Bots;
using DTA.Game.Actions;
using DTA.Game.Invoke;
using DTA.Game.Ui;
using DTA.Runtime.Core;
using DTA.Runtime.Device;

namespace DTA.Features.Farm;

/// <summary>Sự kiện riêng của bot nông trại: số liệu vừa đọc, 1 món vừa thu hoạch / bán.</summary>
public sealed class FarmEvents
{
    public Action<FarmInfo> Info { get; init; } = _ => { };
    public Action<FarmRecord> Record { get; init; } = _ => { };
}

/// <summary>
/// Bot nông trại, 1 việc chọn trước khi Bật: Scan (đọc số liệu rồi dừng), Harvest, Sell, Plant, BuySeeds. Nút nông trại gọi qua hàm game
/// (UIButton.OnClick / hàm riêng của bảng); trước mỗi thao tác đọc lại bảng đang mở, gặp bảng lạ thì chờ chứ không bấm bừa.
/// </summary>
public sealed partial class FarmBot(EmulatorDevice device, BotEvents events, FarmEvents farm, FarmOptions options) : Bot(device, events)
{
    protected override string Channel => "farm";
    private const double RipeWait = 10, ReapWait = 4, ShopMoveWait = 3, OpenTime = 0.45;
    private const float BubbleRange = 5;
    private const int ReapMisses = 3;
    private FarmGame _game = null!;

    public FarmMode Mode { get; set; } = FarmMode.Scan;
    private static double Now => Environment.TickCount64 / 1000.0;

    protected override (int, int)? Durability() => null;

    protected override void Prepare() => _game = new FarmGame(Session);

    protected override string? Work()
    {
        Report();
        return Mode switch
        {
            FarmMode.Harvest => Harvest(),
            FarmMode.Sell => Sell(),
            FarmMode.Plant => Plant(),
            FarmMode.BuySeeds => BuySeeds(),
            _ => null,
        };
    }

    /// <summary>Đọc 1 lượt mọi số liệu nông trại (kèm tên + nền từng ID để giao diện khỏi đọc game).</summary>
    private FarmInfo Report()
    {
        var now = FarmIds.Now;
        var crops = _game.Crops();
        var fruits = _game.Fruits();
        var bag = new[] { FarmIds.SeedGroup, FarmIds.FruitGroup, FarmIds.GearGroup }.ToDictionary(g => g, _game.Bag);
        var shop = _game.SeedShop();
        var ids = fruits.Select(f => f.ItemId).Concat(crops.Select(c => c.SeedId)).Concat(bag.Values.SelectMany(i => i).Select(i => i.ItemId))
            .Concat(shop?.Goods.Select(g => g.ItemId) ?? []).Distinct();
        var info = new FarmInfo(_game.InFarm(), crops, _game.MaxPlants(), fruits.Where(f => f.End <= now).ToList(), fruits.Where(f => f.End > now).ToList(),
            bag, shop, ids.ToDictionary(i => i, i => Session.Tables.Item(i)), _game.Mutations(), now);
        Session.SaveCache();
        farm.Info(info);
        return info;
    }

    /// <summary>
    /// Bấm 1 nút cụm nông trại: cụm đang thu gọn thì mở ra; đang ở menu thường thì đổi sang menu nông trại (nút ⇆) trước. Gọi UIButton.OnClick.
    /// </summary>
    private bool PressFarmButton(string name)
    {
        if (_game.MenuFolded())
        {
            Click(_game.MainButton("ButtonMenuFold"));
            WaitUntil(() => !_game.MenuFolded(), 1.5);
            Sleep(OpenTime);
        }
        if (_game.MenuSwitch() != FarmIds.FarmMenu)
        {
            for (var i = 0; i < 2 && _game.MenuSwitch() != FarmIds.FarmMenu; i++)
            {
                Click(_game.MainButton("ButtonMenuSwitch"));
                WaitUntil(() => _game.MenuSwitch() == FarmIds.FarmMenu, 2);
            }
            Sleep(OpenTime);
        }
        return Click(_game.FarmButton(name));
    }

    private bool Click(long button) => Bin.IsPtr(button) && Session.Invoker.Call(Fn.UiButtonClick, button);

    /// <summary>Đóng 1 bảng (DialogCloseFromBack) rồi chờ nó biến mất.</summary>
    private void Close(long dialog)
    {
        Session.Dialogs.Handle(dialog, DialogAction.Close);
        WaitUntil(() => Session.Open.Top().Addr != dialog, 2);
    }

    /// <summary>Ghi lịch sử món mới xuất hiện (thu hoạch) / biến mất (bán) trong túi, so 2 lần đọc.</summary>
    private void RecordChange(List<BagItem> before, List<BagItem> after, bool harvest)
    {
        var old = before.ToDictionary(i => i.Uid);
        var now = after.ToDictionary(i => i.Uid);
        var changed = harvest
            ? now.Values.Where(i => !old.TryGetValue(i.Uid, out var o) || i.Count > o.Count).ToList()
            : old.Values.Where(i => !now.ContainsKey(i.Uid)).ToList();
        foreach (var item in changed)
        {
            var info = Session.Tables.Item(item.ItemId);
            farm.Record(new FarmRecord(DateTime.Now, harvest ? "Thu hoạch" : "Bán", item.ItemId, info.Name.Length > 0 ? info.Name : item.ItemId.ToString(), info.Grade,
                MathF.Round(item.Weight, 2), item.Mutations));
        }
    }

    /// <summary>Trồng: về Nhà ta rồi dùng hạt đang cầm (nút "dùng" HUD - bấm màn hình, gọi hàm lên hình HUD từng làm văng game).</summary>
    private string? Plant()
    {
        if (PressFarmButton("ButtonMoveToMyFarm"))
        {
            Events.Status("Di chuyển về Nhà ta để bắt đầu trồng...", Level.Info);
            Sleep(1.5);
        }
        while (Running)
        {
            if (!_game.InFarm()) return "Nhân vật đã rời nông trại";
            var crops = _game.Crops();
            var max = _game.MaxPlants();
            if (max > 0 && crops.Count >= max) Idle($"Nông trại đã đầy ({crops.Count}/{max} gốc) - chờ dọn dẹp hoặc thu hoạch...", 5);
            else if (options.PlantSeeds.Count == 0) Idle("Chưa chọn loại hạt giống cần trồng trên bảng - hãy tick chọn hạt giống", 5);
            else if (_game.Bag(FarmIds.SeedGroup).FirstOrDefault(s => !s.Locked && options.PlantSeeds.Contains(s.ItemId)) is not { } seed)
                Idle("Không còn hạt giống phù hợp trong túi - chờ mua thêm hạt...", 5);
            else
            {
                var name = Session.Tables.Item(seed.ItemId).Name;
                var plots = options.PlantPlots.Count > 0 ? $"ô {string.Join(',', options.PlantPlots.Order())}" : "tất cả ô đất";
                Events.Status($"Đang trồng {(name.Length > 0 ? name : seed.ItemId)} ({plots}, khoảng cách {options.PlantSpacing}m)...", Level.Ok);
                if (Session.Ui.HudButton("cast") is { } cast) Touch.Tap(cast.X, cast.Y);
                Sleep(1);
                Report();
                Sleep(2);
            }
        }
        return null;
    }

    private void Idle(string message, double seconds)
    {
        Events.Status(message, Level.Quiet);
        Sleep(seconds);
    }
}
