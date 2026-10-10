using DTA.Game.Session;

/// <summary>Chỉ đọc: mọi FishingZone trong cây object của bản đồ hiện tại (ID + độ sâu + tên GameObject cha nếu có): allzones.</summary>
public static class AllZones
{
    public static void Run(GameSession session, Action<string> step)
    {
        var map = session.Camera.CurrentMap();
        var scripts = session.World.DescendantScripts(map);
        step($"Bản đồ 0x{map:X}: {scripts.Count} script trong cây; FishingZone: {scripts.Count(s => s.Class == "FishingZone")}");
        foreach (var (_, zone) in scripts.Where(s => s.Class == "FishingZone"))
        {
            var infos = session.Managed.ListItems(session.Managed.Ptr(zone, "FishingZoneList"));
            var ids = infos.Select(i => $"{session.Managed.I32(i, "FishingZoneID")}/sâu {session.Managed.I32(i, "Depth")}");
            step($"  0x{zone:X} @ {session.World.Position(zone)}: {string.Join(", ", ids)}");
        }
    }
}
