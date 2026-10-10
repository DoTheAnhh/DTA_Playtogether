using System.Globalization;
using System.Windows;
using System.Windows.Media;
using DTA.Server.Store;
using DTA.Ui;

namespace DTA.ServerPanel;

/// <summary>Tab Quản lý key: tạo key, lọc theo trạng thái + tìm, bảng key, nhật ký, các nút thao tác.</summary>
public sealed partial class PanelWindow
{
    private static readonly (string Value, string Text)[] Filters = [("all", "Tất cả"), ("new", "Chưa kích hoạt"), ("active", "Đang hoạt động"), ("expired", "Hết hạn"), ("blocked", "Đã khoá")];
    private CEntry _count = null!, _days = null!, _devices = null!, _note = null!, _search = null!;
    private readonly Dictionary<string, CButton> _chips = [];
    private string _filter = "all";
    private CTable _keyTable = null!;
    private LogBox _log = null!;
    private CanvasText _info = null!;
    private Dictionary<string, KeyItem> _items = [];

    private static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private void BuildKeys()
    {
        _board.Round(18, 54, W - 36, 74, 8, new Fill(Card, null, Theme.Hex("#22304a")));
        CanvasText Label(double x, string text, Color? color = null, double size = 9, bool bold = false) => _board.Text(x, 76, text, Anchor.W, color ?? Theme.Muted, size, bold);
        Label(32, "TẠO KEY", Theme.Muted, 8, true);
        Label(100, "Số key");
        _count = new CEntry(_board, 146, 63, 50, 26, "1", () => { });
        _count.Set("1");
        Label(210, "Dùng được (ngày)");
        _days = new CEntry(_board, 318, 63, 56, 26, "30", () => { });
        _days.Set("30");
        Label(382, "0 = không giới hạn", Hint, 8);
        Label(500, "Số máy mở cùng lúc");
        _devices = new CEntry(_board, 626, 63, 44, 26, "1", () => { });
        _devices.Set("1");
        Label(684, "Ghi chú");
        _note = new CEntry(_board, 736, 63, 250, 26, "", () => { });
        Action(1000, 62, 110, "+  Tạo key", CreateKeys);
        _board.Text(32, 108, "Thời hạn tính từ lúc nhập key lần đầu. Tắt tool / mất điện thì chỗ tự trống sau ~2 phút, máy khác vào được.", Anchor.W, Hint, 8);
        for (var i = 0; i < Filters.Length; i++)
        {
            var value = Filters[i].Value;
            _chips[value] = new CButton(_board, 18 + i * 136, 138, 130, 28, Filters[i].Text, () => SetFilter(value), new Fill(Button), new Fill(ButtonHover), Theme.Muted, 6, 9);
        }
        _board.Text(900, 152, "Tìm key / ghi chú", Anchor.E, Theme.Muted, 9);
        _search = new CEntry(_board, 908, 139, W - 18 - 908, 28, "", ShowKeys);
        _keyTable = new CTable(_board, 18, 176, W - 36,
        [
            new("key", "Key", 300), new("note", "Ghi chú", 150), new("state", "Trạng thái", 110), new("activated", "Kích hoạt lúc", 125), new("expires", "Hết hạn", 190),
            new("devices", "Đang mở", 70, Anchor.Center), new("seen", "Dùng lần cuối", 150),
        ], "key", 12, SelectMode.Extended);
        _keyTable.RowActivated += _ => EditKey();
        _board.Round(18, 512, W - 36, 112, 8, new Fill(Card, null, Theme.Hex("#22304a")));
        _board.Text(30, 524, "NHẬT KÝ", Anchor.W, Theme.Muted, 8, true);
        _log = new LogBox(_board, 30, 536, W - 60, 82);
        const double y = 634;
        Action(18, y, 86, "Chép key", CopyKeys);
        Action(110, y, 70, "Sửa...", EditKey);
        var x = 186.0;
        foreach (var days in new[] { 1, 7, 30 })
        {
            Action(x, y, 76, $"+{days} ngày", () => Extend(days), Good);
            x += 82;
        }
        Action(x, y, 124, "Khoá / Mở khoá", ToggleBlock, Theme.Warn);
        Action(x + 130, y, 84, "Gỡ máy", ResetDevices, Theme.Warn);
        Action(x + 220, y, 60, "Xoá", DeleteKeys, Theme.Danger);
        _info = _board.Text(W - 18, y + 14, "", Anchor.E, Theme.Muted, 9);
        ReloadKeys();
        SetFilter("all");
    }

    /// <summary>Trạng thái 1 key: blocked / expired / new / active.</summary>
    private static string StateOf(KeyItem item, long now) => item.Blocked ? "blocked" : item.Expires > 0 && now >= item.Expires ? "expired" : item.Activated == 0 ? "new" : "active";

    private static readonly Dictionary<string, (string Name, Color Color)> States = new()
    {
        ["new"] = ("Chưa kích hoạt", Theme.Muted), ["active"] = ("Đang hoạt động", Theme.Text), ["expired"] = ("Hết hạn", Theme.Warn), ["blocked"] = ("Đã khoá", Theme.Danger),
    };

    private static string Moment(long seconds) => seconds > 0 ? DateTimeOffset.FromUnixTimeSeconds(seconds).LocalDateTime.ToString("dd/MM/yyyy HH:mm") : "---";

