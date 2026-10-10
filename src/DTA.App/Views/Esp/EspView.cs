using System.Windows.Media;
using DTA.App.Shell;
using DTA.Ui;
using DTA.Engine.Core;
using DTA.Features.Esp;
using DTA.Runtime.Device;

namespace DTA.App.Views.Esp;

/// <summary>
/// ESP: bật / tắt, nhóm + loại vật muốn xem, phạm vi, cỡ chữ, bảng loại đang có (bấm để ẩn / hiện), bảng vật gần nhân vật nhất, lớp phủ vẽ nhãn
/// lên cửa sổ game. Chỉ đọc game nên chạy song song chức năng khác được.
/// </summary>
public sealed class EspView(MainWindow app) : PageView(app)
{
    private const double OptionsH = 96, TablesDy = 108, KindsW = 440;
    private const int NearRows = 60;
    private static readonly IReadOnlyDictionary<string, double> ChipWidths = new Dictionary<string, double>
    {
        [EspGroups.Relic] = 74, [EspGroups.Ore] = 98, [EspGroups.Plant] = 126, [EspGroups.Insect] = 88, [EspGroups.Card] = 56,
    };
    private EspOptions _options = new();
    private EmulatorDevice? _device;
    private EspScanner? _scanner;
    private EspOverlay? _overlay;
    private Dictionary<string, int> _counts = [];
    private CToggle _on = null!, _distance = null!;
    private CSegment<int> _range = null!, _font = null!;
    private readonly Dictionary<string, CChip> _chips = [];
    private CTable _kinds = null!, _near = null!;
    private CanvasText _status = null!, _dot = null!;
    private StatusTicker? _ticker;

    public override string Name => "ESP";
    public override string Icon => "eye";
    public override string Subtitle => "Xem mọi cổ vật, đá, cây, côn trùng, gói thẻ đang có trong bản đồ, vẽ tên ngay trên cửa sổ game";
    public override IReadOnlyList<(string Key, string Label)> Tabs => [("esp", "ESP")];

    public override void BuildTab(string tab)
    {
        const double left = Layout.Left, right = Layout.Right, top = Layout.ContentTop, tablesTop = top + TablesDy, nearX = left + KindsW + 12, bottom = Layout.ContentBottom;
        Board.Cards((left, top, right - left, OptionsH), (left, tablesTop, KindsW, bottom - tablesTop), (nearX, tablesTop, right - nearX, bottom - tablesTop));
        CanvasText Label(double x, double y, string text, Anchor anchor = Anchor.W) => Board.Text(x, y, text, anchor, Theme.Muted, 8, true);
        const double line1 = top + 28, line2 = top + 68;
        _on = new CToggle(Board, left + 16, line1 - 9, "Bật ESP", OnToggle);
        _on.SetEnabled(false);
        _distance = new CToggle(Board, left + 130, line1 - 9, "Hiện khoảng cách", on => Change(o => o.Distance = on));
        Label(right - 16 - 216 - 10, line1, "Phạm vi", Anchor.E);
        _range = new CSegment<int>(Board, right - 16 - 216, line1 - 17, 216, EspGroups.Ranges.Select(r => (r, r > 0 ? $"{r} m" : "Cả map")).ToList(), r => Change(o => o.Radius = r));
        Label(left + 16, line2, "Nhóm");
        var x = left + 58;
        foreach (var group in EspGroups.All)
        {
            var color = Theme.Hex(EspGroups.Colors[group]);
            _chips[group] = new CChip(Board, x, line2 - 14, ChipWidths[group], 28, group, on =>
            {
                Change(o =>
                {
                    if (on) o.Groups.Add(group);
                    else o.Groups.Remove(group);
                });
                ShowKinds();
            }, (color, color));
            x += ChipWidths[group] + 5;
        }
        Board.Text(x + 4, line2, "(Không chọn mặc định tất cả)", Anchor.W, Theme.Dim, 7);
        Label(right - 16 - 216 - 10, line2, "Cỡ chữ", Anchor.E);
        _font = new CSegment<int>(Board, right - 16 - 216, line2 - 17, 216, EspGroups.FontSizes, size => Change(o => o.FontSize = size));
        Label(left + 16, tablesTop + 18, "Loại đang có trên bản đồ");
        Board.Text(left + KindsW - 16, tablesTop + 18, "bấm vào dòng để hiện / ẩn", Anchor.E, Theme.Dim, 8);
        _kinds = new CTable(Board, left + 16, tablesTop + 34, KindsW - 32,
            [new("pick", "Hiện", 48, Anchor.Center), new("kind", "Loại", 190), new("group", "Nhóm", 120), new("count", "SL", 46, Anchor.Center)], "kind", 12);
        _kinds.RowClicked += (row, _) =>
        {
            Change(o =>
            {
                if (!o.Hidden.Remove(row.Key)) o.Hidden.Add(row.Key);
            });
            ShowKinds();
        };
        Label(nearX + 16, tablesTop + 18, "Gần nhân vật nhất");
        _near = new CTable(Board, nearX + 16, tablesTop + 34, right - nearX - 32,
            [new("kind", "Tên", 200), new("group", "Nhóm", 130), new("distance", "Cách", 65, Anchor.Center), new("note", "Ghi chú", 80)], "kind", 12);
    }

