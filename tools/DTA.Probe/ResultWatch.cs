using DTA.Game.Session;

/// <summary>Chỉ đọc: chờ bảng kết quả câu hiện ra, in tên class + các field nút + nút keep / sell có đang hiện không.</summary>
public static class ResultWatch
{
    public static void Run(GameSession session, int seconds, Action<string> step)
    {
        var end = DateTime.UtcNow.AddSeconds(seconds);
        var seen = new HashSet<long>();
        while (DateTime.UtcNow < end)
        {
            var top = session.Open.Top();
            if (top.Addr != 0 && seen.Add(top.Addr))
            {
                var klass = session.Managed.ClassOf(top.Addr);
                var buttons = session.Il2Cpp.AllFields(klass).Select(f => f.Key).Where(n => n.Contains("Button", StringComparison.OrdinalIgnoreCase) || n.Contains("btn", StringComparison.OrdinalIgnoreCase));
                step($"Bảng {top.Name}: keep={session.Buttons.Find(top.Addr, "keep")} sell={session.Buttons.Find(top.Addr, "sell")}");
                string Flag(string f) => session.Il2Cpp.HasField(klass, f) && session.Managed.Ptr(top.Addr, f) is var p and not 0 ? $"{f}={session.World.IsShown(p)}" : $"{f}=-";
                step($"  hiện: {Flag("OpenButton")} {Flag("MembershipBoxButtonRoot")} {Flag("NotMembershipBoxButtonRoot")} {Flag("MembershipFishButtonRoot")} {Flag("NotMembershipFishButtonRoot")}");
            }
            Thread.Sleep(5);
        }
    }
}
