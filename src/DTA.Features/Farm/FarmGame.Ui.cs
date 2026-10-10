using DTA.Game.Geometry;
using DTA.Game.Ui;
using DTA.Runtime.Core;

namespace DTA.Features.Farm;

/// <summary>Phần nút + bảng nông trại của <see cref="FarmGame"/>.</summary>
public sealed partial class FarmGame
{
    public const string HarvestDialog = "DialogMyFarmHarvest", HarvestRow = "MyFarmHarvestItem", SellDialog = "DialogSelectItemDetail",
        SeedShopDialog = "DialogMyFarmTimedShop", SelectCountDialog = "DialogBoxSelectCount", UserItem = "PlayTogether.UserItem";

    private long MainMenu() => Session.Ui.Screens().GetValueOrDefault(DialogReader.MainMenu);

    /// <summary>Cụm nút bên phải đang thu gọn (DialogMainMenu._isButtonGroupFoldOut = 0).</summary>
    public bool MenuFolded() => Session.Optional(() => MainMenu() is var s and not 0 && Session.Memory.Read(s + Session.Il2Cpp.Field(M.ClassOf(s), "_isButtonGroupFoldOut"), 1) is [0], "cụm nút");

    /// <summary>Cụm nút đang hiện (FarmIds.FarmMenu = nút nông trại).</summary>
    public int? MenuSwitch() => Session.Optional(() => M.I32(MainMenu(), "_currentSwitchMenuType"), "loại cụm nút");

    /// <summary>Nút của màn chơi chính (ButtonMenuFold, ButtonMenuSwitch).</summary>
    public long MainButton(string name) => Session.Optional(() => M.Ptr(MainMenu(), name), name);

    /// <summary>Nút trong cụm nút nông trại (DialogMainMenu.MyFarmMenuGroup -> nút).</summary>
    public long FarmButton(string name) => Session.Optional(() => M.Ptr(M.Ptr(MainMenu(), "MyFarmMenuGroup"), name), name);

    /// <summary>Bong bóng nói chuyện (trên đầu NPC) gần nhân vật nhất trong <paramref name="limit"/> m - ở quầy NPC thay vì ghi cứng vị trí NPC.</summary>
    public ScreenPoint? NearestBubble(float limit) => Session.Optional<ScreenPoint?>(() =>
    {
        if (Session.Player.Position() is not { } me) return null;
        (float Dist, Vec3 Place)? best = null;
        foreach (var group in M.ListItems(M.Ptr(Session.System("sysHud"), "_selectButtonList")))
        {
            var shown = M.ListItems(M.Ptr(group, "onHeadUpList"));
            var klass = shown.Count > 0 ? M.ClassOf(shown[0]) : 0;
            if (klass == 0 || !Session.Il2Cpp.HasField(klass, "targetPos")) continue;
            var at = Session.Il2Cpp.Field(klass, "targetPos");
            foreach (var raw in Session.Memory.ReadObjects(shown, at + 12).Values)
            {
                var place = new Vec3(Bin.F32(raw, at), me.Y, Bin.F32(raw, at + 8));
                var dist = place.FlatDistance(me);
                if (dist <= limit && (best == null || dist < best.Value.Dist)) best = (dist, place);
            }
        }
        return best is { } b ? Session.HeadUps.Above(b.Place) : null;
    }, "bong bóng NPC");

    /// <summary>(số quả chín game đang liệt kê, nút "Thu hoạch" của từng dòng đang hiện) trong bảng thu hoạch (Wrapper._items -> MyFarmHarvestItem.ButtonHarvest).</summary>
    public (int Count, List<long> Buttons) HarvestRows(long dialog) => Session.Optional<(int, List<long>)?>(() =>
    {
        var wrapper = M.Ptr(dialog, "Wrapper");
        var count = M.I32(wrapper, "_dataSize") ?? 0;
        var rows = M.ListItems(M.Ptr(wrapper, "_items"));
        var buttons = Session.World.Scripts(rows).Values.Select(s => M.Ptr(s.GetValueOrDefault(HarvestRow), "ButtonHarvest")).Where(Bin.IsPtr).ToList();
        return (count, buttons);
    }, "bảng thu hoạch") ?? (0, []);

    /// <summary>ItemUID các món đang chọn trong bảng bán (_selectedSellItemList); null nếu phần tử không phải UserItem.</summary>
    public List<long>? SellSelected(long dialog) => Session.Optional(() =>
    {
        var items = M.ListItems(M.Ptr(dialog, "_selectedSellItemList"));
        if (items.Count > 0 && !M.Names(items[0]).Contains(UserItem)) return null;
        var (at, data) = Fields(items, "ItemUID");
        return data.Values.Select(r => Bin.U64(r, at["ItemUID"])).ToList();
    }, "món đang chọn bán");

    /// <summary>Nút Mua trong cửa hàng hạt: PurchaseButton_Type1 (xu hoa) / Type2 (kim cương) -> buttonPurchase.</summary>
    public long SeedBuyButton(long dialog, bool diamond) =>
        Session.Optional(() => M.Ptr(M.Ptr(dialog, diamond ? "PurchaseButton_Type2" : "PurchaseButton_Type1"), "buttonPurchase"), "nút mua hạt");
}
