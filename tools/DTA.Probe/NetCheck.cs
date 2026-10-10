using DTA.Game.Session;
using DTA.Runtime.Core;

/// <summary>Thử giữ nút vợt N giây (theo dõi _insectState, _holdInsectNet, _isBtnPress, tốc độ) rồi hạ không vung: net giây (âm = chỉ theo dõi).</summary>
public static class NetCheck
{
    public static void Run(GameSession session, int seconds, Action<string> step)
    {
        var tool = session.Tools.HeldTool();
        step($"Vợt 0x{tool:X} ({session.Il2Cpp.FullName(session.Managed.ClassOf(tool))})");
        int state = session.Il2Cpp.Field(session.ControlClass, "_insectState"), hold = session.Il2Cpp.Field(session.ControlClass, "_holdInsectNet");
        var press = session.Il2Cpp.Field(session.Managed.ClassOf(tool), "_isBtnPress");
        var last = "";
        void Watch(double secs)
        {
            var end = DateTime.UtcNow.AddSeconds(secs);
            while (DateTime.UtcNow < end)
            {
                var c = session.Memory.Read(session.Control, Math.Max(state, hold) + 8);
                var t = session.Memory.Read(tool, press + 1);
                var m = session.Player.Motion();
                var line = $"state {(c == null ? -1 : Bin.I32(c, state))} hold {c?[hold]} press {t?[press]} v {(m is { } mo ? MathF.Sqrt(mo.Vx * mo.Vx + mo.Vz * mo.Vz) : 0):F2}";
                if (line != last) step(line);
                last = line;
                Thread.Sleep(50);
            }
        }
        Watch(0.5);
        if (seconds < 0)
        {
            Watch(-seconds);
            return;
        }
        step($"Giữ: {session.Tools.HoldNet()}");
        Watch(seconds);
        step($"Hạ: {session.Tools.LowerNet()}");
        Watch(3);
    }
}
