using System.Collections.Concurrent;
using DTA.Game.Geometry;
using DTA.Game.Session;
using DTA.Runtime.Core;

namespace DTA.Game.Data;

/// <summary>Vật thể bản đồ (sysCollect) và côn trùng đang sống (sysInsectCollection) - đọc theo lô, dùng chung theo phiên.</summary>
public sealed class MapObjects(GameSession session)
{
    /// <summary>MapObjectManager._state của vật vừa bị thu hoạch hết, đang biến mất.</summary>
    public static readonly int[] GoneStates = [6, 7];
    private static readonly Logger L = Log.For("map");
    private static readonly string[] InsectFields = ["ID", "UID", "control"];

    private int[]? _thingLayout, _insectLayout;
    private readonly ConcurrentDictionary<long, Vec3> _spawnSpots = new();
    private readonly ConcurrentDictionary<(uint Uid, uint Key, int Rand), int> _insectItems = new();

    /// <summary>
    /// Đá, quặng, cây, nguyên liệu đang có (sysCollect._mapObjectInfoList). Vị trí = tâm khối cầu bao (_cullingSphere); vật cỡ lớn
    /// game để tâm (0,0,0) thì lấy vị trí điểm mọc (_cacheSpawnInfo) - không bao giờ trả vật ở (0,0,0).
    /// </summary>
    public List<MapThing> Things()
    {
        var collect = session.System("sysCollect");
        var managers = collect != 0 ? session.Managed.ListItems(session.Managed.Ptr(collect, "_mapObjectInfoList")) : [];
        if (managers.Count == 0) return [];
        var memory = session.Memory;
        var il2cpp = session.Il2Cpp;
        if (_thingLayout == null)
        {
            var managerClass = session.Managed.ClassOf(managers[0]);
            var infoAt = il2cpp.Field(managerClass, "MapObjectInfo");
            var infoClass = session.Managed.ClassOf((long)memory.U64(managers[0] + infoAt));
            _thingLayout = [infoAt, il2cpp.Field(managerClass, "_state"), il2cpp.Field(managerClass, "_cullingSphere"),
                il2cpp.Field(infoClass, "ObjectId"), il2cpp.Field(infoClass, "ResourceId"), il2cpp.Field(infoClass, "Step"),
                il2cpp.DeclaredFields(managerClass).GetValueOrDefault("_cacheSpawnInfo", 0)];
        }
        var (info, state, sphere, id, resource, step, spawn) = (_thingLayout[0], _thingLayout[1], _thingLayout[2], _thingLayout[3], _thingLayout[4], _thingLayout[5], _thingLayout[6]);
        var assets = session.Tables.SpawnAssets();
        var data = memory.ReadObjects(managers, new[] { info + 8, state + 4, sphere + 12, spawn + 8 }.Max());
        var infos = memory.ReadObjects(data.Values.Select(r => Bin.U64(r, info)), new[] { id, resource, step }.Max() + 4);
        var things = new List<MapThing>();
        foreach (var (manager, raw) in data)
        {
            if (!infos.TryGetValue(Bin.U64(raw, info), out var row)) continue;
            var kind = Bin.I32(row, resource);
            var place = new Vec3(Bin.F32(raw, sphere), Bin.F32(raw, sphere + 4), Bin.F32(raw, sphere + 8));
            if (place == Vec3.Zero)
            {
                if (SpawnSpot(spawn != 0 ? Bin.U64(raw, spawn) : 0) is not { } spot) continue;
                place = spot;
            }
            things.Add(new MapThing(Bin.I32(row, id), manager, assets.GetValueOrDefault(kind, $"vật thể loại {kind}"), Bin.I32(row, step), Bin.I32(raw, state), place.X, place.Y, place.Z));
        }
        return things;
    }

    /// <summary>
    /// Côn trùng đang sống (sysInsectCollection._liveInsectList): mã, object điều khiển, ID loài (EncryptInt). Chỉ nhận con còn sống
    /// phía Unity (m_CachedPtr ≠ 0) - con trỏ cũ là văng game khi gọi hàm lên nó.
    /// </summary>
    public List<Insect> Insects()
    {
        var system = session.System("sysInsectCollection");
        var klass = session.Managed.ClassOf(system);
        if (klass == 0 || !session.Il2Cpp.HasField(klass, "_liveInsectList"))
        {
            if (system != 0) session.ForgetSystem("sysInsectCollection");
            _insectItems.Clear();
            return [];
        }
        var live = session.Managed.ListItems(session.Managed.Ptr(system, "_liveInsectList"));
        if (live.Count == 0)
        {
            _insectItems.Clear();
            return [];
        }
        _insectLayout ??= InsectFields.Select(f => session.Il2Cpp.Field(session.Managed.ClassOf(live[0]), f)).ToArray();
        var (idAt, uidAt, controlAt) = (_insectLayout[0], _insectLayout[1], _insectLayout[2]);
        var candidates = new List<((uint, uint, int) Key, long Control, int Item)>();
        foreach (var raw in session.Memory.ReadObjects(live, _insectLayout.Max() + 8).Values)
        {
            var key = (Bin.U32(raw, uidAt), Bin.U32(raw, idAt), Bin.I32(raw, idAt + 4));
            var control = Bin.U64(raw, controlAt);
            var item = _insectItems.TryGetValue(key, out var known) ? known : session.DecodeEncryptInt(key.Item2, key.Item3);
            if (Bin.IsPtr(control) && item is > 0 and < 1_000_000_000) candidates.Add((key, control, item));
        }
        var cached = session.Memory.ReadMany(candidates.Select(c => (c.Control + 0x10, 8)).ToList());
        _insectItems.Clear();
        var found = new List<Insect>();
        for (var i = 0; i < candidates.Count; i++)
        {
            if (cached[i] is not { } ptr || Bin.U64(ptr, 0) == 0) continue;
            _insectItems[candidates[i].Key] = candidates[i].Item;
            found.Add(new Insect(candidates[i].Key.Item1, candidates[i].Control, candidates[i].Item));
        }
        return found;
    }

    /// <summary>Vị trí điểm mọc (không đổi chỗ, nhớ lại); null nếu chưa đọc được.</summary>
    private Vec3? SpawnSpot(long spawn)
    {
        if (spawn == 0) return null;
        if (_spawnSpots.TryGetValue(spawn, out var spot)) return spot;
        var position = session.Optional(() => session.World.Position(spawn), "điểm mọc");
        if (position is { } p) _spawnSpots[spawn] = p;
        else L.Debug($"Chưa đọc được điểm mọc 0x{spawn:X}");
        return position;
    }
}
