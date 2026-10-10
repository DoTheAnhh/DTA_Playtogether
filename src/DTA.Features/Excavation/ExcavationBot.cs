using DTA.Engine.Bots;
using DTA.Engine.Gather;
using DTA.Engine.Movement;
using DTA.Game.Actor;
using DTA.Game.Data;
using DTA.Runtime.Device;

namespace DTA.Features.Excavation;

/// <summary>
/// Đào cổ vật tự động (bản đồ thường + Hòn đảo bị mất): chọn điểm (quà chờ nhận trước, lọc rương trên đảo), đi tới, đào bằng hàm game
/// (luồng gõ riêng), nhận quà cổ vật lớn. Điểm chưa lộ thì game cũng chưa biết loại - lọc rương chỉ bỏ được sau nhát đầu.
/// </summary>
public sealed partial class ExcavationBot(EmulatorDevice device, BotEvents events, GatherEvents gather, ExcavationOptions options)
    : GatherBot<Spot>(device, events, gather, options)
{
    protected override string Channel => "excavation";
    private const float FirstRange = 1, HitMargin = 0.3f, IslandStand = 0.6f;
    private ExcavationGame _game = null!;
    private DigTapper? _tapper;
    private ExcavationOptions Dig => (ExcavationOptions)Options;

    protected override string ToolName => "xẻng";
    protected override string ToolClass => Tools.Shovel;
    protected override string Broken => "Xẻng đã hỏng - bật Tự sửa xẻng hoặc sửa trong game";
    protected override string Place => "điểm đào";
    protected override bool StraightChoice => Teleporting || _game.IsIsland;

    /// <summary>Lọc rương chỉ có hiệu lực trên Hòn đảo bị mất.</summary>
    private bool ChestFilter => Dig.OnlyChest && (Dig.Mode == ExcavationMode.LostIsland || _game.IsIsland);

    private DigTapper Tapper => _tapper ??= new DigTapper(_game);

    protected override void Prepare()
    {
        _game = new ExcavationGame(Session);
        _game.LoadKinds();
        Session.Held.Read(Tools.Shovel);
        _game.State();
        Session.SceneChanged -= _game.SceneChanged;
        Session.SceneChanged += _game.SceneChanged;
        if (Session.Player.Facing() == null) return;
        _game.Spots();
        Session.Camera.Direction();
        Session.Ui.Joystick();
        _game.ShovelOffset();
    }

    protected override Phase CurrentPhase()
    {
        var state = _game.State();
        if (_game.IsIsland) return state == ExcavateState.Digging ? Phase.Busy : Phase.Idle;
        return state switch
        {
            ExcavateState.None => Phase.Idle,
            <= ExcavateState.Miss => Phase.Busy,
            ExcavateState.Complete => Phase.Claim,
            _ => Phase.Reward,
        };
    }

    protected override List<Spot> Targets() => [.. _game.Spots().Values];

    /// <summary>Đã đào xong: chỉ còn nhận quà nếu có phần của mình (không tính phạm vi). Đã lộ không phải rương khi lọc rương: bỏ.</summary>
    protected override bool Wanted(Spot spot)
    {
        if (spot.Kind != 0 && spot.Hp <= 0) return spot.Reward;
        if (ChestFilter && spot.Kind != 0 && !_game.IsChest(spot.Kind)) return false;
        return InRange(spot);
    }

    /// <summary>Ưu tiên: có quà nhận ngay > đã lộ cổ vật đang đào dở (đào cho xong) > chưa lộ.</summary>
    protected override int Priority(Spot spot) => spot.Reward ? 0 : spot.Kind != 0 ? 1 : 2;
    protected override bool NeedsTool(Spot spot) => !spot.Reward;
    protected override string Label(Spot spot) => spot.Kind != 0 ? _game.KindName(spot.Kind) : "điểm đào chưa lộ";

    protected override Spot? Refresh(Spot spot) =>
        _game.Reload(spot) is { } fresh && (fresh.Hp > 0 || fresh.Kind == 0 || fresh.Reward) ? fresh : null;

    /// <summary>
    /// Chạy thẳng tới thì mặt quay vào điểm, xẻng chạm đất trước mặt 1 quãng: tới khi điểm chạm lọt tầm đào là dừng. Cổ vật lớn có thân
    /// chắn: bị nó chặn quanh đó cũng là tới. Đảo: đi tới tận tâm.
    /// </summary>
    protected override (float Reach, float Solid) Stand(Spot spot)
    {
        if (_game.IsIsland) return (IslandStand, 0);
        var offset = _game.ShovelOffset();
        return ((spot.Kind != 0 ? spot.Reach : FirstRange) - HitMargin + offset, spot.Kind != 0 ? spot.Reach + offset + 1 : 0);
    }

    /// <summary>Đứng cách tâm điểm khi dịch chuyển: đảo 0,35 m; thường theo tầm đào + độ vươn xẻng.</summary>
    protected override float TeleportOffset(Spot spot) => _game.IsIsland ? 0.35f
        : Math.Max(0.5f, ((spot.Kind != 0 ? spot.Reach : FirstRange) - HitMargin * 0.5f) * 0.7f + _game.ShovelOffset() * 0.8f);

    protected override float TeleportArrive(Spot spot) => _game.IsIsland ? 1.8f : (spot.Kind != 0 ? spot.Reach : FirstRange) + 1.2f;

    /// <summary>Đảo: hỏi ở MỌI nhịp điều khiển (1 lượt đọc) - người khác vừa đào điểm này là bỏ ngay.</summary>
    protected override WalkResult GoTo(Spot spot, float reach, float solid, Func<bool> stillWanted, Action<int> onEscape, IEnumerable<(float X, float Z, float R)> avoid) =>
        _game.IsIsland
            ? Walk.Go((spot.X, spot.Z), IslandStand, pace: (_, _) => _game.IslandAlive(spot) ? (1f, 0f) : null)
            : base.GoTo(spot, reach, solid, stillWanted, onEscape, avoid);

    /// <summary>Đảo: xẻng hết thì mua gói mới (nếu bật Tự sửa); bản đồ thường game tự mở bảng sửa.</summary>
    protected override string? ReadyTool()
    {
        if (!_game.IsIsland || Durability() is not { Item1: 0 }) return null;
        return Options.AutoRepair ? BuyShovels() : Broken;
    }

    /// <summary>Đào 1 điểm tới khi xong rồi nhận quà; false = bỏ điểm này.</summary>
    protected override bool Harvest(Spot spot)
    {
        if (_game.IsIsland) return DigIsland(spot);
        Source = spot.Kind != 0 ? _game.KindName(spot.Kind) : "";
        try
        {
            return DigRegular(spot);
        }
        finally
        {
            Tapper.Pause();
        }
    }

    /// <summary>Đến vật bằng dịch chuyển (thường / đảo) hoặc đi bộ lại gần; kết quả theo walker.</summary>
    private WalkResult MoveCloser(Spot spot, float reach) =>
        Teleporting ? TeleportBeside(spot) : Walk.Go((spot.X, spot.Z), reach, solid: Stand(spot).Solid);

    protected override bool ShouldCollect()
    {
        if (!_game.IsIsland) return true;
        if (Session.Open.Top().Addr == 0 && _dug.Length > 0 && _dug != _game.KindName(1)) WaitUntil(() => Session.Open.Top().Addr != 0, 0.08);
        return Session.Open.Top().Addr != 0;
    }

    /// <summary>Đảo: đào xong mà game không mở bảng nhận đồ (quà là tiền sao về thẳng túi) thì vẫn ghi 1 dòng "Tiền sao".</summary>
    protected override void Collected()
    {
        if (Running && _dug.Length > 0 && !Recorded) Gather.Find(new FindRecord(DateTime.Now, _dug, StarMoney, 0, 0, "keep"));
        _dug = "";
    }
}
