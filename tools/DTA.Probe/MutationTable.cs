using DTA.Game.Session;
using DTA.Runtime.Core;

/// <summary>Chỉ đọc: bảng biến thể TableMutationListImpl (mã, tên, loại vật phẩm, kiểu, hệ số bán): mutations.</summary>
public static class MutationTable
{
    public static void Run(GameSession session, Action<string> step)
    {
        foreach (var group in new[] { "PreTables", "Tables", "CdnTables", "ServerTables" })
        {
            var tables = session.Tables.Group(group);
            foreach (var (name, addr) in tables.Where(t => t.Key.Contains("Mutation") || t.Key.Contains("FishingArea")))
            {
                var container = session.Managed.Ptr(addr, "_container");
                var count = session.Memory.Read(container + 0x18, 4) is { } c ? Bin.I32(c, 0) : -1;
                step($"{group}: {name} ({tables.Count} bảng) container 0x{container:X} count? {count}");
            }
        }
        var rows = session.Tables.ListRows("TableMutationListImpl");
        step($"Bảng biến thể: {rows.Count} dòng");
        if (rows.Count == 0) return;
        var klass = session.Managed.ClassOf(rows[0]);
        int F(string n) => session.Il2Cpp.Field(klass, $"<{n}>k__BackingField");
        int id = F("Mutationid"), text = F("StringId"), type = F("ItemType"), kind = F("MutationType"), sell = F("SellMultiplier");
        var data = session.Memory.ReadObjects(rows, sell + 4);
        foreach (var d in data.Values.OrderBy(d => Bin.U32(d, type)).ThenBy(d => Bin.U32(d, id)))
            step($"  loại vật phẩm {Bin.U32(d, type)} | mã {Bin.U32(d, id)} | {session.Tables.Text((int)Bin.U32(d, text))} | kiểu {Bin.I32(d, kind)} | x{Bin.F32(d, sell):0.##}");
    }
}
