using System.Diagnostics;
using DTA.Game.Session;

/// <summary>Đo tốc độ: 1 lượt đọc bộ nhớ, côn trùng, vị trí, vật thể (mỗi việc 3 lần): bench.</summary>
public static class Bench
{
    public static void Run(GameSession session, Action<string> step)
    {
        void Time(string name, Func<object?> work)
        {
            for (var i = 0; i < 3; i++)
            {
                var sw = Stopwatch.StartNew();
                var result = work();
                step($"{name}: {sw.ElapsedMilliseconds} ms ({(result is System.Collections.ICollection c ? c.Count : result)})");
            }
        }
        Time("shell true", () => session.Memory.Shell.Run("true").Count);
        Time("đọc 8 byte", () => session.Memory.Read(session.Control, 8)?.Length);
        Time("đọc 64 KB", () => session.Memory.Read(session.Control, 65536)?.Length);
        Time("côn trùng", () => session.Map.Insects());
        var insects = session.Map.Insects();
        Time("vị trí côn trùng", () => session.World.Poses(insects.Select(i => i.Control)));
        Time("tra tên", () => { foreach (var i in insects) session.Tables.Item(i.Item); return insects.Count; });
        Time("vật thể", () => session.Map.Things());
    }
}
