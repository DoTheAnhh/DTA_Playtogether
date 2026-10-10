using DTA.Features.Fishing;
using DTA.Game.Session;

/// <summary>Chỉ đọc: ID vùng quanh chỗ phao (FishingColliderCache) + CastingFishingZoneID / CatchFishingZone của FishingSystem: fakecheck.</summary>
public static class FakeCheck
{
    public static void Run(GameSession session, Action<string> step)
    {
        var game = new FishingGame(session);
        step($"Trạng thái câu {game.Poll().State}");
        foreach (var (info, id) in game.CastZones()) step($"  FishingZoneInfo 0x{info:X}: ID {id}");
        var sys = session.System("sysFishing");
        var cache = session.Managed.ArrayItems(session.Managed.Ptr(sys, "FishingColliderCache"));
        step($"FishingColliderCache: {cache.Count} ô, khác 0: {cache.Count(c => c != 0)}");
        foreach (var c in cache.Where(c => c != 0).Take(8))
        {
            var same = session.World.Scripts([c]).Values.SelectMany(d => d.Keys);
            var tree = session.World.DescendantScripts(c).Select(x => x.Class).Distinct();
            step($"  collider 0x{c:X} {session.Il2Cpp.FullName(session.Managed.ClassOf(c))} | cùng GO: {string.Join(",", same)} | cây con: {string.Join(",", tree)}");
        }
        step($"CastingFishingZoneID {session.Managed.I32(sys, "CastingFishingZoneID")}, CatchFishingZone field? {session.Il2Cpp.HasField(session.Managed.ClassOf(sys), "<CatchFishingZone>k__BackingField")}");
    }
}
