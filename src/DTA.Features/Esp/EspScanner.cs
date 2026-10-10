using DTA.Features.Excavation;
using DTA.Game.Data;
using DTA.Game.Geometry;
using DTA.Game.Session;
using DTA.Runtime.Core;
using DTA.Runtime.Device;

namespace DTA.Features.Esp;

/// <summary>
/// Bộ quét ESP trên 1 tab (chỉ đọc, không bấm gì): luồng camera đọc camera + nhân vật ~60 lần / giây trên 1 phiên game RIÊNG (lượt quét dài
/// không làm giật nhãn); luồng quét đọc vật thể mỗi 1,5 s, vị trí côn trùng mỗi 0,3 s trên phiên dùng chung. Lớp vẽ hỏi <see cref="Look"/>:
/// camera nội suy giữa 2 lần đọc đúng thời điểm vẽ nên nhãn trượt đều theo khung hình game.
/// </summary>
public sealed class EspScanner(EmulatorDevice device, EspOptions options)
{
    private const double Frame = 0.016, ThingsEvery = 1.5, InsectsEvery = 0.3, ListEvery = 1, RetryDelay = 3, ViewCheck = 1;
    private const string HiddenSpot = "Điểm đào chưa lộ", GroundCard = "Gói thẻ dưới đất";
    private static readonly Logger L = Log.For("esp");
    private volatile bool _running;
    private (Look Old, Look New)? _looks;
    private double _pace = Frame;
    private Exception? _problem;

    /// <summary>Mọi vật đang theo dõi (thay nguyên cụm - luồng vẽ đọc lúc nào cũng trọn vẹn).</summary>
    public IReadOnlyList<Thing> Things { get; private set; } = [];
    public float Aspect { get; private set; } = 16f / 9;
    /// <summary>(tên, nhóm, cách mấy m, ghi chú) gần trước; {nhóm|loại: số lượng trên bản đồ}.</summary>
    public event Action<List<(string Kind, string Group, float Distance, string Note)>, Dictionary<string, int>>? Listed;
    public event Action<string, bool>? Status;

    private static double Now => Environment.TickCount64 / 1000.0;

    public void Start()
    {
        if (_running) return;
        _running = true;
        new Thread(Run) { IsBackground = true, Name = "EspScan" }.Start();
    }

    public void Stop() => _running = false;

    /// <summary>Camera + nhân vật để vẽ lúc <paramref name="now"/>: lùi đúng 1 nhịp đọc (+5 ms) để thời điểm vẽ luôn nằm giữa 2 lần đọc.</summary>
    public Look? LookAt(double now)
    {
        if (_looks is not var (old, fresh)) return null;
        var span = fresh.Time - old.Time;
        if (span <= 1e-4) return fresh;
        var render = now - Math.Max(0.016, _pace + 0.005);
        if (render >= fresh.Time) return fresh;
        return render <= old.Time ? old : EspMath.Blend(old, fresh, (float)((render - old.Time) / span));
    }

    public static double Clock => Now;

    /// <summary>Kết nối 2 phiên rồi quét tới khi tắt; lỗi (chưa mở game, chuyển cảnh) thì chờ rồi kết nối lại.</summary>
    private void Run()
    {
        while (_running)
        {
            GameSession? camera = null;
            try
            {
                Status?.Invoke("Đang kết nối bộ nhớ game...", false);
                var shared = SessionHub.Get(device, text => Status?.Invoke(text, false));
                camera = new GameSession(device);
                var size = device.ScreenSize();
                (shared.Screen, camera.Screen, Aspect) = (size, size, size.Width / (float)size.Height);
                _problem = null;
                var watcher = new Thread(() => Watch(camera)) { IsBackground = true, Name = "EspCamera" };
                watcher.Start();
                try
                {
                    Scan(shared);
                }
                finally
                {
                    _problem ??= new GameError("dừng quét");
                    watcher.Join(3000);
                }
            }
            catch (Exception e) when (e is DeviceError or GameError)
            {
                Status?.Invoke(e.Message, true);
                SleepWhileRunning(RetryDelay);
            }
            finally
            {
                (Things, _looks) = ([], null);
                camera?.Dispose();
            }
        }
    }

    private void SleepWhileRunning(double seconds)
    {
        var deadline = Now + seconds;
        while (_running && Now < deadline) Thread.Sleep(50);
    }

    /// <summary>Luồng camera: brain + nhân vật trong 1 lượt đọc; mỗi giây xác định lại theo đường đầy đủ (game đổi camera / nhân vật).</summary>
    private void Watch(GameSession s)
    {
        long brain = 0, character = 0;
        double viewed = 0;
        var fov = 60f;
        try
        {
            while (_running && _problem == null)
            {
                var started = Now;
                if (started - viewed > ViewCheck || brain == 0 || character == 0)
                {
                    viewed = started;
                    brain = s.Managed.Ptr(s.Camera.CurrentMap(), "cinemachineBrain");
                    character = s.Character();
                    fov = s.Camera.View()?.Fov ?? fov;
                }
                var poses = brain != 0 && character != 0 ? s.World.Poses([brain, character], (long)(ViewCheck * 1000)) : [];
                if (!poses.TryGetValue(brain, out var cam) || !poses.TryGetValue(character, out var me))
                {
                    if (!s.Alive()) throw new GameError("Game đã tắt", true);
                    (_looks, viewed) = (null, 0);
                    Thread.Sleep(300);
                    continue;
                }
                var look = new Look((started + Now) / 2, cam.Position, cam.Rotation, fov, me.Position);
                if (_looks is var (_, last)) _pace += (Math.Min(look.Time - last.Time, 0.5) - _pace) * 0.1;
                _looks = _looks is var (_, previous) ? (previous, look) : (look, look);
                var rest = Frame - (Now - started);
                if (rest > 0) Thread.Sleep(TimeSpan.FromSeconds(rest));
            }
        }
        catch (Exception e)
        {
            _problem ??= e;
        }
    }

