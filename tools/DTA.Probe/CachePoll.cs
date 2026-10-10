using System.Diagnostics;
using DTA.Game.Session;
using DTA.Runtime.Core;

/// <summary>Chỉ đọc: đọc liên tục FishingColliderCache N giây, in lúc nó có collider (bắt khoảnh khắc game điền rồi xoá): cachepoll giây.</summary>
public static class CachePoll
{
    public static void Run(GameSession session, int seconds, Action<string> step)
    {
        var sys = session.System("sysFishing");
        var array = session.Managed.Ptr(sys, "FishingColliderCache");
        var zoneAt = session.Il2Cpp.Field(session.Managed.ClassOf(sys), "CastingFishingZoneID");
        var game = new DTA.Features.Fishing.FishingGame(session);
        var sw = Stopwatch.StartNew();
        string last = "";
        long reads = 0;
        while (sw.Elapsed.TotalSeconds < seconds)
        {
            var raw = session.Memory.Read(array + 0x20, 20 * 8);
            var z = session.Memory.Read(sys + zoneAt, 4);
            reads++;
            if (raw == null || z == null) continue;
            var hits = Enumerable.Range(0, 20).Select(i => Bin.U64(raw, i * 8)).Where(v => v != 0).ToList();
            var line = $"câu {game.Poll().State} vùng {Bin.U32(z, 0)}, cache {hits.Count}: {string.Join(" ", hits.Select(h => h.ToString("X")))}";
            if (line != last) step($"{sw.ElapsedMilliseconds,6} ms  {line}");
            last = line;
        }
        step($"{reads} lượt đọc ({reads / (double)seconds:F0}/s)");
    }
}
