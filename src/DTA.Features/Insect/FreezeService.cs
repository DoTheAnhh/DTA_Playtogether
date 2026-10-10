using DTA.Engine.Core;
using DTA.Game.Session;
using DTA.Runtime.Core;

namespace DTA.Features.Insect;

/// <summary>
/// Đóng băng bọ / chim / thẻ bay / hộp bay (cần xác nhận rủi ro): mọi con hiện có + con mới xuất hiện đứng im, không cảnh giác (tốc độ 0,
/// ngưỡng gây nghi 999, _state 2 / _senseState 0; giữ vòng phát hiện để vợt vẫn trúng). Con đang rời đi / vừa bị bắt thì thả để game cho nó biến mất. Tắt thì trả nguyên thông số.
/// Ghi theo tên field (dump), mỗi con 1 lệnh ghi gộp - không cần hook. 1 dịch vụ / tab.
/// </summary>
public sealed class FreezeService
{
    private const int Leaving = 4;
    /// <summary>Ngưỡng tốc độ gây nghi không bao giờ chạm tới (999 m/s, float LE).</summary>
    private static readonly byte[] Never = BitConverter.GetBytes(999f);
    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(120);
    private static readonly Logger L = Log.For("insect");
    private static readonly Dictionary<string, FreezeService> Services = [];

    /// <summary>Thông số gốc 1 InsectMoveSetting (nhiều con dùng chung) + số con đang dùng.</summary>
    private sealed record Original(byte[] Speeds, byte[] Accel, byte[] Factor, byte[] Detect)
    {
        public int Refs { get; set; } = 1;
    }

    private sealed record Frozen(long Setting, byte[] State, byte[] Sense);

    private readonly string _serial;
    private readonly object _lock = new();
    private readonly Dictionary<long, Frozen> _frozen = [];
    private readonly Dictionary<long, Original> _originals = [];
    /// <summary>Con đang bị vung vợt -> tạm không ép trạng thái (tới mốc giờ) để game xử lý bắt / hụt.</summary>
    private readonly Dictionary<long, DateTime> _spared = [];
    private CancellationTokenSource? _monitor;
    private Fields? _fields;

    private FreezeService(string serial)
    {
        _serial = serial;
        AuthSession.OnStop(_ => SetEnabled(false));
    }

    /// <summary>Dịch vụ của tab.</summary>
    public static FreezeService For(string serial)
    {
        lock (Services)
        {
            if (!Services.TryGetValue(serial, out var service)) Services[serial] = service = new FreezeService(serial);
            return service;
        }
    }

    public bool Enabled { get; private set; }

    /// <summary>Bật / tắt; bật cần đã xác nhận rủi ro. Tắt thì trả nguyên mọi con ở luồng nền.</summary>
    public bool SetEnabled(bool enabled)
    {
        lock (_lock)
        {
            if (Enabled == enabled) return true;
            if (enabled && !Risk.IsAccepted(RiskFeature.FreezeBugs))
            {
                L.Warn("Đóng băng bọ chưa được xác nhận rủi ro");
                return false;
            }
            Enabled = enabled;
            _monitor?.Cancel();
            if (enabled)
            {
                var stop = _monitor = new CancellationTokenSource();
                new Thread(() => Monitor(stop.Token)) { IsBackground = true, Name = "BugFreeze" }.Start();
            }
            else ThreadPool.QueueUserWorkItem(_ => UnfreezeAll());
            L.Info(enabled ? "Bật đóng băng bọ & thẻ bay" : "Tắt đóng băng bọ & thẻ bay");
            return true;
        }
    }

    /// <summary>Offset field theo tên (đọc 1 lần): InsectController + InsectMoveSetting.</summary>
    private sealed record Fields(int Setting, int ForceEscape, int State, int Sense, int CurSpd, int Accel, int Factor, int Detect, int DetectEnd, int[] Senses);

