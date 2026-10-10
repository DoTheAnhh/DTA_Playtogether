using DTA.App.Shell;
using DTA.Ui;
using DTA.Engine.Bots;
using DTA.Engine.Gather;
using DTA.Features.Collect;
using DTA.Runtime.Device;

namespace DTA.App.Views.Gather;

/// <summary>Thu lượm: trang thu hoạch chung + cách di chuyển, loại muốn nhặt (mặc định đi bộ).</summary>
public sealed class CollectView(MainWindow app) : GatherView<CollectOptions>(app)
{
    private const double KindW = 86, KindStep = 92;
    private CSegment<MoveMode> _move = null!;
    private readonly Dictionary<string, CChip> _kinds = [];

    public override string Name => "Thu lượm";
    public override string Icon => "card";
    public override string Subtitle => "Tự đi nhặt củi, trứng, nấm, rau củ, gói thẻ dưới đất... và ghi lại lịch sử";
    protected override string Key => "collect";
    protected override string Verb => "nhặt";
    protected override string Places => "Đồ nhặt được";
    protected override string Nearest => "Cách món gần nhất";
    protected override string Footer => "Hỗ trợ Đi bộ hoặc Dịch chuyển tới vật gần nhất, tự động quay mặt vào vật, nhặt bằng hàm game và đổi map tuần hoàn.";
    protected override double ExtraH => 148;

    protected override Bot CreateBot(EmulatorDevice device, BotEvents events, GatherEvents gather, CollectOptions options) => new CollectBot(device, events, gather, options);

    protected override void BuildExtra(double x, double y, double width)
    {
        _move = MoveSegment(x + 16, y + 36, 200);
        Board.Text(x + 232, y + 14, "Loại muốn nhặt", Anchor.NW, Theme.Muted, 8, true);
        Board.Text(x + 332, y + 14, "(Không chọn mặc định tất cả)", Anchor.NW, Theme.Dim, 8);
        for (var i = 0; i < CollectOptions.KindLabels.Length; i++)
        {
            var (kind, label) = CollectOptions.KindLabels[i];
            _kinds[kind] = new CChip(Board, x + 232 + i * KindStep, y + 36, KindW, 28, label, on => Change(o =>
            {
                if (on) o.Kinds.Add(kind);
                else o.Kinds.Remove(kind);
            }));
        }
        HopControls((x + 16, y + 88), (x + 220, y + 74));
    }

    protected override void LoadExtra(CollectOptions options)
    {
        options.Move = MoveMode.Walk;
        _move.Set(MoveMode.Walk);
        foreach (var (kind, chip) in _kinds) chip.Set(options.Kinds.Contains(kind));
    }
}
