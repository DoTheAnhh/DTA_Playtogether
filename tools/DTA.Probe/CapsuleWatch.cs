using DTA.Game.Session;
using DTA.Runtime.Core;

/// <summary>Chỉ đọc: vùng vợt InsectSystem._catchCapsule (Pos1, Pos2, Radius) so với vị trí + hướng nhân vật trong N giây: capsule giây.</summary>
public static class CapsuleWatch
{
    public static void Run(GameSession session, int seconds, Action<string> step)
    {
        var system = session.System("sysInsectCollection");
        var capsule = session.Managed.Ptr(system, "_catchCapsule");
        step($"InsectSystem 0x{system:X}, capsule 0x{capsule:X}, _detectCatchH {(session.Memory.Read(system + session.Il2Cpp.Field(session.Managed.ClassOf(system), "_detectCatchH"), 4) is { } h ? Bin.I32(h, 0) : -1)}");
        var last = "";
        var end = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < end)
        {
            capsule = session.Managed.Ptr(system, "_catchCapsule");
            var c = session.Memory.Read(capsule + 0x10, 0x1C);
            var f = session.Player.Position();
            var line = c == null ? "không đọc được" : $"P1 ({Bin.F32(c, 0):F2},{Bin.F32(c, 4):F2},{Bin.F32(c, 8):F2}) P2 ({Bin.F32(c, 12):F2},{Bin.F32(c, 16):F2},{Bin.F32(c, 20):F2}) R {Bin.F32(c, 24):F2}"
                + (f is { } ff ? $" | người ({ff.X:F2},{ff.Y:F2},{ff.Z:F2})" : "");
            if (line != last) step(line);
            last = line;
            Thread.Sleep(200);
        }
    }
}