    private static string Span(long seconds)
    {
        seconds = Math.Max(0, seconds);
        var (days, hours, minutes) = (seconds / 86400, seconds % 86400 / 3600, seconds % 3600 / 60);
        return days > 0 ? $"{days} ngày {hours} giờ" : hours > 0 ? $"{hours} giờ {minutes} phút" : $"{minutes} phút";
    }

    public static string ExpiryText(KeyItem item, long now) => item.Expires > 0
        ? item.Expires > now ? $"{Moment(item.Expires)}  (còn {Span(item.Expires - now)})" : Moment(item.Expires)
        : item.Duration > 0 ? $"{Span(item.Duration)} từ lúc kích hoạt" : "Không giới hạn";

    private void ReloadKeys()
    {
        _items = _keys.Keys().ToDictionary(i => i.Key);
        ShowKeys();
    }

    private void SetFilter(string value)
    {
        _filter = value;
        foreach (var (name, chip) in _chips)
            chip.Restyle(name == value ? new Fill(Sky) : new Fill(Button), name == value ? new Fill(Sky) : new Fill(ButtonHover), name == value ? Ink : Theme.Muted);
        ShowKeys();
    }

    private void ShowKeys()
    {
        if (_keyTable == null) return;
        var now = Now;
        var text = _search.Text.Trim().ToLowerInvariant();
        var counts = Filters.ToDictionary(f => f.Value, _ => 0);
        var rows = new List<TableRow>();
        foreach (var item in _items.Values)
        {
            var state = StateOf(item, now);
            counts["all"]++;
            counts[state]++;
            if (_filter != "all" && state != _filter) continue;
            if (text.Length > 0 && !item.Key.Contains(text) && !item.Note.ToLowerInvariant().Contains(text)) continue;
            rows.Add(new TableRow(item.Key,
            [
                item.Key, item.Note, States[state].Name, Moment(item.Activated), ExpiryText(item, now), $"{KeyStore.Live(item.Devices, now).Count}/{item.MaxDevices}",
                Moment(item.LastSeen),
            ], States[state].Color));
        }
        foreach (var (value, title) in Filters) _chips[value].Text = $"{title} ({counts[value]})";
        _keyTable.SetRows(rows);
        _info.Text = $"Hiện {rows.Count}/{_items.Count} key";
    }

    private List<KeyItem> SelectedKeys() => _keyTable.Selected.Where(_items.ContainsKey).Select(k => _items[k]).ToList();

    private static int Number(CEntry entry, int min, int max, int fallback) => int.TryParse(entry.Text.Trim(), out var n) ? Math.Clamp(n, min, max) : fallback;

    private void CreateKeys()
    {
        var count = Number(_count, 1, 500, 1);
        var days = Number(_days, 0, 3650, 30);
        var devices = Number(_devices, 1, 100, 1);
        var note = _note.Text.Trim();
        if (note.Length > 200) note = note[..200];
        var keys = _keys.Create(count, note, days * 86400L, 0, devices);
        Note($"tạo {count} key ({days} ngày, {devices} máy): {string.Join(", ", keys.Select(k => k[..12] + "…"))}");
        Clipboard.SetText(string.Join("\n", keys));
        ReloadKeys();
        SetFilter("new");
        _keyTable.Select(keys);
    }

    private void CopyKeys()
    {
        var keys = SelectedKeys().Select(i => i.Key).ToList();
        if (keys.Count > 0) Clipboard.SetText(string.Join("\n", keys));
    }

    private void Extend(int days)
    {
        var items = SelectedKeys();
        if (items.Count == 0) return;
        var now = Now;
        foreach (var item in items)
        {
            var start = item.Expires > now ? item.Expires : now;
            _keys.Update(item.Key, new KeyChanges(Expires: start + days * 86400L, Activated: item.Activated > 0 ? item.Activated : now));
        }
        Note($"gia hạn +{days} ngày cho {items.Count} key");
        ReloadKeys();
    }

    private void ToggleBlock()
    {
        var items = SelectedKeys();
        if (items.Count == 0) return;
        foreach (var item in items) _keys.Update(item.Key, new KeyChanges(Blocked: !item.Blocked));
        Note($"đổi trạng thái khoá của {items.Count} key");
        ReloadKeys();
    }

    private void ResetDevices()
    {
        var items = SelectedKeys();
        if (items.Count == 0) return;
        foreach (var item in items) _keys.ResetDevices(item.Key);
        Note($"gỡ máy đang mở của {items.Count} key");
        ReloadKeys();
    }

    private void DeleteKeys()
    {
        var items = SelectedKeys();
        if (items.Count == 0) return;
        var name = items.Count == 1 ? $"key {items[0].Key[..12]}…" : $"{items.Count} key đang chọn";
        if (MessageBox.Show(this, $"Xoá hẳn {name}?\nNgười đang dùng key này sẽ bị ngắt kết nối.", "Xoá key", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        _keys.Delete(items.Select(i => i.Key));
        Note($"xoá {items.Count} key");
        ReloadKeys();
    }

    private void EditKey()
    {
        if (SelectedKeys() is not [var item]) return;
        if (KeyEditDialog.Ask(this, item) is not { } changes) return;
        _keys.Update(item.Key, changes);
        Note($"sửa key {item.Key[..12]}…");
        ReloadKeys();
    }

    internal static string Days(long seconds) => (seconds / 86400.0).ToString("0.##", CultureInfo.InvariantCulture);
}