    public override void BuildFooter()
    {
        _dot = Board.Text(Layout.Left, Layout.FootTop + 26, "●", Anchor.W, Theme.Muted, 7);
        _status = Board.Text(Layout.Left + 14, Layout.FootTop + 26, "ESP đang tắt. Chỉ đọc dữ liệu của game để hiện lên, không bấm hay sửa gì trong game.", Anchor.W, Theme.Muted, 8);
    }

    public override void OnClose()
    {
        Stop();
        _overlay?.Close();
    }

    public override void OnDevice(EmulatorDevice? device)
    {
        Stop();
        _device = device;
        _options = device != null ? DeviceStore.Options<EspOptions>(device.Serial, "esp") : new EspOptions();
        _options.Normalize();
        _options.Overlay = true;
        _distance.Set(_options.Distance);
        _range.Set(_options.Radius);
        _font.Set(_options.FontSize);
        foreach (var (group, chip) in _chips) chip.Set(_options.Groups.Contains(group));
        _on.SetEnabled(device != null);
        SetStatus(device != null ? "ESP đang tắt" : "Chưa chọn tab giả lập", Theme.Muted);
    }

    private void Change(Action<EspOptions> change)
    {
        change(_options);
        if (_device != null) DeviceStore.SaveOptions(_device.Serial, "esp", _options);
    }

    private void OnToggle(bool on)
    {
        if (!on)
        {
            Stop();
            SetStatus("ESP đang tắt", Theme.Muted);
            return;
        }
        if (_device == null)
        {
            _on.Set(false);
            SetStatus("Chưa chọn tab giả lập", Theme.Warn);
            return;
        }
        _overlay ??= new EspOverlay();
        var scanner = _scanner = new EspScanner(_device, _options);
        scanner.Listed += (near, counts) => App.Ui(() => { if (scanner == _scanner) OnList(near, counts); });
        scanner.Status += (text, error) => App.Ui(() => { if (scanner == _scanner) SetStatus(text, error ? Theme.Warn : Theme.Accent); });
        scanner.Start();
        _overlay.Begin(scanner, _device, _options, LabelColor);
    }

    private void Stop()
    {
        _scanner?.Stop();
        _scanner = null;
        _overlay?.End();
        _on.Set(false);
        OnList([], []);
    }

    /// <summary>Màu nhãn: côn trùng theo màu nền của loài (nếu có), còn lại theo nhóm.</summary>
    private static Color LabelColor(string group, int grade) =>
        group == EspGroups.Insect && Theme.Grades.TryGetValue(grade, out var tint) ? tint.A : Theme.Hex(EspGroups.Colors[group]);

    private void OnList(List<(string Kind, string Group, float Distance, string Note)> near, Dictionary<string, int> counts)
    {
        _counts = counts;
        ShowKinds();
        _near.SetRows(near.Take(NearRows).Select((n, i) => new TableRow(i.ToString(), [n.Kind, n.Group, $"{n.Distance:0} m", n.Note])).ToList());
    }

    /// <summary>Bảng loại theo số lượng mới nhất; loại đang ẩn (tắt riêng hoặc cả nhóm tắt) thì mờ.</summary>
    private void ShowKinds()
    {
        var rows = _counts.Keys.Select(key => (Key: key, Parts: key.Split('|', 2)))
            .OrderBy(k => Array.IndexOf(EspGroups.All, k.Parts[0])).ThenBy(k => k.Parts[1], StringComparer.CurrentCulture)
            .Select(k =>
            {
                var shown = (_options.Groups.Count == 0 || _options.Groups.Contains(k.Parts[0])) && !_options.Hidden.Contains(k.Key);
                return new TableRow(k.Key, [shown ? "✔" : "", k.Parts[1], k.Parts[0], _counts[k.Key].ToString()], shown ? null : Theme.Dim);
            }).ToList();
        _kinds.SetRows(rows);
    }

    private void SetStatus(string text, Color color) => (_ticker ??= new StatusTicker([_status], [_dot], 110)).Set(text, color);

    public override void ShowMessage(string text, Color color) => SetStatus(text, color);
}
