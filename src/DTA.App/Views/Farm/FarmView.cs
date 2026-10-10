using System.Windows.Media;
using DTA.App.Shell;
using DTA.Ui;
using DTA.Features.Farm;
using DTA.Game.Data;

namespace DTA.App.Views.Farm;

/// <summary>
/// Nông trại: 6 tab Hạt giống, Trồng trọt, Thu hoạch, Bán nông sản, Công cụ, Dọn dẹp. Mỗi tab 1 thẻ trên (tuỳ chọn + Bật / Tắt) và bảng sắp theo cấp
/// nền.
/// </summary>
public sealed partial class FarmView(MainWindow app) : PageView(app)
{
    private const double TopH = 165, BtnW = 175, BtnH = 54;
    private const int TableRows = 10;
    private const string Seeds = "farm_seeds", PlantTab = "farm_plant", Reap = "farm_reap", Sale = "farm_sell", Gear = "farm_gear", Clean = "farm_clean";
    private const string TodoNote = "Tự làm: chưa có - cần dò trên máy có giả lập trước (xem CLAUDE.md).";
    private static readonly Color SelOn = Theme.Hex("#123a5c"), SelOff = Theme.Hex("#0b1320");

    private readonly List<CanvasText> _dots = [], _states = [];
    private readonly List<CPower> _starts = [], _stops = [];
    private readonly List<CButton> _refreshes = [];
    private CanvasText _restock = null!, _seedBag = null!, _crops = null!, _ripe = null!, _growing = null!, _next = null!, _gear = null!, _saleCount = null!, _saleGrades = null!;
    private CSegment<bool> _currency = null!;
    private CSegment<int> _reapMin = null!;
    private CSegment<MutationMode> _mutation = null!;
    private CEntry _buyLimit = null!, _shopSearch = null!, _spacing = null!, _plantSearch = null!, _harvestSearch = null!, _minKg = null!, _maxKg = null!, _saleSearch = null!;
    private Dictionary<int, CChip> _shopGrades = [], _plantGrades = [], _harvestGrades = [], _saleGrades2 = [];
    private readonly Dictionary<int, CChip> _plots = [];
    private CChip _allPlots = null!;
    private CToggle _harvestMutation = null!, _keepRare = null!;
    private CButton _harvestMutButton = null!, _sellMutButton = null!;
    private CTable _shop = null!, _seeds = null!, _harvestTypes = null!, _reaped = null!, _fruits = null!, _sold = null!, _gearTable = null!;

    public override string Name => "Nông trại";
    public override string Icon => "farm";
    public override string Subtitle => "Tự động thu hoạch, trồng trọt, bán nông sản, mua hạt giống và quản lý nông trại";
    public override IReadOnlyList<(string Key, string Label)> Tabs =>
        [(Seeds, "Hạt giống"), (PlantTab, "Trồng trọt"), (Reap, "Thu hoạch"), (Sale, "Bán nông sản"), (Gear, "Công cụ"), (Clean, "Dọn dẹp")];

    public override void BuildTab(string tab)
    {
        switch (tab)
        {
            case Seeds: BuildSeeds(); break;
            case PlantTab: BuildPlant(); break;
            case Reap: BuildReap(); break;
            case Sale: BuildSale(); break;
            case Gear: BuildGear(); break;
            default: BuildClean(); break;
        }
    }

    public override void BuildFooter() => Board.Text(Layout.Left, Layout.FootTop + 26, "Chỉ chạy trong nông trại của bạn. Tự động đọc dữ liệu liên tục 6 tab.", Anchor.W, Theme.Muted, 8);

    /// <summary>Thẻ trên + dòng trạng thái 1 tab; trả (mép trái, mép trên, mép trên của bảng).</summary>
    private (double Left, double Top, double TableY) Frame()
    {
        var top = Top;
        Board.Cards((Layout.Left, top, Layout.Right - Layout.Left, TopH));
        _dots.Add(Board.Text(Layout.Left + 16, top + TopH - 18, "●", Anchor.W, Theme.Muted, 9));
        _states.Add(Board.Text(Layout.Left + 34, top + TopH - 18, "Sẵn sàng", Anchor.W, Theme.Muted, 9, true));
        return (Layout.Left, top, top + TopH + 12);
    }

