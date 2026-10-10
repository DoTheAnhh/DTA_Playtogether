using DTA.Game.Session;

/// <summary>Chỉ đọc: vật thể vùng câu đặc biệt trên bản đồ + class / field của object quản lý + script cùng GameObject: zoneobj.</summary>
public static class ZoneObjects
{
    public static void Run(GameSession session, Action<string> step)
    {
        var things = session.Map.Things().Where(t => t.Asset.Contains("fishingzone")).ToList();
        step($"Vùng đặc biệt trên bản đồ {session.Camera.Map()}: {things.Count}");
        foreach (var t in things)
        {
            var klass = session.Managed.ClassOf(t.Ref);
            step($"  {t.Asset} ({t.X:F1},{t.Z:F1}) manager {session.Il2Cpp.FullName(klass)}");
            var comp = session.Managed.Ptr(t.Ref, "CollectObjectComp");
            var ck = session.Managed.ClassOf(comp);
            step($"     comp 0x{comp:X} {session.Il2Cpp.FullName(ck)}");
            var all = session.World.DescendantScripts(comp);
            step($"     con cháu: {all.Count} script - {string.Join(", ", all.Select(a => a.Class).Distinct().Take(15))}");
            foreach (var (_, zone) in all.Where(a => a.Class == "FishingZone"))
            {
                var infos = session.Managed.ListItems(session.Managed.Ptr(zone, "FishingZoneList"));
                var ids = infos.Select(i => session.Managed.I32(i, "FishingZoneID")).ToList();
                step($"     FishingZone 0x{zone:X}: ID {string.Join(", ", ids)}");
            }
        }
    }
}
