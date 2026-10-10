using System.Text.RegularExpressions;
using DTA.App.Shell;
using DTA.Ui;
using DTA.Engine.Core;
using DTA.Features.Fishing;

namespace DTA.App.Views.Fishing;

/// <summary>Tuỳ chọn câu: gói bán nhanh, giữ lại khi bán nhanh, lọc cá, vùng câu, khoá POV / cá cắn nhanh.</summary>
public sealed partial class FishingView
{
    private FishingOptions _options = new();
    private CToggle _keepVariant = null!, _keepMutant = null!, _filter = null!, _zoneTele = null!, _fakeZone = null!;
    private CEntry _keepIds = null!, _wantedIds = null!, _fakeZoneEntry = null!;
    private CButton _clearKeep = null!, _clearFilter = null!, _keepVariants = null!;
    private List<CanvasText> _keepLabels = [], _filterLabels = [];
    private Dictionary<int, CChip> _keepShadows = [], _keepGrades = [], _wantedShadows = [], _wantedGrades = [];
    private CCombo _zone = null!, _fakeZoneBox = null!;
    private List<string> _zoneKeys = [];
    private List<int> _fakeZoneIds = [];

    /// <summary>Đổi tuỳ chọn: lưu ngay; bot đọc thẳng object này nên có tác dụng tức thì.</summary>
    private void Change(Action<FishingOptions> change)
    {
        change(_options);
        if (Device != null) DeviceStore.SaveOptions(Device.Serial, "fishing", _options);
    }

    private static HashSet<int> Numbers(string text) => Regex.Matches(text, @"\d+").Select(m => int.TryParse(m.Value, out var n) ? n : 0).Where(n => n > 0).ToHashSet();

    private static void Toggle(HashSet<int> set, int value, bool on)
    {
        if (on) set.Add(value);
        else set.Remove(value);
    }

    /// <summary>Thẻ "giữ lại khi bán nhanh": dòng trên biến thể / đột biến / ID + xoá; dòng dưới cỡ bóng + nền.</summary>
    private void BuildKeep()
    {
        const double left = Layout.Left, right = Layout.Right, line1 = KeepTop + 26, line2 = KeepTop + 64;
        _keepVariant = new CToggle(Board, left + 16, line1 - 9, "Giữ cá biến thể", on => KeepChange(o => o.KeepVariant = on));
        _keepVariants = VariantButton(left + 155, line1 - 15, 130, "Giữ cá biến thể", () => _options.KeepVariantTypes, set => KeepChange(o => o.KeepVariantTypes = set));
        _keepMutant = new CToggle(Board, left + 365, line1 - 9, "Giữ cá đột biến", on => KeepChange(o => o.KeepMutant = on));
        const double clearX = right - 16 - 124;
        _keepIds = new CEntry(Board, left + 555, line1 - 15, clearX - 12 - (left + 555), 30, IdHint, () => KeepChange(o => o.KeepIds = Numbers(_keepIds.Text)));
        _clearKeep = new CButton(Board, clearX, line1 - 14, 124, 28, "Xoá điều kiện giữ", ClearKeep, Theme.GhostFill, Theme.GhostHover, Theme.Text, 14, 8);
        _keepLabels = [Label(left + 510, line1, "Giữ ID"), Label(left + 16, line2, "Giữ bóng"), Label(left + GradeLabelDx, line2, "Giữ nền")];
        (_keepShadows, _keepGrades) = ChipRows(line2, (t, on) => KeepChange(o => Toggle(o.KeepShadows, t, on)), (g, on) => KeepChange(o => Toggle(o.KeepGrades, g, on)));
        Board.Text(right - 14, line2, AllHint, Anchor.E, Theme.Dim, 7);
    }

