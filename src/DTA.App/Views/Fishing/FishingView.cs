using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using DTA.App.Shell;
using DTA.Ui;
using DTA.Engine.Core;
using DTA.Features.Fishing;
using DTA.Game.Data;

namespace DTA.App.Views.Fishing;

/// <summary>
/// Câu cá: tab "Câu cá" (3 ô số liệu, trạng thái, tuỳ chọn, nâng cao POV / cắn nhanh, Bật / Tắt, thẻ lọc cá, thẻ giữ lại khi bán nhanh, thẻ vùng
/// câu) và tab "Lịch sử". Bản miễn phí không có thẻ nâng cao / lọc / giữ / vùng câu.
/// </summary>
public sealed partial class FishingView(MainWindow app) : PageView(app)
{
    private const double RowBot = 292, StatW = 118, StatH = 116, StatGap = 10, OptDx = 384, OptW = 170, AdvDx = 564, AdvW = 170, BtnW = 186, BtnH = 80;
    private const double FilterTop = 304, FilterH = 100, KeepTop = 416, KeepH = 92, ZoneTop = 520, ZoneH = 96;
    private const double ShadowDx = 72, ShadowStep = 31, GradeLabelDx = 300, GradeDx = 348, GradeW = 94, GradeStep = 98;
    private static readonly (bool Sell, string Label)[] Actions = [(false, "Bảo quản"), (true, "Bán nhanh")];

    private CanvasText _fishId = null!, _shadow = null!, _count = null!, _rod = null!, _state = null!, _dot = null!, _catalogInfo = null!;
    private readonly List<Ellipse> _gradeDots = [];
    private readonly List<Border> _shadowBars = [];
    private StatusTicker? _ticker;
    private CPower _start = null!, _stop = null!;
    private CButton _updateCatalog = null!;
    private CToggle _repair = null!, _package = null!;
    private CSegment<bool> _action = null!;
    private CToggle? _pov, _fastBite;

    public override string Name => "Câu cá";
    public override string Icon => "fish";
    public override string Subtitle => "Tự câu, giữ / bán cá theo ý muốn và ghi lại lịch sử";
    public override IReadOnlyList<(string Key, string Label)> Tabs => [("fishing", "Câu cá"), ("fishing_history", "Lịch sử")];

    private static double StatX(int index) => Layout.Left + index * (StatW + StatGap);

    public override void BuildTab(string tab)
    {
        if (tab == "fishing") BuildMain();
        else BuildHistory();
    }

    /// <summary>Dãy chip cỡ bóng 1..7 + dãy chip nền ở dòng <paramref name="y"/>.</summary>
    private (Dictionary<int, CChip> Shadows, Dictionary<int, CChip> Grades) ChipRows(double y, Action<int, bool> onShadow, Action<int, bool> onGrade)
    {
        const double left = Layout.Left;
        var shadows = Enumerable.Range(1, 7).ToDictionary(t => t, t => new CChip(Board, left + ShadowDx + (t - 1) * ShadowStep, y - 14, 28, 28, t.ToString(), on => onShadow(t, on)));
        var grades = GameNames.Grades.ToDictionary(g => g.Key, g =>
            new CChip(Board, left + GradeDx + (g.Key - 1) * GradeStep, y - 14, GradeW, 28, g.Value, on => onGrade(g.Key, on), Theme.Grades[g.Key]));
        return (shadows, grades);
    }

    private CanvasText Label(double x, double y, string text) => Board.Text(x, y, text, Anchor.W, Theme.Muted, 8, true);

