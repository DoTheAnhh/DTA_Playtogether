using DTA.Features.Fishing;
using DTA.Game.Session;
using DTA.Runtime.Core;

/// <summary>Chỉ đọc: theo dõi trạng thái câu + số rỉa giả của phao + số chạm còn lại của bóng cá trong N giây (kiểm cá cắn nhanh).</summary>
public static class FishWatch
{
    public static void Run(GameSession session, int seconds, Action<string> step)
    {
        var game = new FishingGame(session);
        var last = "";
        var end = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < end)
        {
            var state = game.Poll().State;
            var flt = game.Float();
            var fake = "";
            if (Bin.IsPtr(flt) && session.Memory.Read(flt + session.Il2Cpp.Field(session.Managed.ClassOf(flt), "FakeCountMin"), 8) is { } f) fake = $"rỉa giả {Bin.I32(f, 0)}-{Bin.I32(f, 4)}";
            var shadow = game.ShadowControl();
            var touch = "";
            if (Bin.IsPtr(shadow) && session.Memory.Read(shadow + session.Il2Cpp.Field(session.Managed.ClassOf(shadow), "_remainTouchCnt"), 8) is { } t)
                touch = $"chạm còn {session.DecodeEncryptInt(Bin.U32(t, 0), Bin.I32(t, 4))}";
            var cam = session.Camera.View() is { } v ? $"cam ({v.Item1.X:F1},{v.Item1.Y:F1},{v.Item1.Z:F1})" : "";
            var line = $"trạng thái {state} {fake} {touch} {cam}";
            if (line != last) step(line);
            last = line;
            Thread.Sleep(50);
        }
    }
}
