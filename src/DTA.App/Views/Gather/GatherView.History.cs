using System.Windows;
using DTA.App.Shell;
using DTA.Ui;
using DTA.Engine.Core;
using DTA.Game.Data;

namespace DTA.App.Views.Gather;

/// <summary>Tab "Lịch sử": tìm theo tên, lọc theo nền, bảng mới nhất lên đầu (chữ mang màu nền của món), xoá lịch sử.</summary>
public abstract partial class GatherView<TOptions>
{
    private List<HistoryEntry> _history = [];
    private CEntry _search = null!;
    private CCombo _grade = null!;
    private CTable _table = null!;
    private CanvasText _historyInfo = null!;

    private void BuildHistory()
    {
        const double left = Layout.Left, right = Layout.Right, top = Layout.ContentTop, bottom = Layout.ContentBottom, row = top + 28;
        Board.Cards((left, top, right - left, bottom - top));
        _search = new CEntry(Board, left + 16, row - 15, 320, 30, $"Tìm theo thứ {Verb} được hoặc món nhận được", ShowHistory);
        Board.Text(left + 354, row, "Nền", Anchor.W, Theme.Muted, 8, true);
        _grade = new CCombo(Board, left + 386, row - 15, 130, 30, ["Tất cả", .. GameNames.Grades.Values], ShowHistory);
        _grade.Select(0);
        new CButton(Board, right - 16 - 120, row - 14, 120, 28, "Xoá lịch sử", ClearHistory, Theme.DangerFill, Theme.DangerHover, Theme.Danger, 14, 8);
        _table = new CTable(Board, left + 16, top + 52, right - left - 32, Columns(), "name", 13, SelectMode.Browse);
        _historyInfo = Board.Text(left + 16, bottom - 14, "", Anchor.W, Theme.Muted, 8);
    }

    /// <summary>Cột bảng lịch sử.</summary>
    protected virtual IReadOnlyList<Column> Columns() =>
    [
        new("time", "Thời gian", 150), new("source", $"{char.ToUpper(Verb[0])}{Verb[1..]} được", 200), new("name", "Nhận được", 280),
        new("grade", "Nền", 90, Anchor.Center), new("price", "Giá bán", 90, Anchor.E),
    ];

    /// <summary>Chữ 1 dòng theo cột: lúc, vật thu hoạch, món nhận được, nền, giá. Dòng của bản cũ chỉ ghi tên vật thì món nhận được để trống.</summary>
    protected virtual string[] Cells(HistoryEntry entry)
    {
        var (source, name) = string.IsNullOrEmpty(entry.Source) ? (entry.Name ?? "", "") : (entry.Source, entry.Name ?? "");
        return [Formats.HistoryTime(entry.Time ?? ""), source, name, name.Length > 0 ? GradeName(entry.Grade) : "---", Formats.Price(entry.Price)];
    }

    protected static string GradeName(int? grade) => grade is { } g ? GameNames.Grades.GetValueOrDefault(g, "---") : "---";

    /// <summary>Dòng bảng; chữ mang màu nền của món nhận được.</summary>
    private TableRow Row(HistoryEntry entry, int index)
    {
        var color = !string.IsNullOrEmpty(entry.Name) && entry.Grade is { } g && Theme.Grades.TryGetValue(g, out var pair) ? pair.A : (System.Windows.Media.Color?)null;
        return new TableRow(index.ToString(), Cells(entry), color);
    }

    /// <summary>Vẽ lại bảng theo bộ lọc (mới nhất lên đầu).</summary>
    private void ShowHistory()
    {
        if (_table == null) return;
        var (text, grade) = (_search.Text.Trim(), _grade.Index);
        var rows = new List<TableRow>();
        for (var i = _history.Count - 1; i >= 0; i--)
        {
            var entry = _history[i];
            if (grade > 0 && entry.Grade != grade) continue;
            if (text.Length > 0 && !Fold.Matches(entry.Source ?? "", text) && !Fold.Matches(entry.Name ?? "", text)) continue;
            rows.Add(Row(entry, i));
        }
        _table.SetRows(rows);
        _historyInfo.Text = _history.Count == 0 ? $"Chưa có lượt {Verb} nào trong lịch sử"
            : rows.Count == _history.Count ? $"{rows.Count} lượt {Verb} trong lịch sử" : $"Đang hiện {rows.Count} / {_history.Count} lượt {Verb}";
    }

    /// <summary>Vừa thu hoạch xong 1 vật: tăng số đếm, ghi lịch sử.</summary>
    private void OnFind(HistoryEntry entry)
    {
        SetAll(_count, (++_found).ToString());
        _history.Add(entry);
        if (Device != null) DeviceStore.SaveHistory(Device.Serial, Key, _history);
        ShowHistory();
    }

    private void ClearHistory()
    {
        if (_history.Count == 0 || MessageBox.Show(App, $"Xoá toàn bộ {_history.Count} lượt {Verb} trong lịch sử của tab này?", "Xoá lịch sử", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        _history = [];
        if (Device != null) DeviceStore.SaveHistory(Device.Serial, Key, _history);
        ShowHistory();
    }
}
