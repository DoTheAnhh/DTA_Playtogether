using System.Windows.Media;
using DTA.App.Shell;
using DTA.Ui;
using DTA.Engine.Core;
using DTA.Engine.Gather;
using DTA.Engine.Movement;

namespace DTA.App.Views.Gather;

/// <summary>
/// Trang chung của các chức năng "tự đi tới từng vật rồi thu hoạch" (đào cổ vật, đập đá, nhặt đồ, bắt bọ): tab chính (3 ô số liệu, thanh trạng
/// thái, thẻ tuỳ chọn, Bật / Tắt, thẻ tuỳ chọn riêng + đổi bản đồ) và tab "Lịch sử". Lớp con khai báo tên gọi, bot, tuỳ chọn riêng. Ô số liệu /
/// nút / công tắc chung giữ theo danh sách: trang có nhiều tab chính (đào cổ vật + đảo) thì mọi tab cùng cập nhật.
/// </summary>
public abstract partial class GatherView<TOptions>(MainWindow app) : PageView(app) where TOptions : GatherOptions, new()
{
    protected const double RowBot = 292, StatW = 132, StatH = 116, StatGap = 12, OptDx = 432, OptW = 204, BtnW = 204, BtnH = 80, ListTop = 304;

    /// <summary>Tên ngắn: mã tab + đuôi file dữ liệu riêng ("mining").</summary>
    protected abstract string Key { get; }
    /// <summary>Động từ thu hoạch, viết thường ("đào", "đập").</summary>
    protected abstract string Verb { get; }
    /// <summary>Tên dụng cụ viết thường ("xẻng"); rỗng = làm tay không.</summary>
    protected virtual string Tool => "";
    /// <summary>Tên ô số liệu 1 (vật trong tầm) và 2 (khoảng cách tới vật đang nhắm).</summary>
    protected abstract string Places { get; }
    protected abstract string Nearest { get; }
    protected abstract string Footer { get; }
    /// <summary>Chiều cao thẻ tuỳ chọn riêng dưới hàng thẻ chính.</summary>
    protected virtual double ExtraH => 84;
    /// <summary>Chữ trên huy hiệu đầu trang khi đang chạy.</summary>
    protected virtual string Doing => $"Đang {Name.ToLower()}";
    /// <summary>Ghi chú dưới ô số vật: tổng cả bản đồ.</summary>
    protected virtual string WholeMap => "cả bản đồ";

    public override IReadOnlyList<(string Key, string Label)> Tabs => [(Key, Name), ($"{Key}_history", "Lịch sử")];

    protected TOptions Options { get; set; } = new();
    private readonly List<CanvasText> _targets = [], _targetsNote = [], _distance = [], _target = [], _count = [], _tool = [], _state = [], _dot = [];
    private readonly List<CPower> _starts = [], _stops = [];
    private readonly List<CToggle> _repairs = [];
    private readonly List<CSegment<int>> _ranges = [];
    private StatusTicker? _ticker;
    private CToggle? _hop;
    private Dictionary<int, CChip> _mapChips = [];
    private int _found;

    protected static double StatX(int index) => Layout.Left + index * (StatW + StatGap);

    public override void BuildTab(string tab)
    {
        if (tab == Key) BuildMain(BuildOptions, true, Start);
        else BuildHistory();
    }