    /// <summary>Thẻ lọc cá: dòng trên công tắc + ID + xoá; dòng dưới cỡ bóng + nền.</summary>
    private void BuildFilter()
    {
        const double left = Layout.Left, right = Layout.Right, line1 = FilterTop + 30, line2 = FilterTop + 70, clearX = right - 16 - 124;
        _filter = new CToggle(Board, left + 16, line1 - 9, "Lọc cá", on => FilterChange(o => o.FilterOn = on));
        _filterLabels = [Label(left + 510, line1, "ID cá"), Label(left + 16, line2, "Bóng"), Label(left + GradeLabelDx, line2, "Nền")];
        _wantedIds = new CEntry(Board, left + 555, line1 - 15, clearX - 12 - (left + 555), 30, IdHint, () => FilterChange(o => o.WantedIds = Numbers(_wantedIds.Text)));
        _clearFilter = new CButton(Board, clearX, line1 - 14, 124, 28, "Xoá điều kiện lọc", ClearFilter, Theme.GhostFill, Theme.GhostHover, Theme.Text, 14, 8);
        (_wantedShadows, _wantedGrades) = ChipRows(line2, (t, on) => FilterChange(o => Toggle(o.WantedShadows, t, on)), (g, on) => FilterChange(o => Toggle(o.WantedGrades, g, on)));
        Board.Text(right - 14, line2, AllHint, Anchor.E, Theme.Dim, 7);
    }

    private const string AllHint = "(Không chọn mặc định tất cả)", IdHint = "Không nhập = tất cả (vd: 42, 1687)";

    /// <summary>Nút "✨ Biến thể": bấm mở hộp chọn nhiều; đang chọn ít nhất 1 biến thể thì nút sáng màu (không chọn = tất cả).</summary>
    private CButton VariantButton(double x, double y, double w, string title, Func<HashSet<int>> current, Action<HashSet<int>> apply)
    {
        CButton button = null!;
        button = new CButton(Board, x, y, w, 30, "✨  Biến thể", () =>
        {
            if (PickDialog.Ask(App, title, FishMutations.All.Select(m => (m.Type, m.Name)).ToList(), current()) is not { } picked) return;
            apply(picked);
            PaintVariant(button, picked);
        }, Theme.GhostFill, Theme.GhostHover, Theme.Text, 14, 8);
        return button;
    }

    /// <summary>Màu nút biến thể theo lựa chọn: có chọn = màu nhấn, trống (tất cả) = nút mờ.</summary>
    private static void PaintVariant(CButton button, HashSet<int> picked)
    {
        if (picked.Count > 0) button.Restyle(Theme.AccentFill, Theme.AccentHover, System.Windows.Media.Colors.White);
        else button.Restyle(Theme.GhostFill, Theme.GhostHover, Theme.Text);
    }

    /// <summary>Thẻ vùng câu: dịch chuyển tới vùng câu đặc biệt; giả ID vùng câu.</summary>
    private void BuildZones()
    {
        const double left = Layout.Left, line1 = ZoneTop + 28, line2 = ZoneTop + 68;
        _zoneTele = new CToggle(Board, left + 16, line1 - 9, "Dịch chuyển tới vùng câu", OnZoneTele);
        Label(left + 250, line1, "Vùng");
        _zone = new CCombo(Board, left + 300, line1 - 15, 270, 30, ZoneChoices().Select(z => z.Name), OnZone, true);
        Board.Text(left + 586, line1, "(Đứng cách tâm vùng đúng tầm quăng, mặt quay vào vùng)", Anchor.W, Theme.Dim, 8);
        _fakeZone = new CToggle(Board, left + 16, line2 - 9, "Giả vùng câu", OnFakeZoneToggle);
        Label(left + 250, line2, "ID vùng");
        _fakeZoneBox = new CCombo(Board, left + 300, line2 - 15, 270, 30, [], OnFakeZone, true);
        _fakeZoneEntry = new CEntry(Board, left + 580, line2 - 15, 130, 30, "hoặc gõ ID vùng", OnFakeZoneTyped);
        Board.Text(left + 722, line2, "(Ghi đè mỗi lần quăng, không cần vùng trên map)", Anchor.W, Theme.Dim, 8);
    }

    /// <summary>Lựa chọn ô Vùng: (tên tài nguyên | bất kỳ, "mã - tên").</summary>
    private static List<(string Key, string Name)> ZoneChoices() =>
        [(FishingOptions.AnyZone, "Vùng câu đặc biệt gần nhất"), .. ZoneSpot.Known.OrderBy(z => z.Value.Id).Select(z => (z.Key, $"{z.Value.Id} - {z.Value.Name}"))];