    private void Caption(double x, double y, string text) => Board.Text(x, y, text, Anchor.NW, Theme.Muted, 8, true);

    private CanvasText Line(double x, double y, Color? color = null, double size = 10) => Board.Text(x, y, "---", Anchor.NW, color ?? Theme.Text, size, true);

    /// <summary>Nút Bật (việc <paramref name="mode"/>) / Tắt ở góc phải thẻ trên.</summary>
    private void RunButtons(double top, FarmMode mode)
    {
        const double x = Layout.Right - 16 - BtnW;
        var start = new CPower(Board, x, top + 16, BtnW, BtnH, PowerKind.Start, () => Start(mode));
        var stop = new CPower(Board, x, top + 22 + BtnH, BtnW, BtnH - 16, PowerKind.Stop, Stop);
        start.SetEnabled(false);
        stop.SetEnabled(false);
        _starts.Add(start);
        _stops.Add(stop);
    }

    private CTable Table(double x, double y, double width, IReadOnlyList<Column> columns, string stretch) => new(Board, x, y, width, columns, stretch, TableRows);

    /// <summary>5 chip nền trên 1 hàng: Trắng, Xanh lá, Xanh dương, Tím, VVIP.</summary>
    private Dictionary<int, CChip> GradeChips(double x, double y, Func<FarmOptions, HashSet<int>> set)
    {
        var widths = new Dictionary<int, double> { [1] = 44, [2] = 54, [3] = 72, [4] = 38, [5] = 44 };
        var chips = new Dictionary<int, CChip>();
        foreach (var (grade, name) in GameNames.Grades)
        {
            chips[grade] = new CChip(Board, x, y, widths[grade], 26, name, on => Change(o => Toggle(set(o), grade, on)));
            x += widths[grade] + 4;
        }
        return chips;
    }

    private void BuildSeeds()
    {
        var (left, top, tableY) = Frame();
        Caption(left + 16, top + 12, "Cửa hàng hạt giống");
        _restock = Line(left + 16, top + 34);
        _seedBag = Line(left + 16, top + 68, Theme.Muted, 9);
        Caption(left + 205, top + 12, "Thanh toán bằng");
        _currency = new CSegment<bool>(Board, left + 205, top + 32, 190, [(false, "Xu nông trại"), (true, "Kim cương")], diamond => Change(o => o.BuyWithDiamond = diamond));
        Caption(left + 205, top + 70, "Giới hạn trong túi");
        _buyLimit = new CEntry(Board, left + 205, top + 88, 80, 26, "50", () => Change(o => o.BuyLimit = int.TryParse(_buyLimit.Text, out var n) ? Math.Max(1, n) : o.BuyLimit));
        Caption(left + 430, top + 12, "Tìm kiếm");
        _shopGrades = GradeChips(left + 430, top + 32, o => o.ShopGrades);
        _shopSearch = new CEntry(Board, left + 430, top + 68, 280, 26, "Tìm hạt giống...", () => Change(o => o.ShopSearch = _shopSearch.Text.Trim()));
        RunButtons(top, FarmMode.BuySeeds);
        _shop = Table(left, tableY, Layout.Right - left,
        [
            new("name", "Hạt giống (bấm dòng để chọn)", 300), new("sel", "[ Chọn ]", 85, Anchor.Center), new("stock", "Trong shop", 130, Anchor.Center),
            new("flower", "Giá xu nông trại", 160, Anchor.E), new("diamond", "Giá kim cương", 150, Anchor.E), new("bag", "Trong túi", 140, Anchor.Center),
        ], "name");
        Selectable(_shop, o => o.BuySeeds);
    }

