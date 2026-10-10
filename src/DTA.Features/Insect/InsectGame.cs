using DTA.Game.Session;
using DTA.Runtime.Core;

namespace DTA.Features.Insect;

/// <summary>
/// Đọc côn trùng đang sống kèm mức cảnh giác + trạng thái vợt. Cách game cho 1 con phát hiện người chơi (ngưỡng ở InsectMoveSetting): ngoài
/// vòng DetectRadius chạy nhanh mấy cũng được; trong vòng đi ≥ SensitiveSpd thì nó nghi; đang nghi mà còn đi nhanh hơn SensitiveDetectSpd thì
/// nó bỏ chạy. Đứng im thì hết nghi.
/// </summary>
public sealed class InsectGame(GameSession session)
{
    private static readonly string[] BugFields = ["_state", "_senseState", "_moveSetting", "m_CachedPtr"];
    private static readonly string[] SettingFields = ["DetectRadius", "DefalutDetectSpd", "SensitiveSpd", "SensitiveDetectSpd"];
    private int[]? _bugLayout, _settingLayout;
    private readonly Dictionary<long, float[]> _settings = [];

    public GameSession Session { get; } = session;

    /// <summary>Quên ngưỡng cảnh giác theo cảnh (đổi bản đồ).</summary>
    public void SceneChanged() => _settings.Clear();

    /// <summary>Trạng thái vợt (1 lượt đọc).</summary>
    public NetState State()
    {
        if (Session.ControlClass == 0) Session.LocateActor();
        if (!Session.Il2Cpp.Is(Session.ControlClass, "ActorDefaultControl")) throw new GameError("Nhân vật đang ở trạng thái không bắt bọ được (đang lái xe?)");
        return (NetState)Session.ControlValue(Session.Il2Cpp.Field(Session.ControlClass, "_insectState"));
    }

    /// <summary>Mọi con đang sống + vị trí + mức cảnh giác lúc này (theo lô: vài lượt đọc cho cả bản đồ).</summary>
    public List<Bug> Bugs()
    {
        var insects = Session.Map.Insects();
        if (insects.Count == 0) return [];
        _bugLayout ??= BugFields.Select(f => Session.Il2Cpp.Field(Session.Managed.ClassOf(insects[0].Control), f)).ToArray();
        var data = Session.Memory.ReadObjects(insects.Select(i => i.Control), _bugLayout.Max() + 8);
        LoadSettings(data.Values.Select(r => Bin.U64(r, _bugLayout[2])));
        var poses = Session.World.Poses(insects.Select(i => i.Control));
        return insects.Where(i => data.ContainsKey(i.Control) && poses.ContainsKey(i.Control))
            .Select(i => Build((int)i.Uid, i.Control, i.Item, data[i.Control], poses[i.Control].Position)).ToList();
    }

    /// <summary>Đọc lại 1 con (2 lượt đọc); null nếu đã bị huỷ / đang rời đi.</summary>
    public Bug? Reload(Bug bug) => _bugLayout == null ? null : Session.Optional(() =>
    {
        var raw = Session.Memory.Read(bug.Ref, _bugLayout.Max() + 8);
        if (raw == null || Bin.U64(raw, _bugLayout[3]) == 0) return null;
        if (!Session.World.Poses([bug.Ref]).TryGetValue(bug.Ref, out var pose)) return null;
        var fresh = Build(bug.Uid, bug.Ref, bug.Item, raw, pose.Position);
        return fresh.State < Bug.Leaving ? fresh : null;
    }, "côn trùng");

    /// <summary>Ngưỡng cảnh giác từ InsectMoveSetting (đọc 1 lần / object); số hỏng thì coi như không cảnh giác.</summary>
    private void LoadSettings(IEnumerable<long> settings)
    {
        var fresh = settings.Where(s => s != 0 && !_settings.ContainsKey(s)).Distinct().ToList();
        if (fresh.Count == 0) return;
        _settingLayout ??= SettingFields.Select(f => Session.Il2Cpp.Field(Session.Managed.ClassOf(fresh[0]), f)).ToArray();
        foreach (var (setting, raw) in Session.Memory.ReadObjects(fresh, _settingLayout.Max() + 4))
        {
            var values = _settingLayout.Select(o => Bin.F32(raw, o)).ToArray();
            _settings[setting] = values.All(v => float.IsFinite(v) && v is >= 0 and < 1000) ? values : new float[4];
        }
    }

    /// <summary>Dựng Bug: đi chậm hơn cả 2 ngưỡng (gây nghi, phát hiện) thì nó không biết gì; ngưỡng 0 = không có ngưỡng đó.</summary>
    private Bug Build(int uid, long control, int item, byte[] raw, DTA.Game.Geometry.Vec3 p)
    {
        var l = _bugLayout!;
        var s = _settings.GetValueOrDefault(Bin.U64(raw, l[2])) ?? new float[4];
        var info = Session.Tables.Item(item);
        var speeds = new[] { s[2], s[1] }.Where(v => v > 0).ToList();
        var name = info.Name.Length > 0 ? info.Name : $"Côn trùng {item}";
        return new Bug(uid, control, item, name, info.Grade, BugKinds.Of(item, info.Name), p.X, p.Y, p.Z, Bin.I32(raw, l[0]),
            (SenseState)Bin.I32(raw, l[1]), speeds.Count > 0 ? s[0] : 0, speeds.Count > 0 ? speeds.Min() : 0, s[3]);
    }
}