    private Fields Layout(GameSession s, long control)
    {
        if (_fields != null) return _fields;
        var il2cpp = s.Il2Cpp;
        var klass = s.Managed.ClassOf(control);
        var setting = s.Managed.Ptr(control, "_moveSetting");
        var sk = s.Managed.ClassOf(setting);
        return _fields = new Fields(il2cpp.Field(klass, "_moveSetting"), il2cpp.Field(klass, "_forceEscape"),
            il2cpp.Field(klass, "_state"), il2cpp.Field(klass, "_senseState"), il2cpp.Field(sk, "CurSpd"), il2cpp.Field(sk, "AccelatorSpd"),
            il2cpp.Field(sk, "SpdFactor"), il2cpp.Field(sk, "DetectRadius"), il2cpp.Field(sk, "SensitiveDetectSpd") + 4,
            [il2cpp.Field(sk, "DefalutDetectSpd"), il2cpp.Field(sk, "SensitiveSpd"), il2cpp.Field(sk, "SensitiveDetectSpd")]);
    }

    /// <summary>
    /// Ô ghi cho 1 con đứng im + không cảnh giác: tốc độ (CurSpd/DefaultSpd/EscapeSpd), gia tốc, hệ số = 0; 3 ngưỡng tốc độ gây nghi = 999
    /// (người chơi đi nhanh mấy cũng dưới ngưỡng). GIỮ NGUYÊN DetectRadius + _isDetect: game dùng chúng khi xét vợt trúng - ghi 0 là vung
    /// trúng mấy cũng hụt; ngưỡng = 0 thì nhúc nhích là nó nghi.
    /// </summary>
    private static List<(long, byte[])> CalmWrites(Fields f, long control, long setting)
    {
        var writes = new List<(long, byte[])>();
        if (Bin.IsPtr(setting))
        {
            writes.AddRange([(setting + f.CurSpd, new byte[12]), (setting + f.Accel, new byte[4]), (setting + f.Factor, new byte[4])]);
            writes.AddRange(f.Senses.Select(o => (setting + o, Never)));
        }
        writes.AddRange([(control + f.ForceEscape, new byte[1]), (control + f.State, [2, 0, 0, 0]), (control + f.Sense, new byte[4])]);
        return writes;
    }

    /// <summary>Đóng băng 1 con (nhớ thông số gốc để trả); đã đóng thì ép lại.</summary>
    public bool Freeze(long control)
    {
        if (SessionHub.Peek(_serial) is not { } s || !Bin.IsPtr(control)) return false;
        lock (_lock)
        {
            if (_frozen.ContainsKey(control)) return Refreeze(control);
            return s.Optional(() =>
            {
                if (s.Memory.U64(control + 0x10) == 0) return false;
                var f = Layout(s, control);
                var setting = (long)s.Memory.U64(control + f.Setting);
                if (!Bin.IsPtr(setting)) return false;
                var raw = s.Memory.ReadMany([(setting, f.DetectEnd), (control + f.State, 4), (control + f.Sense, 4)]);
                if (raw is not [{ } set, { } state, { } sense] || Bin.I32(state, 0) >= Leaving) return false;
                if (_originals.TryGetValue(setting, out var original)) original.Refs++;
                else _originals[setting] = new Original(set[f.CurSpd..(f.CurSpd + 12)], set[f.Accel..(f.Accel + 4)], set[f.Factor..(f.Factor + 4)], set[f.Detect..f.DetectEnd]);
                s.Memory.WriteMany(CalmWrites(f, control, setting));
                _frozen[control] = new Frozen(setting, state, sense);
                return true;
            }, "đóng băng");
        }
    }

    /// <summary>
    /// Tạm thôi ép trạng thái 1 con trong <paramref name="time"/> (đang vung vợt vào nó): ép _state 2 liên tục thì game không chuyển được
    /// sang bị bắt -> lượt vung treo tới hết giờ rồi hụt. Thông số tốc độ / vòng phát hiện vẫn 0 nên nó vẫn không chạy được.
    /// </summary>
    public void Spare(long control, TimeSpan time)
    {
        lock (_lock) _spared[control] = DateTime.UtcNow + time;
    }

    /// <summary>Con đang được tạm tha (đang vung vợt vào nó).</summary>
    private bool Spared(long control)
    {
        lock (_lock)
        {
            if (!_spared.TryGetValue(control, out var until)) return false;
            if (DateTime.UtcNow < until) return true;
            _spared.Remove(control);
            return false;
        }
    }

