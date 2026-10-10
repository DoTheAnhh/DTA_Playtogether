using DTA.Game.Session;
using DTA.Runtime.Core;

/// <summary>Chỉ đọc: in TransformHierarchy của object vùng bạch tuộc (dò mảng con trỏ Transform): hier.</summary>
public static class HierDump
{
    public static void Run(GameSession session, Action<string> step)
    {
        var thing = session.Map.Things().First(t => t.Asset.Contains("fishingzone_octo"));
        var comp = session.Managed.Ptr(thing.Ref, "CollectObjectComp");
        var l = session.World.Layout()!;
        var compNative = (long)session.Memory.U64(comp + l.Cached);
        var go = (long)session.Memory.U64(compNative + l.ComponentGo);
        var arr = (long)session.Memory.U64(go + l.GoComponents);
        var cnt = session.Memory.Read(go + l.GoComponents + 0x10, 4) is { } c ? Bin.I32(c, 0) : -1;
        var entries = session.Memory.Read(arr, 16 * Math.Max(1, Math.Min(cnt, 8)))!;
        step($"comp native 0x{compNative:X} go 0x{go:X} comps {cnt}: {string.Join(" ", Enumerable.Range(0, entries.Length / 16).Select(i => $"[{Bin.U64(entries, 16 * i):X}:{Bin.U64(entries, 16 * i + 8):X}]"))}");
        var native = (long)Bin.U64(entries, 8);
        var head = session.Memory.Read(native + l.Hierarchy, 16)!;
        long h = Bin.U64(head, 0); var index = Bin.I32(head, 8);
        step($"layout Cached 0x{l.Cached:X} Hierarchy 0x{l.Hierarchy:X} GoComp 0x{l.ComponentGo:X}; native 0x{native:X} hierarchy 0x{h:X} index {index}");
        var t = session.Memory.Read(h, 0x100)!;
        for (var o = 0; o < 0x100; o += 8)
        {
            var v = (long)Bin.U64(t, o);
            var hit = Bin.IsPtr(v) ? (long)session.Memory.U64(v + 8L * index) : 0;
            step($"  +{o:X2}: 0x{v:X}  [{index}]→0x{hit:X}{(hit == native ? "  <== native" : "")}");
        }
    }
}