    /// <summary>Luồng quét: vật đứng yên mỗi 1,5 s, côn trùng mỗi 0,3 s, báo danh sách mỗi giây.</summary>
    private void Scan(GameSession s)
    {
        var relics = new ExcavationGame(s);
        relics.LoadKinds();
        IReadOnlyList<Thing> fixedThings = [];
        IReadOnlyList<Thing> bugs = [];
        List<DTA.Game.Data.Insect> insects = [];
        double thingsAt = 0, insectsAt = 0, listed = 0, blind = 0;
        while (_running)
        {
            if (_problem != null) throw _problem as GameError ?? new GameError(_problem.Message, true);
            var started = Now;
            if (started - thingsAt > ThingsEvery)
            {
                thingsAt = started;
                fixedThings = [.. Relics(relics), .. MapThings(s)];
                insects = s.Map.Insects();
                Things = [.. fixedThings, .. bugs];
            }
            if (started - insectsAt > InsectsEvery)
            {
                insectsAt = started;
                var poses = s.World.Poses(insects.Select(i => i.Control));
                bugs = insects.Where(i => poses.ContainsKey(i.Control)).Select(i =>
                {
                    var info = s.Tables.Item(i.Item);
                    var card = IsCard(info.Name);
                    var p = poses[i.Control].Position;
                    return new Thing($"i{i.Uid}", card ? EspGroups.Card : EspGroups.Insect, info.Name.Length > 0 ? info.Name : $"Côn trùng {i.Item}", p.X, p.Y, p.Z, card ? "bay" : "", info.Grade);
                }).ToList();
                Things = [.. fixedThings, .. bugs];
            }
            if (_looks is not var (_, look))
            {
                blind = blind == 0 ? started : blind;
                if (started - blind > 2 && started - listed > ListEvery)
                {
                    listed = started;
                    Status?.Invoke("Chưa đọc được camera / vị trí nhân vật (game đang chuyển cảnh?)", true);
                }
            }
            else if (started - listed > ListEvery)
            {
                (listed, blind) = (started, 0);
                Report(look.Me);
            }
            Thread.Sleep(50);
        }
    }

    /// <summary>Báo danh sách (gần trước, tối đa 200) + số lượng từng loại.</summary>
    private void Report(Vec3 me)
    {
        var things = Things;
        var counts = things.GroupBy(t => EspOptions.HiddenKey(t.Group, t.Kind)).ToDictionary(g => g.Key, g => g.Count());
        var near = things.Select(t => (Distance: new Vec3(t.X, t.Y, t.Z).FlatDistance(me), Thing: t))
            .Where(p => options.Shows(p.Thing) && (options.Radius == 0 || p.Distance <= options.Radius)).OrderBy(p => p.Distance).ToList();
        Listed?.Invoke(near.Take(200).Select(p => (p.Thing.Kind, p.Thing.Group, p.Distance, p.Thing.Note)).ToList(), counts);
        Status?.Invoke($"Đang theo dõi {things.Count} vật thể, hiện {near.Count}", false);
    }

    private static bool IsCard(string name) => name.Contains("thẻ", StringComparison.OrdinalIgnoreCase) || name.Contains("card", StringComparison.OrdinalIgnoreCase);

    /// <summary>Điểm đào cổ vật / rương đảo.</summary>
    private static IEnumerable<Thing> Relics(ExcavationGame game) => game.Spots().Values.Select(spot =>
    {
        string name, note;
        if (game.IsIsland)
            (name, note) = (spot.Kind != 0 ? game.KindName(spot.Kind) : "Điểm kho báu", spot.Reward ? "chờ nhận quà" : spot.Kind != 0 ? $"loại {spot.Kind}" : "");
        else
            (name, note) = (spot.Kind != 0 ? game.KindName(spot.Kind) : HiddenSpot, spot.Kind == 0 ? "" : spot.Reward ? "chờ nhận quà" : $"còn {spot.Hp}/{spot.MaxHp}");
        return new Thing($"r{spot.Uid}", EspGroups.Relic, name, spot.X, spot.Y, spot.Z, note);
    });

    /// <summary>Đá, quặng, cây, nguyên liệu, gói thẻ dưới đất.</summary>
    private static IEnumerable<Thing> MapThings(GameSession s) => s.Map.Things().Where(t => !MapObjects.GoneStates.Contains(t.State)).Select(t =>
    {
        var (group, name) = t.Asset.Contains("cardcollect") ? (EspGroups.Card, GroundCard)
            : new[] { "treasure", "lost_island", "chest" }.Any(t.Asset.ToLowerInvariant().Contains) ? (EspGroups.Relic, t.Asset)
            : DTA.Game.Data.Things.Describe(t.Asset);
        return EspGroups.All.Contains(group) ? new Thing($"o{t.Uid}", group, name, t.X, t.Y, t.Z, t.Step > 1 ? $"bước {t.Step}" : "") : null;
    }).OfType<Thing>();
}
