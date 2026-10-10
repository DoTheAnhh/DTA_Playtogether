using System.Globalization;
using System.Windows;
using DTA.Server.Store;
using DTA.Ui;

namespace DTA.ServerPanel;

/// <summary>Tab Quản lý vị trí TELE: thêm, lọc bản đồ, tìm, làm mới, bảng vị trí, sửa / bật tắt / xoá.</summary>
public sealed partial class PanelWindow
{
    private const string AllMaps = "Tất cả bản đồ";
    private CCombo _teleMap = null!;
    private CEntry _teleSearch = null!;
    private CanvasText _teleVersion = null!, _teleCount = null!;
    private CTable _teleTable = null!;
    private Dictionary<string, TelePosition> _positions = [];

    private void BuildTele()
    {
        _board.Round(18, 54, W - 36, 56, 8, new Fill(Card, null, Theme.Hex("#22304a")));
        Action(32, 68, 150, "+ Thêm vị trí mới", () => EditTele(null), Good);
        _board.Text(200, 82, "Lọc Bản đồ:", Anchor.W, Theme.Muted, 9);
        _teleMap = new CCombo(_board, 280, 68, 170, 28, [AllMaps, "Plaza", "Khu nghỉ dưỡng", "Khu cắm trại", "Khu trung tâm"], ShowTele);
        _teleMap.Select(0);
        _board.Text(466, 82, "Tìm kiếm:", Anchor.W, Theme.Muted, 9);
        _teleSearch = new CEntry(_board, 530, 68, 200, 28, "", ShowTele);
        Action(744, 68, 100, "⟳ Làm mới", ReloadTele);
        _teleVersion = _board.Text(W - 32, 82, "", Anchor.E, Theme.Muted, 8, true);
        _teleTable = new CTable(_board, 18, 120, W - 36,
        [
            new("id", "Mã ID", 150), new("name", "Tên vị trí", 190), new("map", "Bản đồ", 130), new("xyz", "Tọa độ (X, Y, Z)", 200), new("rot", "Góc nhìn nhân vật", 140),
            new("status", "Trạng thái", 90, Anchor.Center), new("ver", "Ver", 50, Anchor.Center),
        ], "name", 19, SelectMode.Browse);
        _teleTable.RowActivated += _ => EditSelectedTele();
        const double y = 630;
        Action(18, y, 106, "Sửa vị trí...", EditSelectedTele);
        Action(130, y, 126, "Bật / Tắt vị trí", ToggleTele, Theme.Warn);
        Action(262, y, 96, "Xoá vị trí", DeleteTele, Theme.Danger);
        _teleCount = _board.Text(W - 18, y + 14, "", Anchor.E, Theme.Muted, 9);
    }

    private void ReloadTele()
    {
        _positions = _tele.All().ToDictionary(p => p.Id);
        _teleVersion.Text = $"Version: {_tele.Version} (Authoritative)  |  Đồng bộ realtime cho Client";
        ShowTele();
    }

    private void ShowTele()
    {
        if (_teleTable == null) return;
        var query = _teleSearch.Text.Trim().ToLowerInvariant();
        var filter = _teleMap.Value is { Length: > 0 } v ? v : AllMaps;
        var rows = _positions.Values.OrderBy(p => p.OrderIndex).ThenBy(p => p.MapId).ThenBy(p => p.Name)
            .Where(p => filter == AllMaps || MapName(p).Contains(filter, StringComparison.CurrentCultureIgnoreCase))
            .Where(p => query.Length == 0 || p.Name.ToLowerInvariant().Contains(query) || p.Id.ToLowerInvariant().Contains(query))
            .Select(p => new TableRow(p.Id,
            [
                p.Id, p.Name, MapName(p), string.Create(CultureInfo.InvariantCulture, $"X={p.X:F2}, Y={p.Y:F2}, Z={p.Z:F2}"),
                p.Rotation.Length > 0 ? $"[{string.Join(", ", p.Rotation.Select(r => r.ToString(CultureInfo.InvariantCulture)))}]" : "---",
                p.Enabled ? "BẬT" : "TẮT", p.Version.ToString(),
            ], p.Enabled ? Theme.Text : Theme.Hex("#6b7280"))).ToList();
        _teleTable.SetRows(rows);
        _teleCount.Text = $"Hiển thị {rows.Count}/{_positions.Count} vị trí ({_positions.Values.Count(p => p.Enabled)} đang BẬT)";
    }

    private static string MapName(TelePosition p) => p.MapName.Length > 0 ? p.MapName : TeleStore.MapNames.GetValueOrDefault(p.MapId, p.MapId.ToString());

    private TelePosition? SelectedTele(string action)
    {
        if (_teleTable.Selected.FirstOrDefault() is { } id && _positions.TryGetValue(id, out var position)) return position;
        MessageBox.Show(this, $"Vui lòng chọn 1 vị trí {action}", "Chọn vị trí");
        return null;
    }

    private void EditSelectedTele()
    {
        if (SelectedTele("trong bảng để sửa") is { } position) EditTele(position);
    }

    private void EditTele(TelePosition? position)
    {
        if (TeleEditDialog.Ask(this, position, _tele)) ReloadTele();
    }

    private void ToggleTele()
    {
        if (SelectedTele("để Bật/Tắt") is not { } position) return;
        _tele.Toggle(position.Id, !position.Enabled);
        ReloadTele();
    }

    private void DeleteTele()
    {
        if (SelectedTele("để xoá") is not { } position) return;
        if (MessageBox.Show(this, $"Bạn có chắc muốn xoá vị trí '{position.Name}'?", "Xoá vị trí", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        _tele.Delete(position.Id);
        ReloadTele();
    }
}
