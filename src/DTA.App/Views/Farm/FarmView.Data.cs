using System.Globalization;
using DTA.Ui;
using DTA.Engine.Core;
using DTA.Features.Farm;

namespace DTA.App.Views.Farm;

/// <summary>1 món trong lịch sử nông trại: lúc, "Thu hoạch" / "Bán", ID, tên, nền, kg, cờ biến thể.</summary>
public sealed record FarmLog(string Time, string Action, int? Id, string Name, int? Grade, float? Kg, int? Mutations)
{
    public static FarmLog Of(FarmRecord r) => new(r.Time.ToString(HistoryEntry.TimeFormat), r.Action, r.Id, r.Name, r.Grade, r.Kg, r.Mutations);
}

/// <summary>Đưa số liệu nông trại lên các thẻ + bảng (sắp theo cấp nền Trắng -> VVIP), lịch sử thu hoạch / bán.</summary>
public sealed partial class FarmView
{
    private static readonly CultureInfo Dots = new("vi-VN");

    private static string Money(int value) => value > 0 ? value.ToString("#,0", Dots) : "---";

    private static string Clock(double seconds)
    {
        var s = Math.Max((int)seconds, 0);
        return s >= 3600 ? $"{s / 3600}:{s / 60 % 60:00}:{s % 60:00}" : $"{s / 60}:{s % 60:00}";
    }

    private string NameOf(int id) => _info?.Items.GetValueOrDefault(id)?.Name is { Length: > 0 } name ? name : id.ToString();

    private int GradeOf(int id) => _info?.Items.GetValueOrDefault(id)?.Grade ?? 0;

    private bool Shows(int id, HashSet<int> grades, string search) =>
        (grades.Count == 0 || grades.Contains(GradeOf(id))) && (search.Length == 0 || NameOf(id).Contains(search, StringComparison.CurrentCultureIgnoreCase));

    /// <summary>Dòng bảng chọn được: cột 1 = "● tên" + ô vật phẩm, cột 2 = [✔] Bật / [  ] Tắt; nền xanh khi đang chọn.</summary>
    private TableRow Pick(int id, string kind, bool locked, bool selected, params string[] rest) => new(id.ToString(),
        [$"  {(selected ? '●' : ' ')}  {NameOf(id)}", selected ? "[✔] Bật" : "[  ] Tắt", .. rest], Grade(id), selected ? SelOn : SelOff, Icons.ItemBadge(GradeOf(id), kind, locked));

    private System.Windows.Media.Color? Grade(int id) => Theme.Grades.TryGetValue(GradeOf(id), out var c) ? c.A : null;

    private void ShowInfo()
    {
        var info = _info;
        if (info == null)
        {
            foreach (var text in new[] { _restock, _seedBag, _crops, _ripe, _growing, _gear }) text.Text = "---";
            _next.Text = "";
            foreach (var table in new[] { _shop, _seeds, _gearTable, _fruits, _harvestTypes }) table.SetRows([]);
            ShowSale();
            return;
        }
        var bag = info.Bag;
        var seeds = bag.GetValueOrDefault(FarmIds.SeedGroup, []).GroupBy(i => i.ItemId).ToDictionary(g => g.Key, g => (Count: g.Sum(i => i.Count), Locked: g.Any(i => i.Locked)));
        var gears = bag.GetValueOrDefault(FarmIds.GearGroup, []).GroupBy(i => i.ItemId).ToDictionary(g => g.Key, g => g.Sum(i => i.Count));
        int Order(int id) => GradeOf(id) is > 0 and var g ? g : 1;

        if (info.Shop is { } shop)
        {
            _restock.Text = shop.Restock > 0 ? $"Tự nhập hàng mới sau {Clock(shop.Restock - FarmIds.Now)}" : "Chưa rõ giờ nhập hàng";
            _shop.SetRows(shop.Goods.OrderBy(s => Order(s.ItemId)).ThenBy(s => NameOf(s.ItemId)).Where(s => Shows(s.ItemId, _options.ShopGrades, _options.ShopSearch))
                .Select(s => Pick(s.ItemId, "seed", false, _options.BuySeeds.Contains(s.ItemId), s.Stock > 0 ? s.Stock.ToString() : "Hết hàng", Money(s.FlowerPrice),
                    Money(s.DiamondPrice), seeds.GetValueOrDefault(s.ItemId).Count.ToString())).ToList());
        }
        else
        {
            _restock.Text = "Chưa đọc được cửa hàng hạt";
            _shop.SetRows([]);
        }
        _seedBag.Text = $"Trong túi: {seeds.Values.Sum(s => s.Count)} hạt ({seeds.Count} loại)";

        _crops.Text = $"{info.Crops.Count} / {(info.MaxPlants > 0 ? info.MaxPlants : "?")}";
        var planted = info.Crops.GroupBy(c => c.SeedId).ToDictionary(g => g.Key, g => g.Count());
        _seeds.SetRows(seeds.OrderBy(p => Order(p.Key)).ThenByDescending(p => p.Value.Count).ThenBy(p => NameOf(p.Key))
            .Where(p => Shows(p.Key, _options.PlantGrades, _options.PlantSearch))
            .Select(p => Pick(p.Key, "seed", p.Value.Locked, _options.PlantSeeds.Contains(p.Key), p.Value.Count.ToString(), p.Value.Locked ? "Đã khoá" : "Sẵn sàng",
                planted.GetValueOrDefault(p.Key).ToString())).ToList());

        _ripe.Text = info.Ripe.Count.ToString();
        _growing.Text = info.Growing.Count.ToString();
        var soon = info.Growing.Count > 0 ? info.Growing.Min(f => f.End) : 0;
        _next.Text = soon > 0 ? $"Quả kế chín sau {Clock(soon - info.Time)}" : "";
        var ripe = info.Ripe.GroupBy(f => f.ItemId).ToDictionary(g => g.Key, g => g.Count());
        var growing = info.Growing.GroupBy(f => f.ItemId).ToDictionary(g => g.Key, g => g.Count());
        _harvestTypes.SetRows(ripe.Keys.Union(growing.Keys).OrderBy(Order).ThenByDescending(ripe.GetValueOrDefault).ThenByDescending(growing.GetValueOrDefault).ThenBy(NameOf)
            .Where(id => Shows(id, _options.HarvestGrades, _options.HarvestSearch))
            .Select(id => Pick(id, "fruit", false, _options.HarvestFruits.Contains(id), ripe.GetValueOrDefault(id).ToString(), growing.GetValueOrDefault(id).ToString())).ToList());

        _gear.Text = $"{gears.Values.Sum()} món";
        _gearTable.SetRows(gears.OrderBy(p => Order(p.Key)).ThenByDescending(p => p.Value).ThenBy(p => NameOf(p.Key))
            .Select(p => new TableRow(p.Key.ToString(), [$"  {NameOf(p.Key)}", p.Value.ToString()], Grade(p.Key), null, Icons.ItemBadge(GradeOf(p.Key), "gear", false))).ToList());
        ShowSale();
        if (!info.InFarm) SetStatus("Nhân vật không ở nông trại - số liệu cây / quả có thể trống", Theme.Warn);
    }

