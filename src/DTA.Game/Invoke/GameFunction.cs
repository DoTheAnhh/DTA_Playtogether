namespace DTA.Game.Invoke;

/// <summary>1 hàm game gọi được qua hook: RVA trong libil2cpp.so (từ dump.cs) + class object nhận (kiểm trước khi gọi).</summary>
public sealed record GameFunction(long Offset, string Owner, string Name)
{
    public override string ToString() => $"{Owner}.{Name}";
}

/// <summary>Toàn bộ hàm game tool gọi (base dump 0x1517CE000). Thêm hàm = thêm 1 dòng ở đây.</summary>
public static class Fn
{
    /// <summary>Vùng trampoline: LayerSystem.&lt;ConnectFailProcess&gt;g__OnRestart (dài 0xAC) - nơi ghi payload.</summary>
    public const long Trampoline = 0x4FF3E50;

    private const string Actor = "ActorDefaultControl";
    public static readonly GameFunction ActorUpdate = new(0x57CA588, Actor, "OnUpdate");
    public static readonly GameFunction ActorFishing = new(0x57D233C, Actor, "OnClickFishing");
    public static readonly GameFunction ActorPickaxe = new(0x57DA818, Actor, "OnClickPickax");
    public static readonly GameFunction ActorExcavate = new(0x57D1034, Actor, "OnClickExcavate");
    public static readonly GameFunction ActorInsect = new(0x57D9370, Actor, "OnClickInsectCollecting");
    public static readonly GameFunction ActorPressNet = new(0x57C96B4, "ActorDefaultControlPlayer", "OnPressInsectNet");
    public static readonly GameFunction ActorHoldNet = new(0x579DBD4, Actor, "OnHoldInsectNet");
    public static readonly GameFunction ActorPickFieldObject = new(0x57CE884, Actor, "OnPickFieldObject");

    public static readonly GameFunction FishingPole = new(0x4EBDF2C, "FishingPoleController", "OnClick_Button");
    public static readonly GameFunction Shovel = new(0x5978AF4, "ShovelController", "OnClick_Button");
    public static readonly GameFunction Pickaxe = new(0x597829C, "PickaxController", "OnClick_Button");
    public static readonly GameFunction InsectNet = new(0x4F77F60, "InsectNetController", "OnClick_Button");
    public static readonly GameFunction Jump = new(0x5E70584, "DialogJoyStick", "OnPress_JumpButton");

    public static readonly GameFunction RequestExcavationReward = new(0x5996C04, "CollectSystem", "RequestExcvationReward");
    public static readonly GameFunction ConnectToZoneMove = new(0x4FEFD78, "LayerSystem", "ConnectToZoneMove");

    public static readonly GameFunction UiButtonClick = new(0x524D90C, "UIButton", "OnClick");
    public static readonly GameFunction HeadUpSelect = new(0x4FB123C, "HeadUpSelectButton", "OnClick");
    public static readonly GameFunction HeadUpBoxOpen = new(0x4F9CE60, "HeadUpBoxOpen", "OnClick_BoxOpen");

    public static readonly GameFunction DialogCloseFromBack = new(0x4E3CF84, "DialogUnit", "DialogCloseFromBack");
    public static readonly GameFunction DialogDelete = new(0x4E2E550, "DialogUnit", "DialogDelete");

    private const string Reward = "DialogRewardPopup";
    public static readonly GameFunction RewardYes = new(0x5A31C00, Reward, "OnClickYes");
    public static readonly GameFunction RewardFinish = new(0x5A31CC0, Reward, "FinishRewardAction");
    public static readonly GameFunction RewardNo = new(0x5A31CF4, Reward, "OnClickNo");
    public static readonly GameFunction RewardClose = new(0x5A2FFD4, Reward, "OnClickClose");

    private const string Question = "DialogBoxQuestion";
    public static readonly GameFunction QuestionOk = new(0x5A2A0F0, Question, "OnClick_OK");
    public static readonly GameFunction QuestionCancel = new(0x5A2A170, Question, "OnClick_Cancel");
    public static readonly GameFunction QuestionClose = new(0x5A2A2A8, Question, "OnClick_Close");

    public static readonly GameFunction MessageOk = new(0x5A29F80, "DialogBoxMessage", "OnClick_OK");
    public static readonly GameFunction MessageClose = new(0x5A2A000, "DialogBoxMessage", "OnClick_Close");

    private const string ItemView = "DialogResultGetItemView";
    public static readonly GameFunction ItemViewSkip = new(0x4D370D8, ItemView, "OnClick_ButtonSkip");
    public static readonly GameFunction ItemViewReveal = new(0x4D374D4, ItemView, "OnClick_ButtonRevealItem");
    public static readonly GameFunction ItemViewSwipe = new(0x4D37458, ItemView, "SetJoystickSwipe");
    public static readonly GameFunction ItemViewGachaPress = new(0x4D39374, ItemView, "OnPress_GachaBoxOpen");
    public static readonly GameFunction ItemListOk = new(0x4D346C0, "DialogResultGetItemList", "OnClick_ButtonOk");

    private const string FishResult = "DialogFishingGetItem";
    public static readonly GameFunction FishSell = new(0x5DE9C7C, FishResult, "OnClick_Selling");
    public static readonly GameFunction FishClose = new(0x5DE6BB4, FishResult, "OnClick_ButtonClose");
    public static readonly GameFunction FishCloseFromBack = new(0x5DE6BB0, FishResult, "DialogCloseFromBack");
    public static readonly GameFunction FishDelete = new(0x5DE6BA8, FishResult, "DialogDelete");
    public static readonly GameFunction FishHide = new(0x5DE6BA0, FishResult, "DialogHide");
    public static readonly GameFunction FishOpenPackage = new(0x5DE9FF8, FishResult, "OnClick_OpenPackagePopup");

    public static readonly GameFunction RepairRepair = new(0x5E6AC7C, "DialogItemRepair", "OnClick_Repair");
    public static readonly GameFunction RepairClose = new(0x5E6AD68, "DialogItemRepair", "OnClick_Close");
    public static readonly GameFunction AchievementClose = new(0x5CFA54C, "DialogAchievementLvUp", "OnClickClose");
    public static readonly GameFunction FarmHarvestAll = new(0x5EBE5C4, "DialogMyFarmHarvest", "OnClick_HarvestAll");
    public static readonly GameFunction FarmHarvestRow = new(0x5BA6B60, "MyFarmHarvestItem", "OnClick_HarvestButton");
    public static readonly GameFunction SeedBuyFlower = new(0x5ECF878, "DialogMyFarmTimedShop", "OnClick_PurchaseButton1");
    public static readonly GameFunction SeedBuyDiamond = new(0x5ECFA5C, "DialogMyFarmTimedShop", "OnClick_PurchaseButton2");
    public static readonly GameFunction SeedShopClose = new(0x5ECCE78, "DialogMyFarmTimedShop", "OnClick_Close");
    public static readonly GameFunction SelectCountPrice = new(0x5D60C30, "DialogBoxSelectCount", "OnClick_PriceButton");
    public static readonly GameFunction SelectCountOk = new(0x5D60BF0, "DialogBoxSelectCount", "OnClick_Ok");
    public static readonly GameFunction ShopBuy1 = new(0x4D93D2C, "DialogShopInGame", "OnClick_ItemBuy1");
    public static readonly GameFunction ShopClose = new(0x4D9273C, "DialogShopInGame", "OnClick_CloseButton");
}
