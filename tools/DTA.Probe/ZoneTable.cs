using DTA.Game.Session;
using DTA.Runtime.Core;

/// <summary>Chỉ đọc: bảng vùng câu TableFishingAreaImpl (ID, tên, ActionId) + mọi asset vật thể có "fishingzone": zones.</summary>
public static class ZoneTable
{
    public static void Run(GameSession session, Action<string> step)
    {
        var rows = session.Tables.Rows("TableFishingAreaImpl").Select(r => r.Row).ToList();
        step($"Bảng vùng câu: {rows.Count} dòng; bảng có chữ Fish: {string.Join(", ", session.Tables.Names().Where(n => n.Contains("Fish")))}");
        if (rows.Count > 0)
        {
            var klass = session.Managed.ClassOf(rows[0]);
            int id = session.Il2Cpp.Field(klass, "<FishingZoneId>k__BackingField"), text = session.Il2Cpp.Field(klass, "<FishingZoneText>k__BackingField"), action = session.Il2Cpp.Field(klass, "<ActionId>k__BackingField");
            var data = session.Memory.ReadObjects(rows, Math.Max(id, Math.Max(text, action)) + 4);
            foreach (var d in data.Values.OrderBy(d => Bin.U32(d, id)))
                step($"  {Bin.U32(d, id)} | {session.Tables.Text((int)Bin.U32(d, text))} | action {Bin.U32(d, action)}");
        }
        foreach (var (kind, asset) in session.Tables.SpawnAssets().Where(a => a.Value.Contains("fishingzone")).OrderBy(a => a.Key))
            step($"  asset {kind}: {asset}");
    }
}