    private void BuildPlant()
    {
        var (left, top, tableY) = Frame();
        Caption(left + 16, top + 12, "Số gốc đã trồng");
        _crops = Line(left + 16, top + 34, Theme.Accent, 18);
        Board.Text(left + 16, top + 74, "Sức chứa nông trại", Anchor.NW, Theme.Muted, 8);
        Caption(left + 145, top + 12, "Vị trí ô đất (chọn nhiều)");
        _allPlots = new CChip(Board, left + 145, top + 32, 44, 26, "Tất cả", OnAllPlots);
        var px = left + 195;
        foreach (var (upper, lower) in new[] { (1, 5), (2, 6), (3, 7), (4, 8) })
        {
            _plots[upper] = new CChip(Board, px, top + 32, 36, 26, $"Ô {upper}", on => OnPlot(upper, on));
            _plots[lower] = new CChip(Board, px, top + 68, 36, 26, $"Ô {lower}", on => OnPlot(lower, on));
            px += 40;
        }
        Caption(left + 365, top + 12, "Khoảng cách cây");
        _spacing = new CEntry(Board, left + 365, top + 32, 65, 26, "0.5", () => Change(o =>
        {
            if (float.TryParse(_spacing.Text.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v))
                o.PlantSpacing = Math.Clamp(MathF.Round(v, 2), 0.1f, 3);
        }));
        Board.Text(left + 365, top + 74, "Mét (chuẩn: 0.5m)", Anchor.NW, Theme.Muted, 8);
        Caption(left + 450, top + 12, "Tìm kiếm");
        _plantGrades = GradeChips(left + 450, top + 32, o => o.PlantGrades);
        _plantSearch = new CEntry(Board, left + 450, top + 68, 264, 26, "Tìm tên hạt giống...", () => Change(o => o.PlantSearch = _plantSearch.Text.Trim()));
        RunButtons(top, FarmMode.Plant);
        _seeds = Table(left, tableY, Layout.Right - left,
        [
            new("name", "Hạt giống (bấm dòng để chọn)", 350), new("sel", "[ Chọn ]", 85, Anchor.Center), new("count", "Trong túi", 120, Anchor.Center),
            new("status", "Trạng thái", 140, Anchor.Center), new("planted", "Đang trồng (gốc)", 140, Anchor.Center),
        ], "name");
        Selectable(_seeds, o => o.PlantSeeds);
    }

    private void BuildReap()
    {
        var (left, top, tableY) = Frame();
        Caption(left + 16, top + 12, "Quả chín");
        _ripe = Line(left + 16, top + 32, Theme.Accent, 18);
        Caption(left + 95, top + 12, "Đang lớn");
        _growing = Line(left + 95, top + 32, Theme.Text, 18);
        _next = Board.Text(left + 16, top + 74, "", Anchor.NW, Theme.Muted, 8);
        Caption(left + 205, top + 12, "Mở bảng khi có ít nhất");
        _reapMin = new CSegment<int>(Board, left + 205, top + 32, 200, FarmIds.HarvestMins.Select(n => (n, $"{n} quả")).ToList(), n => Change(o => o.HarvestMin = n));
        _harvestMutation = new CToggle(Board, left + 205, top + 70, "Giữ cây biến thể", on =>
        {
            Change(o => o.KeepMutationHarvest = on);
            _harvestMutButton.SetEnabled(on);
        });
        _harvestMutButton = new CButton(Board, left + 360, top + 68, 75, 26, "⚙ Biến thể", () => EditMutations("Biến thể giữ lại khi thu hoạch", o => o.HarvestKeepMutations), radius: 8, size: 8);
        _harvestMutButton.SetEnabled(false);
        Caption(left + 450, top + 12, "Tìm kiếm");
        _harvestGrades = GradeChips(left + 450, top + 32, o => o.HarvestGrades);
        _harvestSearch = new CEntry(Board, left + 450, top + 68, 280, 26, "Tìm tên quả thu hoạch...", () => Change(o => o.HarvestSearch = _harvestSearch.Text.Trim()));
        RunButtons(top, FarmMode.Harvest);
        var half = Math.Floor((Layout.Right - left - 16) / 2);
        _harvestTypes = Table(left, tableY, half,
            [new("name", "Loại thu hoạch (bấm chọn)", 220), new("sel", "[ Chọn ]", 80, Anchor.Center), new("ripe", "Chín", 85, Anchor.Center), new("growing", "Đang lớn", 85, Anchor.Center)], "name");
        Selectable(_harvestTypes, o => o.HarvestFruits);
        _reaped = Table(left + half + 16, tableY, half, [new("time", "Thời gian", 110), new("name", "Nông sản", 180), new("kg", "kg", 65, Anchor.E), new("mutation", "Biến thể", 125)], "name");
    }

