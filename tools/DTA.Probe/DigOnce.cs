using DTA.Features.Excavation;
using DTA.Game.Session;

/// <summary>Gọi đào N lần cách nhau gap ms, in trạng thái đào + độ bền xẻng: digonce lần gap.</summary>
public static class DigOnce
{
    public static void Run(GameSession session, int times, int gap, Action<string> step)
    {
        var game = new ExcavationGame(session);
        step($"Trước: trạng thái {game.State()}, xẻng {session.Held.Read(DTA.Game.Data.Tools.Shovel).Uses}");
        var last = "";
        for (var i = 0; i < times; i++)
        {
            step($"Đào {i + 1}: {session.Tools.Dig()}");
            var end = DateTime.UtcNow.AddMilliseconds(gap);
            while (DateTime.UtcNow < end)
            {
                var line = $"   trạng thái {game.State()}";
                if (line != last) step(line);
                last = line;
                Thread.Sleep(20);
            }
        }
        Thread.Sleep(1500);
        step($"Sau: trạng thái {game.State()}, xẻng {session.Held.Read(DTA.Game.Data.Tools.Shovel).Uses}");
    }
}
