using DTA.App.Shell;
using DTA.Ui;
using DTA.Engine.Bots;
using DTA.Engine.Gather;
using DTA.Features.Excavation;
using DTA.Runtime.Device;

namespace DTA.App.Views.Gather;

/// <summary>
/// Đào cổ vật: tab "Cổ vật" (map thường, đổi map, nút nhận quà thủ công), tab "Hòn đảo bị mất" (lọc rương), tab "Lịch sử". 2 tab chính dùng chung
/// số liệu + bot; nút Bật của mỗi tab chọn chế độ tương ứng.
/// </summary>
public sealed class ExcavationView(MainWindow app) : GatherView<ExcavationOptions>(app)
{
    private const string Island = "lost_island";
    private CSegment<MoveMode> _move = null!;
    private readonly List<CToggle> _chests = [];

    public override string Name => "Đào cổ vật";
    public override string Icon => "shovel";
    public override string Subtitle => "Tự đi tới điểm đào, đào, nhận quà và ghi lại lịch sử";
    protected override string Key => "excavation";
    protected override string Verb => "đào";
    protected override string Tool => "xẻng";
    protected override string Places => "Điểm đào";
    protected override string Nearest => "Cách điểm gần nhất";
    protected override string WholeMap => "cả map";
    protected override string Footer => "Hỗ trợ Đi bộ hoặc Dịch chuyển tới điểm đào, tự động quay mặt và căn POV camera sau lưng. Game chỉ cho biết dưới đất là gì sau nhát đào đầu tiên.";
    protected override double ExtraH => 148;
    protected override string Doing => Options.Mode == ExcavationMode.LostIsland ? "Đang đào đảo" : "Đang đào cổ vật";

    public override IReadOnlyList<(string Key, string Label)> Tabs => [(Key, "Cổ vật"), (Island, "Hòn đảo bị mất"), ($"{Key}_history", "Lịch sử")];

    protected override Bot CreateBot(EmulatorDevice device, BotEvents events, GatherEvents gather, ExcavationOptions options) => new ExcavationBot(device, events, gather, options);

    public override void BuildTab(string tab)
    {
        if (tab == Key) BuildMain(BuildOptions, true, () => StartIn(ExcavationMode.Excavation));
        else if (tab == Island) BuildMain(IslandOptions, false, () => StartIn(ExcavationMode.LostIsland));
        else base.BuildTab(tab);
    }

    /// <summary>Thẻ tuỳ chọn tab Cổ vật: tự sửa xẻng + phạm vi.</summary>
    protected override void BuildOptions(double x, double top)
    {
        RepairToggle(x, top + 20);
        RangeSegment(x, top + 58, OptW - 32, gap: 20);
    }

    /// <summary>Thẻ tuỳ chọn tab đảo: tự sửa xẻng, lọc rương, phạm vi (Cả đảo).</summary>
    private void IslandOptions(double x, double top)
    {
        RepairToggle(x, top + 18);
        _chests.Add(new CToggle(Board, x, top + 46, "Lọc rương", on =>
        {
            Change(o => o.OnlyChest = on);
            foreach (var toggle in _chests) toggle.Set(on);
        }));
        RangeSegment(x, top + 74, OptW - 32, "Cả đảo", 18);
    }

    /// <summary>Hàng 1: cách di chuyển; hàng 2: đổi bản đồ.</summary>
    protected override void BuildExtra(double x, double y, double width)
    {
        _move = MoveSegment(x + 16, y + 36, 220);
        HopControls((x + 16, y + 88), (x + 220, y + 74));
    }

    protected override void LoadExtra(ExcavationOptions options)
    {
        options.Move = MoveMode.Walk;
        _move.Set(MoveMode.Walk);
        foreach (var toggle in _chests) toggle.Set(options.OnlyChest);
    }

    /// <summary>Bật theo tab: tab Cổ vật bỏ lọc rương (chỉ có nghĩa trên đảo).</summary>
    private void StartIn(ExcavationMode mode)
    {
        Change(o =>
        {
            o.Mode = mode;
            if (mode == ExcavationMode.Excavation) o.OnlyChest = false;
        });
        foreach (var toggle in _chests) toggle.Set(Options.OnlyChest);
        Start();
    }
}