    /// <summary>Ép lại đứng im (game vừa cho nó di chuyển / cảnh giác); đang rời đi thì thả; đang tạm tha thì thôi.</summary>
    public bool Refreeze(long control)
    {
        if (Spared(control)) return false;
        if (!Enabled || SessionHub.Peek(_serial) is not { } s || _fields is not { } f) return false;
        return s.Optional(() =>
        {
            var raw = s.Memory.ReadMany([(control + 0x10, 8), (control + f.State, 4), (control + f.Setting, 8)]);
            if (raw is not [{ } alive, { } state, { } setting] || Bin.U64(alive, 0) == 0) return false;
            if (Bin.I32(state, 0) >= Leaving)
            {
                Release(control);
                return false;
            }
            s.Memory.WriteMany(CalmWrites(f, control, Bin.U64(setting, 0)));
            return true;
        }, "ép đóng băng");
    }

    /// <summary>Thôi theo dõi 1 con (vừa bị bắt / biến mất) mà KHÔNG ghi gì, để game xử lý tiếp.</summary>
    public void Release(long control)
    {
        lock (_lock)
        {
            if (!_frozen.Remove(control, out var entity) || !_originals.TryGetValue(entity.Setting, out var original)) return;
            if (--original.Refs <= 0) _originals.Remove(entity.Setting);
        }
    }

    /// <summary>Trả nguyên mọi con: thông số chung trả khi con cuối dùng nó được thả; trạng thái AI + cảnh giác trả từng con.</summary>
    private void UnfreezeAll()
    {
        lock (_lock)
        {
            if (SessionHub.Peek(_serial) is { } s && _fields is { } f)
            {
                var writes = new List<(long, byte[])>();
                foreach (var (setting, o) in _originals)
                    writes.AddRange([(setting + f.CurSpd, o.Speeds), (setting + f.Accel, o.Accel), (setting + f.Factor, o.Factor), (setting + f.Detect, o.Detect)]);
                foreach (var (control, entity) in _frozen)
                    if (s.Memory.U64(control + 0x10) != 0) writes.AddRange([(control + f.State, entity.State), (control + f.Sense, entity.Sense)]);
                s.Optional(() => s.Memory.WriteMany(writes), "trả đóng băng");
            }
            _frozen.Clear();
            _originals.Clear();
            _spared.Clear();
        }
    }

    /// <summary>
    /// Vòng giám sát (~120 ms): đóng băng con mới; con đã đóng mà bị game cho di chuyển / cảnh giác thì ép lại; con đang rời đi thì thả;
    /// đổi bản đồ thì quên con trỏ cũ.
    /// </summary>
    private void Monitor(CancellationToken stop)
    {
        int? lastMap = null;
        while (!stop.IsCancellationRequested)
        {
            try
            {
                if (SessionHub.Peek(_serial) is { } s)
                {
                    var map = s.Camera.Map()?.Id;
                    if (map != null && lastMap != null && map != lastMap)
                        lock (_lock)
                        {
                            _frozen.Clear();
                            _originals.Clear();
                            _spared.Clear();
                        }
                    lastMap = map ?? lastMap;
                    Sweep(s);
                }
            }
            catch (Exception e)
            {
                L.Debug($"Giám sát đóng băng lỗi: {e.Message}");
            }
            stop.WaitHandle.WaitOne(Tick);
        }
    }

    /// <summary>1 lượt soát mọi con: trạng thái cả loạt trong 1 lượt đọc, mọi con cần ép lại gộp 1 lượt ghi (không chiếm kênh bộ nhớ của bot).</summary>
    private void Sweep(GameSession s)
    {
        var insects = s.Map.Insects();
        if (insects.Count == 0) return;
        var f = Layout(s, insects[0].Control);
        var states = s.Memory.ReadMany(insects.Select(i => (i.Control + f.State, 8)).ToList());
        var writes = new List<(long, byte[])>();
        for (var k = 0; k < insects.Count; k++)
        {
            var control = insects[k].Control;
            var (state, sense) = states[k] is { } raw ? (Bin.I32(raw, 0), Bin.I32(raw, 4)) : (0, 0);
            Frozen? entity;
            lock (_lock) _frozen.TryGetValue(control, out entity);
            if (state >= Leaving) Release(control);
            else if (Spared(control)) continue;
            else if (entity == null) Freeze(control);
            else if (state == 3 || sense > 0) writes.AddRange(CalmWrites(f, control, entity.Setting));
        }
        if (writes.Count > 0) s.Memory.WriteMany(writes);
        var alive = insects.Select(i => i.Control).ToHashSet();
        List<long> gone;
        lock (_lock) gone = _frozen.Keys.Where(c => !alive.Contains(c)).ToList();
        foreach (var control in gone) Release(control);
    }
}
