using DTA.App.Shell;
using DTA.Ui;
using DTA.Engine.Bots;
using DTA.Engine.Gather;
using DTA.Features.Mining;
using DTA.Runtime.Device;

namespace DTA.App.Views.Gather;

/// <summary>Đập đá: trang thu hoạch chung + cách di chuyển, loại muốn đập.</summary>
public sealed class MiningView(MainWindow app) : GatherView<MiningOptions>(app)
{
    private CSegment<MoveMode> _move = null!;
    private CSegment<RockKind> _kind = null!;

    public override string Name => "Đập đá";
    public override string Icon => "pickaxe";
    public override string Subtitle => "Tự đi tới mạch đá / quặng, đập bằng cuốc, nhận đồ và ghi lại lịch sử";
    protected override string Key => "mining";
    protected override string Verb => "đập";
    protected override string Tool => "cuốc";
    protected override string Places => "Điểm đập";
    protected override string Nearest => "Cách điểm gần nhất";
    protected override string Footer => "Tool tự cầm cuốc, hỗ trợ Đi bộ hoặc Dịch chuyển tới cạnh mạch đá gần nhất và đập tới khi vỡ hẳn.";
    protected override double ExtraH => 148;

    protected override Bot CreateBot(EmulatorDevice device, BotEvents events, GatherEvents gather, MiningOptions options) => new MiningBot(device, events, gather, options);

    /// <summary>Hàng 1: cách di chuyển + loại muốn đập; hàng 2: đổi bản đồ.</summary>
    protected override void BuildExtra(double x, double y, double width)
    {
        _move = MoveSegment(x + 16, y + 36, 220);
        Board.Text(x + 256, y + 14, "Loại muốn đập", Anchor.NW, Theme.Muted, 8, true);
        _kind = new CSegment<RockKind>(Board, x + 256, y + 36, 330, MiningOptions.Kinds, kind => Change(o => o.Kind = kind));
        HopControls((x + 16, y + 88), (x + 220, y + 74));
    }

    protected override void LoadExtra(MiningOptions options)
    {
        options.Move = MoveMode.Walk;
        _move.Set(MoveMode.Walk);
        _kind.Set(options.Kind);
    }
}
