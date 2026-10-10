using DTA.Engine.Gather;
using DTA.Game.Data;
using DTA.Game.Session;
using DTA.Runtime.Core;

namespace DTA.Features.Collect;

/// <summary>Vật nhặt tay trên sân: mã, vị trí, tên, loại, tên tài nguyên, object quản lý (để bấm bong bóng nhặt).</summary>
public sealed record Item(int Uid, float X, float Y, float Z, string Name, string Kind, string Asset, long Ref) : IGatherTarget;

/// <summary>Điểm bỏ qua vĩnh viễn (vật mọc kẹt trong tường...).</summary>
public sealed record IgnoreSpot(int MapId, string Kind, float X, float Z, float Radius, string Reason)
{
    public bool Matches(int mapId, Item item) => mapId == MapId && item.Kind == Kind && (item.X - X) * (item.X - X) + (item.Z - Z) * (item.Z - Z) <= Radius * Radius;
}

/// <summary>Tuỳ chọn nhặt đồ: các loại cần nhặt.</summary>
public sealed class CollectOptions : GatherOptions
{
    public const string Wood = "wood", Egg = "egg", Mushroom = "mushroom", Shrimp = "shrimp", Plant = "plant", Card = "card", Other = "other";
    public static readonly (string Kind, string Label)[] KindLabels = [(Wood, "Củi"), (Egg, "Trứng"), (Mushroom, "Nấm"), (Shrimp, "Tôm"), (Plant, "Rau củ"), (Card, "Thẻ")];

    public HashSet<string> Kinds { get; set; } = [.. KindLabels.Select(k => k.Kind)];

    public CollectOptions() => Radius = 0;

    public override void Normalize()
    {
        base.Normalize();
        Kinds = Kinds.Where(k => KindLabels.Any(l => l.Kind == k)).ToHashSet();
    }
}

/// <summary>Trạng thái nhặt (ActorDefaultControl._collectActionState).</summary>
public enum CollectState { Idle = 0, RewardReq = 1, RewardWait = 2, RewardReady = 3, RewardSuccess = 4, RewardFail = 5, Boasting = 6, Finish = 7 }

/// <summary>Đọc vật nhặt được + trạng thái nhặt.</summary>
public static class CollectReader
{
    private static readonly (string Word, string Kind)[] Words =
        [("cardcollect", CollectOptions.Card), ("treelog", CollectOptions.Wood), ("egg", CollectOptions.Egg), ("mushroom", CollectOptions.Mushroom),
         ("shrimp", CollectOptions.Shrimp), ("plants", CollectOptions.Plant), ("ing", CollectOptions.Plant)];
    private static readonly string[] NotPicked = ["excavation", "fishingzone", "monster", "slime", "snowman", "shop", "nametag"];

    /// <summary>Điểm bỏ qua vĩnh viễn.</summary>
    public static readonly IgnoreSpot[] Ignored = [new(1001, CollectOptions.Mushroom, -15.2f, 34.8f, 1.5f, "Vật thể mọc kẹt sâu trong khối va chạm tường")];

    /// <summary>Loại vật nhặt tay; null nếu không nhặt được (quặng, vùng câu, quái...).</summary>
    public static string? KindOf(string asset)
    {
        if (Things.Describe(asset).Group == Things.Ore || NotPicked.Any(asset.Contains)) return null;
        var words = Things.Parts(asset);
        return Words.FirstOrDefault(w => words.Contains(w.Word)).Kind ?? CollectOptions.Other;
    }

    /// <summary>Vật nhặt được đang có trên bản đồ.</summary>
    public static List<Item> Items(GameSession session) => session.Map.Things()
        .Where(t => t.Step > 0 && !MapObjects.GoneStates.Contains(t.State) && KindOf(t.Asset) != null)
        .Select(t => KindOf(t.Asset) is var kind && kind == CollectOptions.Card
            ? new Item(t.Uid, t.X, t.Y, t.Z, "Gói thẻ", kind, t.Asset, t.Ref)
            : new Item(t.Uid, t.X, t.Y, t.Z, Things.Describe(t.Asset).Name, kind!, t.Asset, t.Ref))
        .ToList();

    /// <summary>Trạng thái nhặt hiện tại (1 lần đọc).</summary>
    public static CollectState State(GameSession session)
    {
        if (session.ControlClass == 0) session.LocateActor();
        if (!session.Il2Cpp.Is(session.ControlClass, "ActorDefaultControl")) throw new GameError("Nhân vật đang ở trạng thái không nhặt đồ được (đang lái xe?)");
        return (CollectState)session.ControlValue(session.Il2Cpp.Field(session.ControlClass, "_collectActionState"));
    }
}
