using System.Collections.Concurrent;
using DTA.Game.Session;
using DTA.Runtime.Core;

namespace DTA.Game.Data;

/// <summary>Class controller của từng dụng cụ cầm tay + ItemSubType trong bảng Item.</summary>
public static class Tools
{
    public const string Rod = "FishingPoleController", Net = "InsectNetController", Pickaxe = "PickaxController", Shovel = "ShovelController";
    public const int HandItem = 45, SubRod = 1, SubNet = 3, SubPickaxe = 4, SubShovel = 6;
}

/// <summary>Đang cầm đúng loại dụng cụ không (null = không đọc được) + (lượt dùng còn, tối đa) nếu đọc được.</summary>
public readonly record struct HeldState(bool? Holding, (int Remaining, int Limit)? Uses)
{
    public static readonly HeldState Unknown = new(null, null);
}

/// <summary>Đọc dụng cụ đang cầm (control._equipHandItem) và độ bền còn lại (sysEquip._selectItem.UseCount) - dùng chung theo phiên.</summary>
public sealed class HeldTool(GameSession session)
{
    private const int TreasureShovelLimit = 10;
    private readonly ConcurrentDictionary<long, (int Id, int Uid, long Select, int RecordUid, int RecordCount)?> _layouts = new();

    /// <summary>Trạng thái cầm dụng cụ có class <paramref name="toolClass"/> (xem <see cref="Tools"/>).</summary>
    public HeldState Read(string toolClass) => session.Optional<HeldState?>(() => ReadCore(toolClass), "dụng cụ") ?? HeldState.Unknown;

    /// <summary>
    /// Đảo bị mất (ActorTreasureHuntPlayer): chỉ có xẻng, số lượt ở EquipGameItemCount / 10. Bình thường: món cầm giữ ID + UID, bản
    /// ghi UserItem ở sysEquip._selectItem phải cùng UID (khác = người chơi vừa đổi đồ trong túi).
    /// </summary>
    private HeldState ReadCore(string toolClass)
    {
        var (memory, il2cpp) = (session.Memory, session.Il2Cpp);
        if (session.ControlClass == 0) session.LocateActor();
        var (control, controlClass) = (session.Control, session.ControlClass);
        if (il2cpp.Is(controlClass, "ActorTreasureHuntPlayer"))
        {
            if (toolClass != Tools.Shovel) return new(false, null);
            var raw = memory.Read(control + il2cpp.Field(controlClass, "EquipGameItemCount"), 4);
            return new(true, (raw != null ? Bin.I32(raw, 0) : TreasureShovelLimit, TreasureShovelLimit));
        }
        var tool = (long)memory.U64(control + il2cpp.Field(controlClass, "_equipHandItem"));
        var toolType = session.Managed.ClassOf(tool);
        if (il2cpp.FullName(toolType) != toolClass) return new(false, null);
        if (!_layouts.TryGetValue(toolType, out var layout) && (layout = LayoutOf(toolType)) != null) _layouts[toolType] = layout;
        if (layout is not { } l) return new(true, null);
        var head = memory.Read(tool, Math.Max(l.Id, l.Uid) + 8);
        var record = memory.Read((long)memory.U64(l.Select), Math.Max(l.RecordUid, l.RecordCount) + 8);
        if (head == null || record == null || Bin.U64(record, l.RecordUid) != Bin.U64(head, l.Uid)) return new(true, null);
        var itemId = (int)Bin.U32(head, l.Id);
        return new(true, ((int)Bin.U32(record, l.RecordCount), session.Tables.UseLimit(itemId)));
    }

    /// <summary>Offset các field cần đọc cho 1 loại dụng cụ; null nếu chưa có bản ghi đang chọn.</summary>
    private (int, int, long, int, int)? LayoutOf(long toolType)
    {
        var equip = session.System("sysEquip");
        var select = equip != 0 ? equip + session.Il2Cpp.Field(session.Managed.ClassOf(equip), "_selectItem") : 0;
        var recordClass = select != 0 ? session.Managed.ClassOf((long)session.Memory.U64(select)) : 0;
        if (recordClass == 0) return null;
        var il2cpp = session.Il2Cpp;
        return (il2cpp.Field(toolType, "currentItemID"), il2cpp.Field(toolType, "currentItemUID"), select,
            il2cpp.Field(recordClass, "ItemUID"), il2cpp.Field(recordClass, "UseCount"));
    }
}
