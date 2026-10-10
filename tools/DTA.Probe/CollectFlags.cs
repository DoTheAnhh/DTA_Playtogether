using DTA.Game.Session;
using DTA.Runtime.Core;

/// <summary>Chỉ đọc: các cờ chờ của CollectSystem (đào / nhận thưởng) + trạng thái đào nhân vật: flags.</summary>
public static class CollectFlags
{
    public static void Run(GameSession session, Action<string> step)
    {
        var sys = session.System("sysCollect");
        var klass = session.Managed.ClassOf(sys);
        foreach (var (f, size) in new[] { ("_waitCreateMonster", 1), ("_reqMonsterReward", 1), ("_closestExcavationObjId", 8), ("_rewardRequestUID", 8), ("_waitCreateObject", 1), ("_reqCollectObjectType", 4) })
        {
            var raw = session.Memory.Read(sys + session.Il2Cpp.Field(klass, f), size);
            step($"{f} = {(raw == null ? "?" : size == 8 ? Bin.U64(raw, 0).ToString() : size == 4 ? Bin.I32(raw, 0).ToString() : raw[0].ToString())}");
        }
        var held = session.Memory.Read(session.Control + session.Il2Cpp.Field(session.ControlClass, "ExcavateState"), 4);
        step($"ExcavateState = {(held != null ? Bin.I32(held, 0) : -1)}, điều khiển {session.Il2Cpp.FullName(session.ControlClass)}");
    }
}