    private void BuildSale()
    {
        var (left, top, tableY) = Frame();
        Caption(left + 16, top + 12, "Biến thể");
        _mutation = new CSegment<MutationMode>(Board, left + 16, top + 32, 180, [(MutationMode.Any, "Mọi quả"), (MutationMode.None, "Không có"), (MutationMode.Only, "Chỉ có")],
            mode => Change(o => o.MutationMode = mode));
        _keepRare = new CToggle(Board, left + 16, top + 70, "Giữ cây biến thể", on =>
        {
            Change(o => o.KeepRare = on);
            _sellMutButton.SetEnabled(on);
        });
        _sellMutButton = new CButton(Board, left + 172, top + 68, 75, 26, "⚙ Biến thể", () => EditMutations("Biến thể giữ lại khi bán", o => o.SellKeepMutations), radius: 8, size: 8);
        _sellMutButton.SetEnabled(false);
        Caption(left + 270, top + 12, "Cân nặng (kg)");
        _minKg = new CEntry(Board, left + 270, top + 32, 55, 26, "từ", OnWeight);
        _maxKg = new CEntry(Board, left + 331, top + 32, 55, 26, "tới", OnWeight);
        _saleCount = Board.Text(left + 270, top + 68, "", Anchor.NW, Theme.Accent, 9, true);
        _saleGrades = Board.Text(left + 270, top + 88, "", Anchor.NW, Theme.Text, 10, true);
        Caption(left + 420, top + 12, "Tìm kiếm");
        _saleGrades2 = GradeChips(left + 420, top + 32, o => o.SellGrades);
        _saleSearch = new CEntry(Board, left + 420, top + 68, 280, 26, "Tìm tên cây nông sản...", () => Change(o => o.SellSearch = _saleSearch.Text.Trim()));
        RunButtons(top, FarmMode.Sell);
        var half = Math.Floor((Layout.Right - left - 16) / 2);
        _fruits = Table(left, tableY, half, [new("name", "Cây (bấm để chọn)", 240), new("sel", "[ Chọn ]", 80, Anchor.Center), new("count", "Trong túi", 145, Anchor.Center)], "name");
        Selectable(_fruits, o => o.SellFruits);
        _sold = Table(left + half + 16, tableY, half, [new("time", "Thời gian", 120), new("name", "Đã bán", 260), new("kg", "kg", 85, Anchor.E)], "name");
    }

    private void BuildGear()
    {
        var (left, top, tableY) = Frame();
        Caption(left + 16, top + 14, "Dụng cụ nông trại trong túi");
        _gear = Line(left + 16, top + 34, Theme.Accent, 18);
        Board.Text(left + 16, top + 86, TodoNote, Anchor.NW, Theme.Warn, 8);
        var refresh = new CButton(Board, Layout.Right - 16 - BtnW, top + 16, BtnW, 40, "⟳  Đọc lại", Scan, Theme.GhostFill, Theme.GhostHover, radius: 12, size: 10);
        refresh.SetEnabled(false);
        _refreshes.Add(refresh);
        _gearTable = Table(left, tableY, Layout.Right - left, [new("name", "Dụng cụ", 680), new("count", "Số lượng", 280, Anchor.Center)], "name");
    }

    private void BuildClean()
    {
        var (top, bottom) = (Top, Layout.ContentBottom);
        Board.Cards((Layout.Left, top, Layout.Right - Layout.Left, bottom - top));
        var center = (Layout.Left + Layout.Right) / 2;
        Board.Text(center, (top + bottom) / 2 - 14, "Dọn dẹp: tự động dọn gốc cây đã tàn", Anchor.Center, Theme.Text, 15, true);
        Board.Text(center, (top + bottom) / 2 + 20, "Dọn chỗ để tiếp tục gieo đợt hạt mới tự động.", Anchor.Center, Theme.Muted, 10);
    }
}