    /// <summary>Đưa tuỳ chọn vừa nạp lên giao diện.</summary>
    private void ShowOptions()
    {
        _package.Set(_options.HasPackage);
        _action.SetLocked(true, !_options.HasPackage);
        _action.Set(_options.Sell);
        _repair.Set(_options.AutoRepair);
        ShowKeep();
        _filter.Set(_options.FilterOn);
        ShowFilter();
        ShowZones();
    }

    private void OnPackage(bool has)
    {
        Change(o => o.HasPackage = has);
        _action.SetLocked(true, !has);
    }

    // ----- Giữ lại khi bán nhanh -----
    private void KeepChange(Action<FishingOptions> change)
    {
        Change(change);
        UpdateKeep();
    }

    private void ShowKeep()
    {
        _keepVariant.Set(_options.KeepVariant);
        PaintVariant(_keepVariants, _options.KeepVariantTypes);
        _keepMutant.Set(_options.KeepMutant);
        _keepIds.Set(string.Join(", ", _options.KeepIds.Order()));
        foreach (var (tier, chip) in _keepShadows) chip.Set(_options.KeepShadows.Contains(tier));
        foreach (var (grade, chip) in _keepGrades) chip.Set(_options.KeepGrades.Contains(grade));
        UpdateKeep();
    }

    /// <summary>Các ô "Giữ ..." chỉ có nghĩa khi đang chọn Bán nhanh.</summary>
    private void UpdateKeep()
    {
        var selling = _options.Sell;
        _keepVariant.SetEnabled(selling);
        _keepMutant.SetEnabled(selling);
        _keepVariants.SetEnabled(selling);
        _keepIds.SetEnabled(selling);
        foreach (var chip in _keepShadows.Values.Concat(_keepGrades.Values)) chip.SetEnabled(selling);
        _clearKeep.SetEnabled(selling && _options.HasKeepConditions);
        foreach (var label in _keepLabels) label.Color = selling ? Theme.Muted : Theme.Dim;
    }

    private void ClearKeep()
    {
        Change(o =>
        {
            o.KeepVariant = o.KeepMutant = false;
            o.KeepVariantTypes = [];
            (o.KeepIds, o.KeepShadows, o.KeepGrades) = ([], [], []);
        });
        ShowKeep();
    }

    // ----- Lọc cá -----
    private void FilterChange(Action<FishingOptions> change)
    {
        Change(change);
        UpdateFilter();
    }

    private void ShowFilter()
    {
        _wantedIds.Set(string.Join(", ", _options.WantedIds.Order()));
        foreach (var (tier, chip) in _wantedShadows) chip.Set(_options.WantedShadows.Contains(tier));
        foreach (var (grade, chip) in _wantedGrades) chip.Set(_options.WantedGrades.Contains(grade));
        UpdateFilter();
    }

    /// <summary>Điều kiện lọc chỉ bấm được khi công tắc Lọc cá bật.</summary>
    private void UpdateFilter()
    {
        var on = _options.FilterOn;
        _wantedIds.SetEnabled(on);
        foreach (var chip in _wantedShadows.Values.Concat(_wantedGrades.Values)) chip.SetEnabled(on);
        _clearFilter.SetEnabled(on && (_options.WantedIds.Count > 0 || _options.WantedShadows.Count > 0 || _options.WantedGrades.Count > 0));
        foreach (var label in _filterLabels) label.Color = on ? Theme.Muted : Theme.Dim;
    }

    private void ClearFilter()
    {
        Change(o => (o.WantedIds, o.WantedShadows, o.WantedGrades) = ([], [], []));
        ShowFilter();
    }

    // ----- Vùng câu -----
    private void ShowZones()
    {
        _zoneTele.Set(_options.ZoneTele);
        var choices = ZoneChoices();
        if (choices.All(c => c.Key != _options.ZoneFocus)) choices.Add((_options.ZoneFocus, ZoneSpot.NameOf(_options.ZoneFocus)));
        _zoneKeys = choices.Select(c => c.Key).ToList();
        _zone.SetValues(choices.Select(c => c.Name));
        _zone.Select(_zoneKeys.IndexOf(_options.ZoneFocus));
        _fakeZone.Set(_options.FakeZoneOn);
        RefreshFakeZones();
    }

