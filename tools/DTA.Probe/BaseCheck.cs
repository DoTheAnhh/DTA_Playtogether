using DTA.Game.Invoke;
using DTA.Game.Session;
using DTA.Runtime.Core;

/// <summary>Kiểm tra chỉ đọc: các vùng libil2cpp.so + base nào làm vtable nhân vật trỏ đúng OnUpdate.</summary>
public static class BaseCheck
{
    public static void Run(GameSession session, Action<string> step)
    {
        var regions = session.Memory.Regions().Where(r => r.Name.EndsWith("libil2cpp.so")).ToList();
        foreach (var r in regions) step($"  vùng 0x{r.Start:X}-0x{r.End:X} {r.Perms}");
        var raw = session.Memory.Read(session.ControlClass + 0x138, 24 * 16)!;
        foreach (var start in regions.Select(r => r.Start).Distinct())
        {
            var target = start + Fn.ActorUpdate.Offset;
            var index = Enumerable.Range(0, 24).FirstOrDefault(i => Bin.U64(raw, i * 16) == target, -1);
            step($"  base 0x{start:X}: OnUpdate ở slot {index}");
        }
    }
}
