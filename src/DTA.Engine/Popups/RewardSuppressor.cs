using DTA.Game.Actions;
using DTA.Game.Session;
using DTA.Runtime.Core;

namespace DTA.Engine.Popups;

/// <summary>
/// Đóng nhanh nhất các màn nhận thưởng CHIẾM TOÀN MÀN HÌNH ngay khi game mở (giữ nguyên hình game, chỉ bỏ hoạt ảnh đóng):
/// DialogResultGetItemView (lật đồ / mở hộp mọi độ hiếm), DialogResultGetItemList, DialogRewardPopup (nếu toàn màn hình - bảng nhỏ
/// dạng thẻ để luồng thường xử lý). Mỗi nhịp 1 lượt đọc gộp 3 ô giữ con trỏ bảng; mỗi 0,3 s quét thêm bảng CÙNG LOẠI màn nhận đồ.
/// Tạo mới khi kết nối / đổi bản đồ.
/// </summary>
public sealed class RewardSuppressor(GameSession session)
{
    private const int Kinds = 3;
    private static readonly TimeSpan Retry = TimeSpan.FromSeconds(2), Scan = TimeSpan.FromSeconds(0.3), CloseOnce = TimeSpan.FromSeconds(5);
    private static readonly string[] FlagFields = ["m_IsVisible", "m_IsHIde", "IsOffScreen", "CloseAniSkip"];
    private static readonly Logger L = Log.For("popup");

    private readonly List<(long Holder, string Kind)> _holders = [];
    private readonly HashSet<int> _viewTypes = [];
    private readonly Dictionary<long, DateTime> _closed = [];
    private int[]? _flags;
    private long _fullSet, _instances;
    private DateTime _prepared = DateTime.MinValue, _scanned = DateTime.MinValue;

    private DialogRouter Router => session.Dialogs;

    /// <summary>1 nhịp canh (gọi ~30 ms / lần).</summary>
    public void Run()
    {
        if (_holders.Count < Kinds && DateTime.UtcNow - _prepared >= Retry) Prepare();
        if (_holders.Count == 0 || _flags == null) return;
        if (DateTime.UtcNow - _scanned >= Scan)
        {
            _scanned = DateTime.UtcNow;
            ScanSameType();
        }
        var memory = session.Memory;
        var dialogs = memory.ReadMany(_holders.Select(h => (h.Holder, 8)).ToList())
            .Select((raw, i) => (Addr: raw != null ? Bin.U64(raw, 0) : 0, _holders[i].Kind)).Where(d => d.Addr != 0).ToList();
        if (dialogs.Count == 0) return;
        var size = _flags.Max() + 1;
        var states = memory.ReadMany(dialogs.Select(d => (d.Addr, size)).ToList());
        for (var i = 0; i < dialogs.Count; i++)
        {
            var (addr, kind) = dialogs[i];
            if (states[i] is not { } raw || raw[_flags[0]] == 0 || raw[_flags[1]] != 0) continue;
            if (kind == "view")
            {
                if (_viewTypes.Count == 0 && DialogType(addr) is { } type) _viewTypes.Add(type);
            }
            else if (kind == "reward" && !FullScreen(addr)) continue;
            Router.Handle(addr);
        }
    }

    /// <summary>Dò ô Self của 2 class màn nhận đồ + sysDialog.globalRewardPopup (Self chỉ có sau lần đầu game mở bảng đó).</summary>
    private void Prepare()
    {
        _prepared = DateTime.UtcNow;
        _holders.Clear();
        var il2cpp = session.Il2Cpp;
        foreach (var (name, kind) in new[] { ("DialogResultGetItemView", "view"), ("DialogResultGetItemList", "list") })
        {
            var klass = session.Class(name);
            if (klass == 0) continue;
            _flags ??= FlagFields.Select(f => il2cpp.Field(klass, f)).ToArray();
            if (session.Optional(() => il2cpp.StaticFieldPtr(klass, "Self"), name) is var slot and not 0) _holders.Add((slot, kind));
        }
        var dialogs = session.System("sysDialog");
        if (dialogs == 0) return;
        var dialogClass = session.Managed.ClassOf(dialogs);
        _holders.Add((dialogs + il2cpp.Field(dialogClass, "globalRewardPopup"), "reward"));
        _fullSet = dialogs + il2cpp.Field(dialogClass, "_fullScreenDialogSet");
        _instances = dialogs + il2cpp.Field(dialogClass, "_instanceDialogs");
        if (_flags != null) return;
        var any = session.Managed.DictItems((long)session.Memory.U64(_instances)).Select(p => p.Value).FirstOrDefault(v => v != 0);
        var anyClass = session.Managed.ClassOf(any);
        if (anyClass != 0 && il2cpp.Names(anyClass).Contains("DialogUnit")) _flags = FlagFields.Select(f => il2cpp.Field(anyClass, f)).ToArray();
        L.Debug($"Canh màn thưởng: {_holders.Count}/{Kinds} loại");
    }

    /// <summary>Bảng chiếm toàn màn hình: m_DialogInfo.m_IsFullScreenUI, hoặc loại bảng nằm trong sysDialog._fullScreenDialogSet.</summary>
    private bool FullScreen(long dialog) => session.Optional(() =>
    {
        var info = session.Managed.Ptr(dialog, "m_DialogInfo");
        var klass = session.Managed.ClassOf(info);
        if (klass == 0) return false;
        if (session.Memory.Read(info + session.Il2Cpp.Field(klass, "m_IsFullScreenUI"), 1) is [1]) return true;
        var type = session.Managed.I32(info, "m_DialogType");
        return type is { } t && session.Managed.IntSet((long)session.Memory.U64(_fullSet)).Contains(t);
    }, "bảng toàn màn hình");

    /// <summary>m_DialogInfo.m_DialogType của bảng; null nếu không đọc được.</summary>
    private int? DialogType(long dialog) => session.Optional(() => session.Managed.I32(session.Managed.Ptr(dialog, "m_DialogInfo"), "m_DialogType"), "loại bảng");

    /// <summary>Đóng mọi bảng đang hiện cùng loại màn "Nhấn vào màn hình để..." (khác class thì đóng chung đúng 1 lần).</summary>
    private void ScanSameType()
    {
        if (_instances == 0 || _flags == null) return;
        var dialogs = session.Managed.DictItems((long)session.Memory.U64(_instances)).Select(p => p.Value).Where(Bin.IsPtr).ToList();
        var data = dialogs.Count > 0 ? session.Memory.ReadObjects(dialogs, _flags.Max() + 1) : [];
        var now = DateTime.UtcNow;
        foreach (var stale in _closed.Where(p => now - p.Value >= CloseOnce).Select(p => p.Key).ToList()) _closed.Remove(stale);
        foreach (var (addr, raw) in data)
        {
            if (raw[_flags[0]] == 0 || raw[_flags[1]] != 0) continue;
            var isView = session.Il2Cpp.Names(Bin.U64(raw, 0)).Any(n => n.Contains("DialogResultGetItemView"));
            if (!isView && (_viewTypes.Count == 0 || DialogType(addr) is not { } type || !_viewTypes.Contains(type))) continue;
            if (isView)
            {
                Router.Handle(addr);
                continue;
            }
            if (_closed.ContainsKey(addr)) continue;
            _closed[addr] = now;
            Router.Context.SkipCloseAnimation(addr);
            Router.Context.CloseBack(addr);
        }
    }
}
