using DTA.Engine.Gather;
using DTA.Game.Data;
using DTA.Game.Session;

namespace DTA.Features.Mining;

/// <summary>Trạng thái cú cuốc (ActorDefaultControl._pickaxState / ePickaxState trong dump).</summary>
public enum PickaxState
{
    None = 0, Pickaxing = 1, Miss = 2, RewardReq = 3, RewardReqCritical = 4, RewardWait = 5, RewardReady = 6, RewardSuccess = 7,
    RewardFail = 8, Boasting = 9, Finish = 10,
}

/// <summary>1 mạch đá / quặng: mã, vị trí, tên, số bậc còn, quặng sự kiện, cỡ lớn, quặng quý (bông cải, ngọc).</summary>
public sealed record Rock(int Uid, float X, float Y, float Z, string Name, int Step, bool Event, bool Large, bool Prized) : IGatherTarget;

/// <summary>Loại quặng người dùng chọn.</summary>
public enum RockKind { All, Rock, Event }

/// <summary>Tuỳ chọn đập đá: loại quặng muốn đập.</summary>
public sealed class MiningOptions : GatherOptions
{
    public static readonly (RockKind Kind, string Label)[] Kinds = [(RockKind.All, "Tất cả"), (RockKind.Rock, "Đá & quặng"), (RockKind.Event, "Quặng sự kiện")];
    public RockKind Kind { get; set; } = RockKind.All;

    public MiningOptions() => Radius = 0;
}

/// <summary>Đọc mạch đá + trạng thái cuốc - dùng chung theo phiên.</summary>
public static class MiningReader
{
    /// <summary>Mạch đá / quặng còn đập được (còn bậc, chưa đang biến mất). Quặng sự kiện: spawn_ore_(tên sự kiện)_...</summary>
    public static List<Rock> Rocks(GameSession session)
    {
        var rocks = new List<Rock>();
        foreach (var thing in session.Map.Things())
        {
            var (group, name) = Things.Describe(thing.Asset);
            if (group != Things.Ore || thing.Step <= 0 || MapObjects.GoneStates.Contains(thing.State)) continue;
            var words = Things.Parts(thing.Asset);
            var isEvent = words[0] == "ore" && words.Length > 1 && !Things.Sizes.ContainsKey(words[1]) && !words[1].All(char.IsDigit);
            var prized = words.Contains("broccoli") || words[0].Contains("gemvein");
            rocks.Add(new Rock(thing.Uid, thing.X, thing.Y, thing.Z, name, thing.Step, isEvent, words.Contains("l"), prized));
        }
        return rocks;
    }

    /// <summary>Trạng thái cuốc hiện tại (1 lần đọc); nhân vật không ở kiểu đi bộ thì báo lỗi.</summary>
    public static PickaxState State(GameSession session)
    {
        if (session.ControlClass == 0) session.LocateActor();
        if (!session.Il2Cpp.Is(session.ControlClass, "ActorDefaultControl")) throw new DTA.Runtime.Core.GameError("Nhân vật đang ở trạng thái không đập đá được (đang lái xe?)");
        return (PickaxState)session.ControlValue(session.Il2Cpp.Field(session.ControlClass, "_pickaxState"));
    }
}
