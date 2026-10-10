using DTA.Game.Session;
using DTA.Runtime.Core;

/// <summary>Chỉ đọc: in N lệnh ARM64 (hex) của hàm tại RVA dump: code rva n.</summary>
public static class CodeDump
{
    public static void Run(GameSession session, long rva, int count, Action<string> step)
    {
        var region = session.Memory.Regions().Where(r => r.Name.EndsWith("libil2cpp.so")).Select(r => r.Start).Where(s => s >= 0x7fff00000000).DefaultIfEmpty(session.Memory.Regions().Where(r => r.Name.EndsWith("libil2cpp.so")).Min(r => r.Start)).Min();
        var addr = region + rva - 0x1517CE000;
        var raw = session.Memory.Read(addr, count * 4)!;
        for (var i = 0; i < count; i++) step($"+{i * 4:X2}: {Bin.U32(raw, i * 4):X8}");
    }
}
