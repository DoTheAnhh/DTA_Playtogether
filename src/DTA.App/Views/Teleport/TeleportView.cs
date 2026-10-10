using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using DTA.App.Shell;
using DTA.Ui;
using DTA.Engine.Core;
using DTA.Features.Teleport;
using DTA.Game.Data;
using DTA.Runtime.Device;

namespace DTA.App.Views.Teleport;

/// <summary>
/// Dịch chuyển: vị trí nhân vật (bản đồ, toạ độ, góc nhìn), vị trí đang chọn (sửa được nếu là của tôi), các nút lưu / sửa / xoá / dịch chuyển /
/// đồng bộ, lọc bản đồ, bảng vị trí [SERVER] (chỉ đọc) + [CỦA TÔI]. Chỉ theo dõi nhân vật khi trang đang hiện.
/// </summary>
public sealed partial class TeleportView(MainWindow app) : PageView(app)
{
    private const double CardW = 430, CardH = 100, ButtonsDy = 112, TableDy = 154, RowH = 24, TableSpare = 92;
    private const string AllMaps = "Tất cả bản đồ", Hint = "Vị trí [SERVER] chỉ đọc  •  Vị trí [CỦA TÔI] có thể thêm / sửa / xoá";
    private EmulatorDevice? _device;
    private TeleportController? _tracker;
    private PlayerPlace _here;
    private List<TelePlace> _places = [];
    private Dictionary<string, TelePlace> _byId = [];
    private string _filter = AllMaps;
    private double _lastSync = -1e9;
    private CanvasText _map = null!, _mapId = null!, _rot = null!, _status = null!, _dot = null!, _count = null!, _note = null!;
    private readonly List<CanvasText> _axes = [];
    private CEntry _name = null!, _rotEntry = null!;
    private readonly List<CEntry> _xyz = [];
    private CButton _add = null!, _update = null!, _delete = null!, _confirm = null!;
    private CCombo _maps = null!;
    private CTable _table = null!;
    private StatusTicker? _ticker;

    public override string Name => "Dịch chuyển";
    public override string Icon => "pin";
    public override string Subtitle => "Quản lý vị trí Server & Vị trí cá nhân của bạn";
    public override IReadOnlyList<(string Key, string Label)> Tabs => [("teleport", "Vị trí")];

    public override void BuildTab(string tab)
    {
        const double left = Layout.Left, right = Layout.Right, top = Layout.ContentTop, rightCard = right - CardW, tableTop = top + TableDy, bottom = Layout.ContentBottom;
        Board.Cards((left, top, CardW, CardH), (rightCard, top, CardW, CardH), (left, tableTop, right - left, bottom - tableTop));
        CanvasText Label(double x, double y, string text) => Board.Text(x, y, text, Anchor.W, Theme.Muted, 8, true);
        Board.Text(left + 16, top + 14, "Vị trí nhân vật", Anchor.NW, Theme.Muted, 8, true);
        _map = Board.Text(left + 16, top + 40, "---", Anchor.W, Theme.Accent, 12, true);
        _mapId = Board.Text(left + CardW - 16, top + 40, "", Anchor.E, Theme.Dim, 8);
        for (var i = 0; i < 3; i++)
        {
            var x = left + 16 + i * 95;
            Label(x, top + 64, "XYZ"[i].ToString());
            _axes.Add(Board.Text(x + 14, top + 64, "---", Anchor.W, Theme.Text, 11, true));
        }
        Label(left + 16 + 3 * 95, top + 64, "Góc nhìn");
        _rot = Board.Text(left + 16 + 3 * 95 + 56, top + 64, "---", Anchor.W, Theme.Accent, 10, true);
        _dot = Board.Text(left + 16, top + 86, "●", Anchor.W, Theme.Muted, 7);
        _status = Board.Text(left + 30, top + 86, "Chưa chọn tab giả lập", Anchor.W, Theme.Muted, 8);

        Board.Text(rightCard + 16, top + 14, "Vị trí đang chọn", Anchor.NW, Theme.Muted, 8, true);
        _name = new CEntry(Board, rightCard + 16, top + 30, CardW - 32, 28, "Tên vị trí", () => { });
        for (var i = 0; i < 3; i++)
        {
            var x = rightCard + 16 + i * 75;
            Label(x, top + 78, "XYZ"[i].ToString());
            _xyz.Add(new CEntry(Board, x + 14, top + 64, 58, 28, "---", () => { }));
        }
        Label(rightCard + 16 + 3 * 75 + 10, top + 78, "Góc nhìn");
        _rotEntry = new CEntry(Board, rightCard + 16 + 3 * 75 + 64, top + 64, 108, 28, "---", () => { });

        const double row = top + ButtonsDy;
        _add = new CButton(Board, left, row, 150, 32, "+ Lưu vị trí của tôi", AddPlace, Theme.SuccessFill, Theme.SuccessHover, Colors.White, 16, 9);
        _update = new CButton(Board, left + 160, row, 105, 32, "Lưu sửa đổi", UpdatePlace, Theme.SecondaryFill, Theme.SecondaryHover, Colors.White, 16, 9);
        _delete = new CButton(Board, left + 275, row, 95, 32, "Xoá vị trí", DeletePlaces, Theme.DangerButtonFill, Theme.DangerButtonHover, Colors.White, 16, 9);
        _confirm = new CButton(Board, left + 380, row, 145, 32, "Dịch chuyển ngay", Confirm, Theme.AccentFill, Theme.AccentHover, Colors.White, 16, 9);
        new CButton(Board, left + 535, row, 105, 32, "↻ Đồng bộ", () =>
        {
            Note("Đang đồng bộ với máy chủ...", Theme.Accent);
            Sync(true, true);
        }, Theme.SecondaryFill, Theme.SecondaryHover, Colors.White, 16, 9);
        Board.Text(right - 228, row + 16, "Lọc bản đồ:", Anchor.E, Theme.Muted, 8, true);
        _maps = new CCombo(Board, right - 220, row, 220, 32, [AllMaps], () =>
        {
            _filter = _maps.Value.Length > 0 ? _maps.Value : AllMaps;
            ShowPlaces();
        });
        _maps.Select(0);
        var rows = (int)((bottom - tableTop - TableSpare) / RowH);
        _table = new CTable(Board, left + 16, tableTop + 16, right - left - 32,
        [
            new("badge", "Nguồn", 80, Anchor.Center), new("name", "Tên vị trí", 185), new("map", "Bản đồ", 130), new("x", "X", 75, Anchor.Center),
            new("y", "Y", 65, Anchor.Center), new("z", "Z", 75, Anchor.Center), new("rot", "Góc nhìn nhân vật", 145, Anchor.Center), new("distance", "Cách đây", 90, Anchor.Center),
        ], "name", rows, SelectMode.Extended);
        _table.SelectionChanged += ShowSelected;
        _count = Board.Text(left + 16, bottom - 14, "", Anchor.W, Theme.Muted, 8);
        _note = Board.Text(right - 16, bottom - 14, Hint, Anchor.E, Theme.Dim, 8);
        Reload();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        timer.Tick += (_, _) => CheckTracking();
        timer.Start();
        Sync(true);
    }