    /// <summary>Tab chính: 3 ô số liệu, thanh trạng thái, thẻ tuỳ chọn (<paramref name="options"/>), Bật / Tắt, thẻ riêng (nếu <paramref name="extra"/>).</summary>
    protected void BuildMain(Action<double, double> options, bool extra, Action start)
    {
        const double left = Layout.Left, right = Layout.Right, top = Layout.ContentTop;
        var statsRight = StatX(2) + StatW;
        Board.Cards((StatX(0), top, StatW, StatH), (StatX(1), top, StatW, StatH), (StatX(2), top, StatW, StatH), (left, RowBot - 40, statsRight - left, 40),
            (left + OptDx, top, OptW, RowBot - top));
        if (extra) Board.Cards((left, ListTop, right - left, ExtraH));
        void Stat(int index, string caption, Color color, List<CanvasText> numbers, List<CanvasText> notes)
        {
            var x = StatX(index);
            Board.Text(x + 16, top + 14, caption, Anchor.NW, Theme.Muted, 8, true);
            numbers.Add(Board.Text(x + 15, top + 34, "---", Anchor.NW, color, 22, true));
            notes.Add(Board.Text(x + 16, top + StatH - 20, "", Anchor.W, Theme.Muted, 8));
        }
        Stat(0, Places, Theme.Accent, _targets, _targetsNote);
        Stat(1, Nearest, Theme.Text, _distance, _target);
        Stat(2, $"Đã {Verb}", Theme.Text, _count, _tool);
        _count[^1].Text = _found.ToString();
        _dot.Add(Board.Text(left + 16, RowBot - 20, "●", Anchor.W, Theme.Muted, 9));
        _state.Add(Board.Text(left + 34, RowBot - 20, "Sẵn sàng", Anchor.W, Theme.Muted, 9, true));
        options(left + OptDx + 16, top);
        _starts.Add(new CPower(Board, right - BtnW, top, BtnW, BtnH, PowerKind.Start, start));
        var stop = new CPower(Board, right - BtnW, RowBot - BtnH, BtnW, BtnH, PowerKind.Stop, Stop);
        stop.SetEnabled(false);
        _stops.Add(stop);
        if (extra) BuildExtra(left, ListTop, right - left);
    }

    /// <summary>Thẻ tuỳ chọn: tự sửa dụng cụ (nếu có) + phạm vi.</summary>
    protected virtual void BuildOptions(double x, double top)
    {
        if (Tool.Length > 0) RepairToggle(x, top + 26);
        RangeSegment(x, top + (Tool.Length > 0 ? 80 : 26), OptW - 32);
    }

    /// <summary>Công tắc "Tự sửa (dụng cụ)" (mọi bản sao đồng bộ).</summary>
    protected void RepairToggle(double x, double y) => _repairs.Add(new CToggle(Board, x, y, $"Tự sửa {Tool}", on =>
    {
        Change(o => o.AutoRepair = on);
        foreach (var toggle in _repairs) toggle.Set(on);
    }));

    /// <summary>Dòng chữ + dãy chọn phạm vi tại (x, y) (mọi bản sao đồng bộ).</summary>
    protected void RangeSegment(double x, double y, double width, string whole = "Cả map", double gap = 22)
    {
        Board.Text(x, y, "Phạm vi quanh chỗ đứng", Anchor.NW, Theme.Muted, 8, true);
        _ranges.Add(new CSegment<int>(Board, x, y + gap, width, GatherOptions.Ranges.Select(r => (r, r > 0 ? $"{r} m" : whole)).ToList(), radius =>
        {
            Change(o => o.Radius = radius);
            foreach (var segment in _ranges) segment.Set(radius);
        }));
    }

    /// <summary>Công tắc "Tự động đổi map" + các chip bản đồ được sang.</summary>
    protected void HopControls((double X, double Y) toggle, (double X, double Y) maps)
    {
        _hop = new CToggle(Board, toggle.X, toggle.Y, "Tự động đổi map", on => Change(o => o.Hop = on));
        Board.Text(maps.X, maps.Y, "Các map được sang", Anchor.NW, Theme.Muted, 8, true);
        Board.Text(maps.X + 124, maps.Y, "(Không chọn mặc định tất cả)", Anchor.NW, Theme.Dim, 8);
        _mapChips = Travel.Maps.Keys.Select((id, i) => (id, chip: new CChip(Board, maps.X + i * 108, maps.Y + 20, 104, 28, Travel.Label(id), on => Change(o =>
        {
            if (on) o.Maps.Add(id);
            else o.Maps.Remove(id);
        })))).ToDictionary(p => p.id, p => p.chip);
    }

