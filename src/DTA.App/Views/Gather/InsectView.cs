using DTA.App.Shell;
using DTA.Ui;
using DTA.Engine.Bots;
using DTA.Engine.Core;
using DTA.Engine.Gather;
using DTA.Features.Insect;
using DTA.Game.Data;
using DTA.Runtime.Device;

namespace DTA.App.Views.Gather;

/// <summary>Bắt bọ: tự sửa vợt, gói bán nhanh, bảo quản / bán, cách di chuyển, đóng băng bọ, loài, nền muốn bắt, nền giữ khi bán nhanh.</summary>
public sealed class InsectView(MainWindow app) : GatherView<InsectOptions>(app)
{
    private const double GradeW = 94, GradeStep = 98;
    private static readonly (bool Sell, string Label)[] Actions = [(false, "Bảo quản"), (true, "Bán nhanh")];
    private CToggle _package = null!, _freeze = null!;
    private CSegment<bool> _action = null!;
    private CSegment<MoveMode> _move = null!;
    private Dictionary<string, CChip> _kinds = [];
    private Dictionary<int, CChip> _grades = [], _keeps = [];
    private CanvasText _keepLabel = null!;

    public override string Name => "Bắt bọ";
    public override string Icon => "bug";
    public override string Subtitle => "Tự rình lại gần côn trùng / chim / gói thẻ bay, vợt bằng hàm game, bảo quản hoặc bán";
    protected override string Key => "insect";
    protected override string Verb => "bắt";
    protected override string Tool => "vợt";
    protected override string Places => "Bọ trong tầm";
    protected override string Nearest => "Cách con gần nhất";
    protected override string Footer => "Hãy cầm vợt trước khi bật. Bot rình chậm theo ngưỡng cảnh giác của từng con và vung vợt bằng hàm game.";
    protected override double ExtraH => 198;

    protected override Bot CreateBot(EmulatorDevice device, BotEvents events, GatherEvents gather, InsectOptions options) => new InsectBot(device, events, gather, options);

    protected override IReadOnlyList<Column> Columns() =>
        [new("time", "Thời gian", 150), new("name", "Bắt được", 250), new("grade", "Nền", 100, Anchor.Center), new("price", "Giá bán", 100, Anchor.E), new("action", "Xử lý", 110, Anchor.Center)];

    protected override string[] Cells(HistoryEntry entry) =>
    [
        Formats.HistoryTime(entry.Time ?? ""), string.IsNullOrEmpty(entry.Name) ? entry.Source ?? "" : entry.Name, GradeName(entry.Grade), Formats.Price(entry.Price),
        entry.Action switch { "keep" => "Bảo quản", "sell" => "Bán nhanh", _ => "" },
    ];

    protected override void BuildOptions(double x, double top)
    {
        RepairToggle(x, top + 18);
        _package = new CToggle(Board, x, top + 52, "Có gói bán nhanh", OnPackage);
        Board.Text(x, top + 90, "Sau khi bắt được", Anchor.NW, Theme.Muted, 8, true);
        _action = new CSegment<bool>(Board, x, top + 112, OptW - 32, Actions, sell =>
        {
            Change(o => o.Sell = sell);
            UpdateKeep();
        });
        _action.SetLocked(true, true);
    }

    protected override void BuildExtra(double x, double y, double width)
    {
        CanvasText Label(double dx, double dy, string text) => Board.Text(x + dx, y + dy, text, Anchor.NW, Theme.Muted, 8, true);
        Dictionary<int, CChip> GradeChips(double dy, Action<InsectOptions, int, bool> set) => GameNames.Grades.ToDictionary(g => g.Key, g =>
            new CChip(Board, x + 16 + (g.Key - 1) * GradeStep, y + dy, GradeW, 28, g.Value, on => Change(o => set(o, g.Key, on)), Theme.Grades[g.Key]));

        _move = MoveSegment(x + 16, y + 32, 172);
        _freeze = new CToggle(Board, x + 204, y + 34, "Đóng băng bọ", OnFreeze);
        RangeSegment(x + 360, y + 12, 172);
        Label(550, 12, "Loại muốn bắt");
        Board.Text(x + 642, y + 12, "(Không chọn mặc định tất cả)", Anchor.NW, Theme.Dim, 8);
        var cx = x + 550;
        foreach (var (kind, label) in BugKinds.Labels)
        {
            var w = Math.Max(48, Math.Round(Theme.Measure(label, 9, true)) + 20);
            _kinds[kind] = new CChip(Board, cx, y + 35, w, 28, label, on => Change(o => Toggle(o.Kinds, kind, on)));
            cx += w + 4;
        }
        Label(16, 74, "Chỉ bắt nền");
        Board.Text(x + 96, y + 74, "(Không chọn mặc định tất cả)", Anchor.NW, Theme.Dim, 8);
        _grades = GradeChips(94, (o, g, on) => Toggle(o.Grades, g, on));
        _keepLabel = Label(16, 134, "Giữ lại nền");
        Board.Text(x + 88, y + 134, "(Giữ lại các nền bật sáng khi bán nhanh)", Anchor.NW, Theme.Dim, 8);
        _keeps = GradeChips(154, (o, g, on) => Toggle(o.KeepGrades, g, on));
        HopControls((x + 530, y + 74), (x + 530, y + 108));
    }

    private static void Toggle<T>(HashSet<T> set, T value, bool on)
    {
        if (on) set.Add(value);
        else set.Remove(value);
    }

    protected override void LoadExtra(InsectOptions options)
    {
        options.Move = MoveMode.Walk;
        _move.Set(MoveMode.Walk);
        _freeze.Set(Device != null && FreezeService.For(Device.Serial).Enabled);
        _package.Set(options.HasPackage);
        _action.SetLocked(true, !options.HasPackage);
        _action.Set(options.Sell);
        foreach (var (kind, chip) in _kinds) chip.Set(options.Kinds.Contains(kind));
        foreach (var (grade, chip) in _grades) chip.Set(options.Grades.Contains(grade));
        foreach (var (grade, chip) in _keeps) chip.Set(options.KeepGrades.Contains(grade));
        UpdateKeep();
    }

    /// <summary>Chip "Giữ lại nền" chỉ dùng được khi đang bán nhanh.</summary>
    private void UpdateKeep()
    {
        foreach (var chip in _keeps.Values) chip.SetEnabled(Options.Sell);
        _keepLabel.Color = Options.Sell ? Theme.Muted : Theme.Dim;
    }

    private void OnPackage(bool has)
    {
        Change(o => o.HasPackage = has);
        _action.SetLocked(true, !has);
        if (!has && Options.Sell)
        {
            Change(o => o.Sell = false);
            _action.Set(false);
            UpdateKeep();
        }
    }

    /// <summary>Bật đóng băng lần đầu thì hỏi xác nhận rủi ro.</summary>
    private void OnFreeze(bool on)
    {
        if (Device == null)
        {
            _freeze.Set(false);
            return;
        }
        if (on && !Risk.IsAccepted(RiskFeature.FreezeBugs))
        {
            if (!RiskDialog.Ask(App))
            {
                _freeze.Set(false);
                return;
            }
            Risk.Set(RiskFeature.FreezeBugs, true);
        }
        _freeze.Set(FreezeService.For(Device.Serial).SetEnabled(on) ? on : !on);
    }
}
