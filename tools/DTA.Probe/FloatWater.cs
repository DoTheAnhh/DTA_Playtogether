using System.Diagnostics;
using DTA.Features.Fishing;
using DTA.Game.Session;

/// <summary>Chỉ đọc: theo dõi phao._targetWaterCol N giây; có giá trị thì in FishingZone trên GameObject đó / cha: floatpoll giây.</summary>
public static class FloatWater
{
    public static void Run(GameSession session, int seconds, Action<string> step)
    {
        var game = new FishingGame(session);
        var sw = Stopwatch.StartNew();
        long last = -1;
        while (sw.Elapsed.TotalSeconds < seconds)
        {
            var fl = game.Float();
            var tw = fl != 0 ? session.Managed.Ptr(fl, "_targetWaterCol") : 0;
            if (tw != last)
            {
                last = tw;
                step($"{sw.ElapsedMilliseconds} ms câu {game.Poll().State} _targetWaterCol 0x{tw:X}");
                if (tw != 0)
                {
                    var all = session.World.AncestorScripts(tw);
                    step("   " + string.Join(", ", all.Select(x => x.Class)));
                    foreach (var (_, z) in all.Where(x => x.Class == "FishingZone"))
                        step($"   FishingZone: {string.Join(", ", session.Managed.ListItems(session.Managed.Ptr(z, "FishingZoneList")).Select(i => $"{session.Managed.I32(i, "FishingZoneID")} @0x{i:X}"))}");
                }
            }
            Thread.Sleep(20);
        }
    }
}
