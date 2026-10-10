using System.Windows;
using DTA.App.Shell;
using DTA.Ui;
using DTA.Engine.Core;
using DTA.Features.Fishing;
using DTA.Game.Data;

namespace DTA.App.Views.Fishing;

/// <summary>1 con trong lịch sử câu: lúc, ID cá, cỡ bóng, nền, ID món, tên, cỡ (cm), giá (như game ghi), giữ / bán.</summary>
public sealed record FishLog(string Time, int? FishId, int? Shadow, int? Grade, int? ItemId, string Name, int? Size, string? Price, string? Action)
{
    public static FishLog Of(CatchRecord r) => new(r.Time.ToString(HistoryEntry.TimeFormat), r.FishId, r.Shadow, r.Grade, r.ItemId, r.Name, r.Size, r.Price, r.Action);
}

/// <summary>Tab "Lịch sử" câu cá: tìm theo ID / tên, lọc bóng + nền, bảng mới nhất lên đầu, xoá lịch sử.</summary>
public sealed partial class FishingView
{
    private List<FishLog> _history = [];
    private CEntry _search = null!;
    private CCombo _shadowBox = null!, _gradeBox = null!;
    private CTable _table = null!;
    private CanvasText _historyInfo = null!;

    private void BuildHistory()
    {
        const double left = Layout.Left, right = Layout.Right, top = Layout.ContentTop, bottom = Layout.ContentBottom, row = top + 28;
        Board.Cards((left, top, right - left, bottom - top));
        _search = new CEntry(Board, left + 16, row - 15, 280, 30, "Tìm theo ID hoặc tên cá", ShowHistory);
        Board.Text(left + 314, row, "Bóng", Anchor.W, Theme.Muted, 8, true);
        _shadowBox = new CCombo(Board, left + 350, row - 15, 96, 30, ["Tất cả", .. Enumerable.Range(1, 7).Select(t => t.ToString())], ShowHistory);
        Board.Text(left + 464, row, "Nền", Anchor.W, Theme.Muted, 8, true);
        _gradeBox = new CCombo(Board, left + 496, row - 15, 130, 30, ["Tất cả", .. GameNames.Grades.Values], ShowHistory);
        _shadowBox.Select(0);
        _gradeBox.Select(0);
        new CButton(Board, right - 16 - 120, row - 14, 120, 28, "Xoá lịch sử", ClearHistory, Theme.DangerFill, Theme.DangerHover, Theme.Danger, 14, 8);
        _table = new CTable(Board, left + 16, top + 52, right - left - 32,
        [
            new("time", "Thời gian", 150), new("fish_id", "ID cá", 60, Anchor.Center), new("shadow", "Bóng", 46, Anchor.Center), new("grade", "Nền", 86, Anchor.Center),
            new("name", "Tên cá", 230), new("size", "Cỡ (cm)", 64, Anchor.Center), new("price", "Giá", 66, Anchor.Center), new("action", "Xử lý", 84, Anchor.Center),
        ], "name", 13, SelectMode.Browse);
        _historyInfo = Board.Text(left + 16, bottom - 14, "", Anchor.W, Theme.Muted, 8);
    }

    private static TableRow Row(FishLog e, int index) => new(index.ToString(),
    [
        Formats.HistoryTime(e.Time ?? ""), e.FishId is > 0 ? e.FishId.ToString()! : "---", e.Shadow is > 0 ? e.Shadow.ToString()! : "---",
        e.Grade is { } g ? GameNames.Grades.GetValueOrDefault(g, "---") : "---", e.Name ?? "", e.Size?.ToString() ?? "", e.Price ?? "",
        e.Action switch { "keep" => "Bảo quản", "sell" => "Bán nhanh", _ => "" },
    ], e.Grade is { } c && Theme.Grades.TryGetValue(c, out var pair) ? pair.A : null);

    /// <summary>Vẽ lại bảng theo bộ lọc (mới nhất lên đầu).</summary>
    private void ShowHistory()
    {
        if (_table == null) return;
        var (text, shadow, grade) = (_search.Text.Trim(), _shadowBox.Index, _gradeBox.Index);
        var rows = new List<TableRow>();
        for (var i = _history.Count - 1; i >= 0; i--)
        {
            var e = _history[i];
            if (shadow > 0 && e.Shadow != shadow) continue;
            if (grade > 0 && e.Grade != grade) continue;
            if (text.Length > 0 && !(e.FishId?.ToString() ?? "").Contains(text) && !Fold.Matches(e.Name ?? "", text)) continue;
            rows.Add(Row(e, i));
        }
        _table.SetRows(rows);
        _historyInfo.Text = _history.Count == 0 ? "Chưa có cá nào trong lịch sử"
            : rows.Count == _history.Count ? $"{rows.Count} con trong lịch sử" : $"Đang hiện {rows.Count} / {_history.Count} con";
    }

    /// <summary>Vừa câu được 1 con: tăng số đếm, ghi lịch sử, nạp lại danh sách vùng đã gặp.</summary>
    private void OnCatch(FishLog entry)
    {
        RefreshFakeZones();
        _count.Text = (++_caught).ToString();
        _history.Add(entry);
        if (Device != null) DeviceStore.SaveHistory(Device.Serial, "fishing", _history);
        ShowHistory();
    }

    private void ClearHistory()
    {
        if (_history.Count == 0 || MessageBox.Show(App, $"Xoá toàn bộ {_history.Count} con trong lịch sử của tab này?", "Xoá lịch sử", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        _history = [];
        if (Device != null) DeviceStore.SaveHistory(Device.Serial, "fishing", _history);
        ShowHistory();
    }
}
