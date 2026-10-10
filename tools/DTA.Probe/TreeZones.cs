using DTA.Game.Session;

/// <summary>Chỉ đọc: tìm FishingZone trong CẢ CÂY Transform chứa vài object đã biết (bản đồ, vật thể, nhân vật): treezones.</summary>
public static class TreeZones
{
    public static void Run(GameSession session, Action<string> step)
    {
        var seeds = new List<(string, long)> { ("map", session.Camera.CurrentMap()), ("control", session.Control) };
        seeds.AddRange(session.Map.Things().Select(t => ($"thing {t.Asset}", session.Managed.Ptr(t.Ref, "CollectObjectComp"))).Where(x => x.Item2 != 0).Take(2));
        var m = session.Managed;
        var doors = m.ArrayItems(m.Ptr(session.Camera.CurrentMap(), "DoorTriggers")).Where(d => d != 0).Take(2).Select(d => ("door", m.Ptr(d, "DoorTrigger")));
        seeds.AddRange(doors);
        var groups = m.ListItems(m.Ptr(session.Camera.CurrentMap(), "InsectSpawnGroups")).Take(1).Select(g => ("insect group", g));
        seeds.AddRange(groups);
        foreach (var (name, obj) in seeds)
        {
            var all = session.World.DescendantScripts(obj, true);
            var zones = all.Where(a => a.Class == "FishingZone").ToList();
            step($"{name}: {all.Count} script, FishingZone {zones.Count}");
            foreach (var (_, zone) in zones.Take(30))
            {
                var infos = session.Managed.ListItems(session.Managed.Ptr(zone, "FishingZoneList"));
                step($"   0x{zone:X}: {string.Join(", ", infos.Select(i => session.Managed.I32(i, "FishingZoneID")))}");
            }
        }
    }
}