    public override void BuildFooter() => Board.Text(Layout.Left, Layout.FootTop + 26,
        "Vị trí [SERVER] được quản lý từ máy chủ. Vị trí [CỦA TÔI] được lưu trên máy của bạn và không tải lên máy chủ.", Anchor.W, Theme.Muted, 8);

    public override void OnClose() => StopTracking();

    public override void OnDevice(EmulatorDevice? device)
    {
        _device = device;
        StopTracking();
        SetStatus(device != null ? "Chưa kết nối" : "Chưa chọn tab giả lập", Theme.Muted);
    }

    public override void ShowMessage(string text, Color color) => SetStatus(text, color);

    private void SetStatus(string text, Color color) => (_ticker ??= new StatusTicker([_status], [_dot], 62)).Set(text, color);

    private void Note(string text, Color color) => _note.Set(text, color);

    // ----- Theo dõi vị trí + đồng bộ nền -----
    /// <summary>Mỗi 0,4 s: trang đang hiện + có tab thì theo dõi nhân vật (và đồng bộ máy chủ mỗi 30 s); không thì dừng theo dõi.</summary>
    private void CheckTracking()
    {
        var active = App.CurrentView == this;
        if (active) Sync(false);
        var wanted = active && _device != null;
        if (wanted && _tracker == null)
        {
            var tracker = _tracker = new TeleportController(_device!);
            tracker.Moved += place => App.Ui(() => { if (tracker == _tracker) OnPosition(place); });
            tracker.Status += (text, error) => App.Ui(() => { if (tracker == _tracker) SetStatus(text, error ? Theme.Warn : Theme.Accent); });
            tracker.Done += (ok, message) => App.Ui(() => Note(message, ok ? Theme.Hex("#4ade80") : Theme.Danger));
            tracker.StartTracking();
        }
        else if (!wanted && _tracker != null) StopTracking();
    }

    private void StopTracking()
    {
        _tracker?.StopTracking();
        _tracker = null;
        OnPosition(default);
    }

    private void OnPosition(PlayerPlace place)
    {
        _here = place;
        _map.Text = place.Map is { } map ? (map.Name.Length > 0 ? GameNames.Map(map.Name) : $"Bản đồ {map.Id}") : "---";
        _mapId.Text = place.Map is { } m ? $"mã bản đồ {m.Id}" : "";
        var values = place.Position is { } p ? new[] { (p.X, 2), (p.Y, 1), (p.Z, 2) } : null;
        for (var i = 0; i < 3; i++) _axes[i].Text = values == null ? "---" : values[i].Item1.ToString($"F{values[i].Item2}", CultureInfo.InvariantCulture);
        _rot.Text = place.Rotation is { } r ? Rotation([r.X, r.Y, r.Z, r.W]) : "---";
        _table.SetRows(_table.Rows.Select(row => _byId.TryGetValue(row.Key, out var saved) ? row with { Cells = [.. row.Cells[..^1], DistanceText(saved)] } : row).ToList());
        UpdateButtons();
    }

    /// <summary>Đồng bộ vị trí máy chủ ở nền (tự động tối đa 30 s / lần).</summary>
    private void Sync(bool force, bool user = false)
    {
        var now = Environment.TickCount64 / 1000.0;
        if (!force && now - _lastSync < 30) return;
        _lastSync = now;
        Task.Run(TelePlaces.SyncAsync).ContinueWith(task => App.Ui(() =>
        {
            if (!task.IsCompletedSuccessfully)
            {
                if (user) Note("Không thể kết nối máy chủ để đồng bộ", Theme.Warn);
                return;
            }
            if (task.Result)
            {
                Reload();
                Note($"Đã đồng bộ {_places.Count(p => !p.Editable)} vị trí Server", Theme.Ok);
            }
            else if (user) Note("Dữ liệu vị trí Server đã là mới nhất", Theme.Dim);
        }));
    }
}
