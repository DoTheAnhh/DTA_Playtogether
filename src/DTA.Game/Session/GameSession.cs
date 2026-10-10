using System.Collections.Concurrent;
using DTA.Runtime.Core;
using DTA.Runtime.Device;
using DTA.Runtime.Il2Cpp;
using DTA.Runtime.Memory;
using DTA.Runtime.Storage;

namespace DTA.Game.Session;

/// <summary>
/// 1 phiên game / tab giả lập, DÙNG CHUNG cho mọi chức năng (bot, luồng nền, ESP...): 1 phiên su, 1 bộ giải mã il2cpp, 1 bộ đệm.
/// Lấy qua <see cref="SessionHub"/>; game tắt / đổi pid thì phiên bị đóng và tạo phiên mới.
/// </summary>
public sealed partial class GameSession : IDisposable
{
    public static readonly string[] RequiredClasses = ["ActorSystem", "PT_Encrypt.EncryptSystem"];
    private const string FrameworkClass = "FrameWork";
    private const string AbsentKey = "absent-classes";
    private static readonly Logger L = Log.For("session");

    private readonly ConcurrentDictionary<string, long> _classes = new();
    private readonly ConcurrentDictionary<string, long> _systems = new();
    private long _framework, _actorSystemSlot, _storageDicSlot;
    private int _myActorOffset;

    public EmulatorDevice Device { get; }
    public RootShell Shell { get; }
    public GameProcess Process { get; }
    public MemoryChannel Memory { get; }
    public Il2CppResolver Il2Cpp { get; }
    public ManagedReader Managed { get; }
    public VersionCache Cache { get; }
    /// <summary>Dữ liệu dùng chung theo phiên (pid): bố cục native, đường vị trí, lưới mặt đất... - mọi reader dùng chung.</summary>
    public ConcurrentDictionary<string, object> Shared { get; } = new();
    /// <summary>Cỡ màn hình giả lập (điểm ảnh).</summary>
    public (int Width, int Height) Screen { get; set; } = (EmulatorDevice.BaseWidth, EmulatorDevice.BaseHeight);

    /// <summary>Object điều khiển nhân vật (ActorDefaultControlPlayer...) và class của nó.</summary>
    public long Control { get; private set; }
    public long ControlClass { get; private set; }

    /// <summary>Nhân vật sang bản đồ khác / đổi kiểu điều khiển.</summary>
    public event Action? SceneChanged;
    public event Action<long>? ControlChanged;

    /// <summary>Kết nối vào game trên tab: mở phiên su, tìm tiến trình, tìm class (lần đầu mỗi phiên bản mới phải dò).</summary>
    public GameSession(EmulatorDevice device, Action<string>? progress = null)
    {
        Device = device;
        Shell = new RootShell(device);
        try
        {
            Process = GameProcess.Find(Shell);
            Memory = new MemoryChannel(Shell, Process.Pid);
            Cache = VersionCache.For(Process.Package, Process.VersionCode);
            Il2Cpp = new Il2CppResolver(Memory, Cache.Data.Il2Cpp);
            Managed = new ManagedReader(Memory, Il2Cpp);
            var wanted = RequiredClasses.Append(FrameworkClass).ToList();
            var scan = RequiredClasses.Any(n => !Cache.Data.Il2Cpp.Slots.ContainsKey(n));
            foreach (var (name, klass) in ClassScanner.FindClasses(Il2Cpp, device, wanted, scan, progress ?? (_ => { }))) _classes[name] = klass;
            SaveCache();
            if (RequiredClasses.Any(n => !_classes.ContainsKey(n))) throw new GameError("Game chưa vào tới màn chơi", true);
            BindActorSystem();
            LocateActor();
            L.Info($"Đã kết nối {device.Serial}: pid {Process.Pid}");
        }
        catch
        {
            Shell.Dispose();
            throw;
        }
    }

    public bool Alive() => Memory.Alive();

    /// <summary>Chạy 1 bước đọc phụ: game đổi cấu trúc ở bước này thì trả mặc định thay vì làm hỏng việc chính (mất kết nối vẫn ném).</summary>
    public T? Optional<T>(Func<T?> read, string what = "")
    {
        try
        {
            return read();
        }
        catch (GameError e) when (!e.Retry)
        {
            L.Debug($"Bỏ qua {what}: {e.Message}");
            return default;
        }
    }

    /// <summary>Class theo tên; chưa biết thì dò 1 lần / phiên bản game. 0 nếu game không có.</summary>
    public long Class(string name)
    {
        if (_classes.TryGetValue(name, out var klass)) return klass;
        var absent = Cache.Get<HashSet<string>>(AbsentKey) ?? [];
        if (absent.Contains(name)) return 0;
        var scanned = !Cache.Data.Il2Cpp.Slots.ContainsKey(name);
        foreach (var (n, k) in ClassScanner.FindClasses(Il2Cpp, Device, [name], true, _ => { })) _classes[n] = k;
        if (scanned && !_classes.ContainsKey(name))
        {
            absent.Add(name);
            Cache.Put(AbsentKey, absent);
            L.Warn($"Game không có class {name}");
        }
        SaveCache();
        return _classes.GetValueOrDefault(name);
    }

    /// <summary>Lưu bộ đệm phiên bản nếu vừa dò thêm.</summary>
    public void SaveCache()
    {
        if (Il2Cpp.Changed) { Cache.Dirty = true; Il2Cpp.Changed = false; }
        Cache.Save();
    }

    /// <summary>Báo đổi bản đồ: bỏ mọi thứ nhớ theo object của cảnh cũ.</summary>
    public void NotifySceneChanged()
    {
        foreach (var key in Shared.Keys.Where(k => k.StartsWith("scene:"))) Shared.TryRemove(key, out _);
        Control = ControlClass = 0;
        _ui?.ResetHud();
        L.Info("Đổi cảnh: xoá dữ liệu theo cảnh cũ");
        SceneChanged?.Invoke();
    }

    /// <summary>Lấy (hoặc tạo) dữ liệu dùng chung theo phiên; khoá bắt đầu "scene:" bị xoá khi đổi cảnh.</summary>
    public T Get<T>(string key, Func<T> create) where T : notnull => (T)Shared.GetOrAdd(key, _ => create());

    public void Dispose()
    {
        SaveCache();
        Shell.Dispose();
        L.Info($"Đóng phiên {Device.Serial}");
    }
}