    private void BuildMain()
    {
        const double left = Layout.Left, right = Layout.Right, top = Layout.ContentTop;
        var free = AuthSession.Free;
        var statsRight = StatX(2) + StatW;
        Board.Cards((StatX(0), top, StatW, StatH), (StatX(1), top, StatW, StatH), (StatX(2), top, StatW, StatH), (left, RowBot - 40, statsRight - left, 40),
            (left + OptDx, top, OptW, RowBot - top));
        if (!free)
            Board.Cards((left + AdvDx, top, AdvW, RowBot - top), (left, KeepTop, right - left, KeepH), (left, FilterTop, right - left, FilterH), (left, ZoneTop, right - left, ZoneH));
        CanvasText Stat(int index, string caption, Color color)
        {
            Board.Text(StatX(index) + 16, top + 14, caption, Anchor.NW, Theme.Muted, 8, true);
            return Board.Text(StatX(index) + 15, top + 34, "---", Anchor.NW, color, 22, true);
        }
        _fishId = Stat(0, "ID cá", Theme.Accent);
        _shadow = Stat(1, "Bóng", Theme.Text);
        _count = Stat(2, "Đã câu", Theme.Text);
        _count.Text = "0";
        var footY = top + StatH - 22;
        for (var i = 0; i < 5; i++) _gradeDots.Add(Board.Add(new Ellipse { Width = 10, Height = 10, IsHitTestVisible = false }, StatX(0) + 16 + i * 14, footY - 2));
        for (var i = 0; i < 7; i++) _shadowBars.Add(Board.Round(StatX(1) + 16 + i * 14, footY, 10, 5, 2, new Fill(Theme.White(30))));
        _rod = Board.Text(StatX(2) + 16, footY + 2, "", Anchor.W, Theme.Muted, 8);
        _dot = Board.Text(left + 16, RowBot - 20, "●", Anchor.W, Theme.Muted, 9);
        _state = Board.Text(left + 34, RowBot - 20, "Sẵn sàng", Anchor.W, Theme.Muted, 9, true);

        var ox = left + OptDx + 16;
        _repair = new CToggle(Board, ox, top + 22, "Tự sửa cần", on => Change(o => o.AutoRepair = on));
        _package = new CToggle(Board, ox, top + 56, "Có gói bán nhanh", OnPackage);
        Board.Text(ox, top + 94, "Sau khi câu được cá", Anchor.NW, Theme.Muted, 8, true);
        _action = new CSegment<bool>(Board, ox, top + 116, OptW - 32, Actions, sell =>
        {
            Change(o => o.Sell = sell);
            UpdateKeep();
        });
        _action.SetLocked(true, true);
        if (!free)
        {
            var ax = left + AdvDx + 16;
            _pov = new CToggle(Board, ax, top + 22, "Khoá POV", OnPov);
            _fastBite = new CToggle(Board, ax, top + 56, "Cá cắn nhanh", OnFastBite);
        }
        _start = new CPower(Board, right - BtnW, top, BtnW, BtnH, PowerKind.Start, Start);
        _start.SetEnabled(false);
        _stop = new CPower(Board, right - BtnW, RowBot - BtnH, BtnW, BtnH, PowerKind.Stop, Stop);
        _stop.SetEnabled(false);
        // Bản miễn phí: các thẻ dưới vẫn dựng (code cập nhật dùng chung) nhưng gỡ khỏi mặt vẽ -> không hiện, không bấm được
        var extras = Board.Collect(() =>
        {
            BuildKeep();
            BuildFilter();
            BuildZones();
        });
        if (free) foreach (var element in extras) Board.Canvas.Children.Remove(element);
    }

    public override void BuildFooter()
    {
        const double left = Layout.Left, foot = Layout.FootTop;
        _updateCatalog = new CButton(Board, left, foot + 12, 138, 28, "Cập nhật ID cá", UpdateCatalog, Theme.GhostFill, Theme.GhostHover, Theme.Text, 14, 8);
        _catalogInfo = Board.Text(left + 152, foot + 26, $"Database: {_catalog.Count} ID cá", Anchor.W, Theme.Muted, 8);
    }

    // ----- Hiển thị -----
    private void SetStatus(string text, Color color) => (_ticker ??= new StatusTicker([_state], [_dot], 54)).Set(text, color);

    public override void ShowMessage(string text, Color color) => SetStatus(text, color);

    /// <summary>ID cá + cỡ bóng (chỉ số), thang 7 vạch bóng, các chấm nền ID đó có thể ra.</summary>
    private void SetFishInfo(int? fishId)
    {
        var id = fishId ?? 0;
        var shadow = _catalog.ShadowOf(id);
        var grades = _catalog.GradesOf(id).OrderBy(g => g).ToList();
        _fishId.Text = id != 0 ? id.ToString() : "---";
        _shadow.Text = shadow > 0 ? shadow.ToString() : "---";
        for (var i = 0; i < _shadowBars.Count; i++) Board.Paint(_shadowBars[i], new Fill(i < shadow ? Theme.Accent : Theme.White(30)));
        for (var i = 0; i < _gradeDots.Count; i++)
        {
            _gradeDots[i].Fill = i < grades.Count && Theme.Grades.TryGetValue(grades[i], out var c)
                ? c.A == c.B ? Theme.Brush(c.A) : new LinearGradientBrush(c.A, c.B, 45)
                : null;
        }
    }

    private void SetRod((int Remaining, int Limit)? rod) =>
        _rod.Set(rod is { } r ? $"Cần câu {r.Remaining}/{r.Limit}" : "", rod is { Remaining: <= 3 } ? Theme.Warn : Theme.Muted);

    private void SetRunningUi(bool running)
    {
        _start.SetEnabled(!running && Device != null);
        _stop.SetEnabled(running);
        App.SetRunning(running, "Đang câu cá");
    }
}
