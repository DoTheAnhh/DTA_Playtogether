using System.Diagnostics;
using DTA.Features.Fishing;
using DTA.Game.Session;

/// <summary>Thử giả vùng: quét vùng câu của bản đồ, ghi ID giả vào tất cả, theo dõi CastingFishingZoneID N giây rồi trả gốc: faketest id giây.</summary>
public static class FakeTest
{
    public static void Run(GameSession session, int zone, int seconds, Action<string> step)
    {
        var game = new FishingGame(session);
        var sw = Stopwatch.StartNew();
        step($"Quét được {game.ScanMapZones(p => { })} vùng sau {sw.ElapsedMilliseconds} ms");
        step($"Ghi ID {zone}: {game.ApplyFakeZone(zone)} ô");
        var sys = session.System("sysFishing");
        var last = -1;
        sw.Restart();
        while (sw.Elapsed.TotalSeconds < seconds)
        {
            var id = session.Managed.I32(sys, "CastingFishingZoneID") ?? -1;
            if (id != last) step($"{sw.ElapsedMilliseconds} ms câu {game.Poll().State}: CastingFishingZoneID = {id}");
            last = id;
            Thread.Sleep(50);
        }
        game.RestoreZones();
        step("Đã trả ID gốc");
    }
}