    /// <summary>Thẻ dưới hàng thẻ chính: mặc định chỉ có đổi bản đồ; lớp con vẽ thêm phần riêng và vẫn gọi <see cref="HopControls"/>.</summary>
    protected virtual void BuildExtra(double x, double y, double width) => HopControls((x + 16, y + 26), (x + 220, y + 14));

    /// <summary>Đưa tuỳ chọn riêng vừa nạp lên giao diện.</summary>
    protected virtual void LoadExtra(TOptions options) { }

    /// <summary>Dãy chọn Đi bộ / Dịch chuyển (nhãn ở trên 22 px): chọn Dịch chuyển lần đầu thì hỏi xác nhận rủi ro.</summary>
    protected CSegment<MoveMode> MoveSegment(double x, double y, double width)
    {
        Board.Text(x, y - 22, "Phương thức di chuyển", Anchor.NW, Theme.Muted, 8, true);
        CSegment<MoveMode> segment = null!;
        segment = new CSegment<MoveMode>(Board, x, y, width, [(MoveMode.Walk, "Đi bộ"), (MoveMode.Teleport, "Dịch chuyển")], mode =>
        {
            if (mode == MoveMode.Teleport && !Risk.IsAccepted(RiskFeature.Teleport))
            {
                if (!RiskDialog.Ask(App))
                {
                    segment.Set(MoveMode.Walk);
                    return;
                }
                Risk.Set(RiskFeature.Teleport, true);
            }
            Change(o => o.Move = mode);
        });
        return segment;
    }

    public override void BuildFooter() => Board.Text(Layout.Left, Layout.FootTop + 26, Footer, Anchor.W, Theme.Muted, 8);

    /// <summary>Đổi tuỳ chọn: lưu ngay; bot đọc thẳng object này nên có tác dụng tức thì.</summary>
    protected void Change(Action<TOptions> change)
    {
        change(Options);
        if (Device != null) DeviceStore.SaveOptions(Device.Serial, Key, Options);
    }

    /// <summary>Đưa tuỳ chọn của tab giả lập vừa chọn lên giao diện.</summary>
    private void ShowOptions()
    {
        foreach (var toggle in _repairs) toggle.Set(Options.AutoRepair);
        foreach (var segment in _ranges) segment.Set(Options.Radius);
        _hop?.Set(Options.Hop);
        foreach (var (id, chip) in _mapChips) chip.Set(Options.Maps.Contains(id));
        LoadExtra(Options);
    }

    // ----- Hiển thị -----
    protected void SetStatus(string text, Color color) => (_ticker ??= new StatusTicker(_state, _dot, 54)).Set(text, color);

    public override void ShowMessage(string text, Color color) => SetStatus(text, color);

    private static void SetAll(List<CanvasText> labels, string text)
    {
        foreach (var label in labels) label.Text = text;
    }

    private void SetTargets(int inRange, int total)
    {
        SetAll(_targets, inRange.ToString());
        SetAll(_targetsNote, $"{WholeMap}: {total}");
    }

    private void SetTarget(float? distance, string name)
    {
        SetAll(_distance, distance is { } d ? d.ToString("0.0") : "---");
        SetAll(_target, distance == null ? "" : name);
    }

    private void SetTool((int Remaining, int Limit)? tool)
    {
        var text = tool is { } t ? $"{char.ToUpper(Tool[0])}{Tool[1..]} {t.Remaining}/{t.Limit}" : "";
        foreach (var label in _tool) label.Set(text, tool is { Remaining: <= 3 } ? Theme.Warn : Theme.Muted);
    }

    private void SetRunningUi(bool running)
    {
        foreach (var button in _starts) button.SetEnabled(!running);
        foreach (var button in _stops) button.SetEnabled(running);
        App.SetRunning(running, Doing);
    }
}
