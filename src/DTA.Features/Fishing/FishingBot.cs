using DTA.Engine.Bots;
using DTA.Engine.Core;
using DTA.Game.Actions;
using DTA.Game.Data;
using DTA.Game.Ui;
using DTA.Runtime.Device;

namespace DTA.Features.Fishing;

/// <summary>1 con vừa câu: lúc, ID cá, cỡ bóng, nền, ID món, tên, cỡ cm, giá, giữ / bán.</summary>
public sealed record CatchRecord(DateTime Time, int FishId, int Shadow, int Grade, int ItemId, string Name, int Size, string Price, string Action);

/// <summary>Sự kiện riêng của bot câu: ID cá lượt đang câu (null = vừa quăng lượt mới), con vừa câu.</summary>
public sealed class FishingEvents
{
    public Action<int?> Fish { get; init; } = _ => { };
    public Action<CatchRecord> Catch { get; init; } = _ => { };
}

/// <summary>
/// Câu cá tự động: mọi quyết định theo trạng thái đọc bộ nhớ, mọi hành động là hàm game (quăng, giật, giữ / bán) - không chạm màn hình.
/// Lọc cá: bóng hiện mà ID không khớp thì thu cần ngay, quăng lượt mới. Cá lớn: giật khi cá kéo phao / đang choáng.
/// </summary>
public sealed partial class FishingBot(EmulatorDevice device, BotEvents events, FishingEvents fishing, FishingOptions options, FishCatalog catalog)
    : Bot(device, events)
{
    protected override string Channel => "fishing";
    private const int CastAttempts = 15;
    private const double RecastDelay = 0.05;
    private const string RodBroken = "Cần câu đã hỏng - bật Tự sửa cần hoặc sửa trong game";
    private FishingGame _game = null!;
    private double _resultClosed;
    /// <summary>Số lần liền quăng mà phao không xuống được nước (chỗ cạn / trên cạn).</summary>
    private int _dryCasts;
    private const int DryCastLimit = 3;

    protected override string ToolName => "cần câu";
    protected override string ToolClass => Tools.Rod;
    private FishingAssist Assist => FishingAssist.For(Device.Serial);
    private static double Now => Environment.TickCount64 / 1000.0;

    protected override void Prepare()
    {
        _game = new FishingGame(Session);
        Session.SceneChanged -= _game.SceneChanged;
        Session.SceneChanged += _game.SceneChanged;
        catalog.GameShadows = _game.Shadows();
        _game.LoadGrades(catalog.Names.Keys, catalog.ItemGrades);
        Session.Held.Read(Tools.Rod);
    }

    /// <summary>Câu hết lượt này tới lượt khác; tắt thì trả ID vùng câu gốc + nhả POV.</summary>
    protected override string? Work()
    {
        Assist.SetBotActive(true);
        _dryCasts = 0;
        try
        {
            var caught = false;
            while (Running)
            {
                if (!AuthSession.Active) return "Key đã hết hạn hoặc phiên làm việc đã bị hủy";
                if (Session.Control == 0) Session.LocateActor();
                if (Session.Open.Top() is { Addr: not 0 } top) Session.Dialogs.Handle(top.Addr);
                var ownCast = _game.Poll().State == FishStates.Idle;
                if (ownCast)
                {
                    fishing.Fish(null);
                    if (!caught) Events.Status("Đang quăng cần...", Level.Info);
                    Assist.CastStarted(_game);
                    BeforeCast();
                    var error = Cast();
                    if (error != null || !Running) return error;
                    Assist.Step(_game, true);
                    AfterCast();
                }
                caught = Follow(ownCast);
                if (_dryCasts >= DryCastLimit) return "Không thả được phao xuống nước (nước cạn hoặc đang đứng trên cạn) - đứng sát mép nước sâu hơn rồi bật lại";
            }
            return null;
        }
        finally
        {
            _game.RestoreZones();
            Assist.CastEnded(_game, restoreFloat: true);
            Assist.SetBotActive(false);
        }
    }

    /// <summary>Đọc trạng thái liên tục tới khi <paramref name="done"/>; false nếu hết giờ / bị tắt.</summary>
    private bool WaitFor(Func<FishState, bool> done, double timeout)
    {
        var deadline = Now + timeout;
        while (Running && Now < deadline)
        {
            if (done(_game.Poll())) return true;
            Thread.Sleep(10);
        }
        return false;
    }

    /// <summary>
    /// Quăng cần tới khi game sang trạng thái câu. Bảng sửa: sửa (nếu bật); bảng thường (hỏi, thưởng, thông báo, nhận đồ): xử lý; cần hỏng thì
    /// game mở bảng sửa thay vì quăng. 15 lần không ăn thì dừng.
    /// </summary>
    private string? Cast()
    {
        var held = Session.Held.Read(Tools.Rod);
        if (held.Holding == false) return NotEquipped();
        var rod = held.Uses;
        Events.Tool(rod);
        var casts = 0;
        while (Running)
        {
            var top = Session.Open.Top();
            if (top.Is(DialogReader.Repair))
            {
                if (!options.AutoRepair) return RodBroken;
                var error = Repair(top.Addr);
                if (error != null || !Running) return error;
                (rod, casts) = (Durability(), 0);
                WaitFor(s => s.State == FishStates.Idle, 4);
                Events.Status("Đang quăng cần...", Level.Info);
            }
            else if (_game.Poll().State != FishStates.Idle) return null;
            else if (top.Addr != 0)
            {
                Session.Dialogs.Handle(top.Addr);
                Thread.Sleep(30);
            }
            else
            {
                var broken = rod is { Item1: 0 };
                if (broken && !options.AutoRepair) return RodBroken;
                if (casts >= CastAttempts) return "Không quăng được cần (kiểm tra cần câu, mồi và chỗ đứng)";
                var wait = RecastDelay - (Now - _resultClosed);
                if (wait > 0) Thread.Sleep(TimeSpan.FromSeconds(wait));
                casts++;
                Session.Tools.Fish();
                if (broken) WaitUntil(() => Session.Open.Top().Addr != 0, 2.5);
                else WaitFor(s => s.State != FishStates.Idle, 0.6);
            }
        }
        return null;
    }

    /// <summary>
    /// Theo 1 lượt câu tới khi kết thúc; true nếu câu được. Bóng hiện: báo ID cá ngay; không khớp lọc thì thu cần. Cắn: giật ngay (0 trễ). Cá
    /// lớn kéo phao: giật (mỗi 0,15 s); choáng: giật liên tục (0,08 s); bơi quanh: chờ (bấm lúc này là giật hụt). Bản miễn phí: không kéo cá lớn.
    /// Đọc 1 ms khi có bóng / đang đấu, 10 ms lúc khác.
    /// </summary>
    private bool Follow(bool ownCast)
    {
        int fishId = 0, drags = 0;
        int? previous = null;
        double lastTap = 0, started = Now;
        bool skipping = false, waitingShown = false, landed = false;
        while (Running)
        {
            var st = _game.Poll();
            var now = Now;
            var entered = st.State != previous;
            previous = st.State;
            landed |= st.State is >= FishStates.Waiting and < FishStates.Result || FishStates.IsBig(st.State);
            if (st.State == FishStates.Idle)
            {
                Assist.CastEnded(_game);
                if (ownCast && !landed && !skipping)
                {
                    _dryCasts++;
                    Events.Status("Phao không xuống được nước (chỗ cạn?) - quăng lại...", Level.Warn);
                }
                else if (!skipping && (fishId != 0 || now - started > 1)) Events.Status("Cá sổng, quăng lại...", Level.Quiet);
                if (ownCast && landed && fishId == 0) FakeZoneMissed();
                return false;
            }
            if (landed) _dryCasts = 0;
            if (fishId != 0) _fakeMisses = 0;
            if (st.State == FishStates.Result)
            {
                Assist.CastEnded(_game);
                Finish(fishId, st, ownCast);
                return ownCast;
            }
            if (fishId == 0 && st.LevelKey != 0 && st.State == FishStates.Shadow && (fishId = _game.FishId(st)) != 0)
            {
                fishing.Fish(fishId);
                Events.Status($"Bóng cá xuất hiện (ID {fishId})... Chờ cắn!", Level.Info);
            }
            var wanted = !options.FilterOn || options.Wants(fishId, catalog);
            if ((st.State == FishStates.Shadow && fishId != 0 || st.State == FishStates.Bite) && !wanted && !skipping)
                return Skip($"[Bỏ qua] ID {fishId} không khớp bộ lọc - thu cần thả lượt mới...");
            if (st.State == FishStates.Bite && !skipping)
            {
                Session.Tools.Fish();
                lastTap = now;
                Events.Status("Cá cắn! Đang kéo cá...", Level.Ok);
            }
            else if (st.State == FishStates.Waiting && !waitingShown)
            {
                waitingShown = true;
                Events.Status("Chờ cá vào mồi...", Level.Quiet);
            }
            else if (FishStates.IsBig(st.State) && AuthSession.Free) return Skip("Cá lớn (bóng 6-7) - bản miễn phí không kéo được, thu cần...");
            else if (st.State == FishStates.BigDrag && (entered || now - lastTap > 0.15))
            {
                Session.Tools.Fish();
                lastTap = now;
                if (entered) Events.Status($"Cá lớn kéo phao - giật lần {++drags}!", Level.Ok);
            }
            else if (st.State == FishStates.BigStun && now - lastTap > 0.08)
            {
                Session.Tools.Fish();
                lastTap = now;
                if (entered) Events.Status("Cá lớn đang choáng - giật liên tục!", Level.Ok);
            }
            else if (entered && st.State == FishStates.BigPumpin)
            {
                var hp = _game.BigFishHp();
                Events.Status($"Đang đấu với cá lớn{(hp is var (h, m) ? $" (HP {h}/{m})" : "")} - chờ cá kéo phao...", Level.Info);
            }
            Assist.Step(_game, st.State is FishStates.Waiting or FishStates.Shadow);
            Thread.Sleep(st.State is FishStates.Shadow or FishStates.Bite || FishStates.IsBig(st.State) ? 1 : 10);
        }
        return false;

        bool Skip(string message)
        {
            skipping = true;
            Events.Status(message, Level.Quiet);
            Session.Tools.Fish();
            Thread.Sleep(50);
            return false;
        }
    }
}