    /// <summary>Nạp lại danh sách ID vùng đã gặp (bot vừa câu ở vùng mới thì có thêm mục).</summary>
    private void RefreshFakeZones()
    {
        var zones = (_cache != null ? FishingBot.KnownZones(_cache) : [])
            .Select(z => int.TryParse(z.Key, out var id) ? (Id: id, Label: $"{id} - {z.Value}") : (Id: 0, Label: ""))
            .Where(z => z.Id != 0).OrderBy(z => z.Id).ToList();
        if (_options.FakeZone != 0 && zones.All(z => z.Id != _options.FakeZone)) zones.Add((_options.FakeZone, $"{_options.FakeZone} - (đã chọn trước đó)"));
        _fakeZoneIds = zones.Select(z => z.Id).ToList();
        _fakeZoneBox.SetValues(zones.Select(z => z.Label));
        if (_fakeZoneIds.Contains(_options.FakeZone)) _fakeZoneBox.Select(_fakeZoneIds.IndexOf(_options.FakeZone));
        else _fakeZoneBox.SetText(zones.Count == 0 ? "Chưa có - gõ ID bên cạnh" : "Chọn vùng...");
    }

    private void OnZone()
    {
        if (_zone.Index >= 0 && _zone.Index < _zoneKeys.Count) Change(o => o.ZoneFocus = _zoneKeys[_zone.Index]);
    }

    private void OnZoneTele(bool on)
    {
        if (on && !Risk.IsAccepted(RiskFeature.Teleport))
        {
            if (!RiskDialog.Ask(App))
            {
                _zoneTele.Set(false);
                return;
            }
            Risk.Set(RiskFeature.Teleport, true);
        }
        Change(o => o.ZoneTele = on);
    }

    private void OnFakeZone()
    {
        if (_fakeZoneBox.Index >= 0 && _fakeZoneBox.Index < _fakeZoneIds.Count) Change(o => o.FakeZone = _fakeZoneIds[_fakeZoneBox.Index]);
    }

    /// <summary>Gõ tay ID vùng: dùng ngay làm vùng giả + thêm vào danh sách (nhãn "Tự nhập") để lần sau chọn lại.</summary>
    private void OnFakeZoneTyped()
    {
        if (Numbers(_fakeZoneEntry.Text).FirstOrDefault() is not (> 0 and var id)) return;
        if (_cache != null)
        {
            var known = FishingBot.KnownZones(_cache);
            if (known.TryAdd(id.ToString(), "Tự nhập")) _cache.Put(FishingBot.ZonesKey, known);
        }
        Change(o => o.FakeZone = id);
        RefreshFakeZones();
    }

    private void OnFakeZoneToggle(bool on)
    {
        if (on && _options.FakeZone == 0)
        {
            _fakeZone.Set(false);
            SetStatus("Chọn hoặc gõ 1 ID vùng câu trước rồi mới bật Giả vùng câu", Theme.Warn);
            return;
        }
        Change(o => o.FakeZoneOn = on);
    }

    // ----- Khoá POV / cá cắn nhanh -----
    private void OnPov(bool on)
    {
        if (Device == null)
        {
            _pov?.Set(false);
            return;
        }
        _pov?.Set(FishingAssist.For(Device.Serial).SetPov(on));
    }

    private void OnFastBite(bool on)
    {
        if (Device == null)
        {
            _fastBite?.Set(false);
            return;
        }
        if (on && !Risk.IsAccepted(RiskFeature.FastBite))
        {
            if (!RiskDialog.Ask(App))
            {
                _fastBite?.Set(false);
                return;
            }
            Risk.Set(RiskFeature.FastBite, true);
        }
        _fastBite?.Set(FishingAssist.For(Device.Serial).SetFastBite(on));
    }

    /// <summary>Đồng bộ 2 công tắc nâng cao theo tiện ích câu của tab đang chọn.</summary>
    private void ShowAssist(bool pov, bool fast)
    {
        _pov?.Set(pov);
        _fastBite?.Set(fast);
    }
}
