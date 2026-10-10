using System.Diagnostics;
using DTA.Game.Session;
using DTA.Game.World;

/// <summary>Thử quét heap: tìm class FishingZoneInfo / FisheryZoneTrigger theo tên rồi mọi object của chúng (ID vùng): heapzones.</summary>
public static class HeapZones
{
    public static void Run(GameSession session, Action<string> step)
    {
        var sw = Stopwatch.StartNew();
        var info = HeapScan.FindClass(session, "FishingZoneInfo");
        var trigger = HeapScan.FindClass(session, "FisheryZoneTrigger");
        step($"class FishingZoneInfo 0x{info:X} ({session.Il2Cpp.FullName(info)}), FisheryZoneTrigger 0x{trigger:X} ({session.Il2Cpp.FullName(trigger)}) sau {sw.ElapsedMilliseconds} ms");
        var found = HeapScan.FindObjects(session, new[] { info, trigger }.Where(k => k != 0).ToList(), p => step($"   quét {p}"));
        step($"quét xong sau {sw.ElapsedMilliseconds} ms");
        foreach (var (klass, objects) in found)
        {
            var idAt = session.Il2Cpp.Field(klass, "FishingZoneID");
            var data = session.Memory.ReadObjects(objects, idAt + 4);
            var ids = data.Select(d => DTA.Runtime.Core.Bin.I32(d.Value, idAt)).Where(id => id is > 0 and < 1_000_000).ToList();
            step($"{session.Il2Cpp.FullName(klass)}: {objects.Count} object, ID hợp lệ {ids.Count}: {string.Join(", ", ids.Distinct().Order().Take(60))}");
        }
    }
}