    /// <summary>Bảng nông sản trong túi + số món sẽ bán theo tuỳ chọn hiện tại (tối đa 100 / lượt) + đếm theo nền.</summary>
    private void ShowSale()
    {
        var fruits = _info?.Bag.GetValueOrDefault(FarmIds.FruitGroup, []) ?? [];
        int Order(int id) => GradeOf(id) is > 0 and var g ? g : 1;
        _fruits.SetRows(fruits.GroupBy(f => f.ItemId).OrderBy(g => Order(g.Key)).ThenByDescending(g => g.Count()).ThenBy(g => NameOf(g.Key))
            .Where(g => Shows(g.Key, _options.SellGrades, _options.SellSearch))
            .Select(g => Pick(g.Key, "fruit", g.Any(f => f.Locked), _options.SellFruits.Contains(g.Key), g.Count().ToString())).ToList());
        var selling = fruits.Where(f => _options.Sellable(f, GradeOf(f.ItemId))).ToList();
        _saleCount.Text = _info != null ? $"Sẽ bán {Math.Min(selling.Count, 100)} / {fruits.Count} món" : "";
        string[] symbols = ["", "⚪", "🟢", "🔵", "🟣", "⭐"];
        _saleGrades.Text = string.Join(" ", selling.Take(100).GroupBy(f => Order(f.ItemId)).OrderBy(g => g.Key).Select(g => $"{symbols[Math.Clamp(g.Key, 1, 5)]} {g.Count()}"));
    }

    private string MutationText(int flags)
    {
        var table = _info?.Mutations ?? [];
        var names = Enumerable.Range(1, 32).Where(id => (flags >> (id - 1) & 1) != 0).Select(id => table.TryGetValue(id, out var m) ? m.Name : $"#{id}").ToList();
        return names.Count > 0 ? string.Join(", ", names) : "---";
    }

    private void ShowHistory()
    {
        TableRow Row(FarmLog e, int i, params string[] cells) => new(i.ToString(), cells, e.Grade is { } g && Theme.Grades.TryGetValue(g, out var c) ? c.A : null, SelOff);
        var newest = Enumerable.Reverse(_history).ToList();
        _reaped.SetRows(newest.Where(e => e.Action == "Thu hoạch").Select((e, i) =>
            Row(e, i, Formats.HistoryTime(e.Time ?? ""), e.Name ?? "", e.Kg?.ToString("0.##", CultureInfo.InvariantCulture) ?? "", MutationText(e.Mutations ?? 0))).ToList());
        _sold.SetRows(newest.Where(e => e.Action == "Bán").Select((e, i) =>
            Row(e, i, Formats.HistoryTime(e.Time ?? ""), e.Name ?? "", e.Kg?.ToString("0.##", CultureInfo.InvariantCulture) ?? "")).ToList());
    }
}
