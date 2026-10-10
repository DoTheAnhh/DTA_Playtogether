using System.Globalization;
using System.Windows;
using System.Windows.Media;
using DTA.App.Shell;
using DTA.Ui;
using DTA.Engine.Core;
using DTA.Features.Teleport;
using DTA.Game.Data;

namespace DTA.App.Views.Teleport;

/// <summary>Bảng vị trí: sắp xếp / lọc / màu theo bản đồ, vị trí đang chọn, thêm / sửa / xoá / dịch chuyển.</summary>
public sealed partial class TeleportView
{
    private static readonly string[] Palette = ["#5eead4", "#fbbf24", "#4ade80", "#f472b6", "#a78bfa", "#38bdf8", "#fb923c", "#e879f9", "#67e8f9", "#fde047", "#93c5fd", "#c084fc"];
    private static readonly Dictionary<string, string> MapColors = new()
    {
        ["Square"] = "#5eead4", ["Plaza"] = "#5eead4", ["Downtown"] = "#fbbf24", ["CampGround"] = "#4ade80", ["Resort Island"] = "#f472b6", ["Lost Island"] = "#fb923c",
        ["LostIsland"] = "#fb923c", ["TreasureHunt"] = "#fb923c", ["House"] = "#a78bfa", ["MyHome"] = "#a78bfa",
    };
    private static readonly Dictionary<string, int> MapOrder = new()
    {
        ["plaza"] = 0, ["square"] = 0, ["khu nghỉ dưỡng"] = 1, ["resort island"] = 1, ["resort"] = 1, ["khu cắm trại"] = 2, ["campground"] = 2, ["camp"] = 2,
        ["khu trung tâm"] = 3, ["downtown"] = 3,
    };

    /// <summary>Quaternion [x, y, z, w] -> "góc° (hướng)" theo trục dọc.</summary>
    private static string Rotation(float[]? rot)
    {
        if (rot is not [var x, var y, var z, var w]) return "---";
        var yaw = (2 * Math.Atan2(y, w) * 180 / Math.PI % 360 + 360) % 360;
        string[] dirs = ["Bắc", "ĐB", "Đông", "ĐN", "Nam", "TN", "Tây", "TB"];
        return $"{yaw:0}° ({dirs[(int)((yaw + 22.5) / 45) % 8]})";
    }

    private static string MapTitle(TelePlace p) => p.MapName.Length > 0 ? GameNames.Map(p.MapName) : $"Bản đồ {p.MapId}";

    private static Color MapColor(TelePlace p)
    {
        if (MapColors.TryGetValue(p.MapName, out var hex) || MapColors.TryGetValue(GameNames.Map(p.MapName), out hex)) return Theme.Hex(hex);
        var key = p.MapName.Length > 0 ? p.MapName : p.MapId.ToString();
        return Theme.Hex(Palette[(int)(DTA.Runtime.Core.Crc32.Compute(System.Text.Encoding.UTF8.GetBytes(key)) % Palette.Length)]);
    }

    private string DistanceText(TelePlace place)
    {
        if (_here.Position is not { } me || _here.Map is not { } map) return "---";
        if (place.MapId != map.Id) return "khác bản đồ";
        return $"{Math.Sqrt((place.X - me.X) * (place.X - me.X) + (place.Z - me.Z) * (place.Z - me.Z)):0.0} m";
    }

    /// <summary>Nạp lại danh sách (máy chủ + của tôi), bộ lọc bản đồ, bảng.</summary>
    private void Reload(string? select = null)
    {
        _places = TelePlaces.All();
        _byId = _places.ToDictionary(p => p.Id);
        var maps = _places.Select(MapTitle).Distinct().OrderBy(m => MapOrder.GetValueOrDefault(m.ToLowerInvariant(), 99)).ThenBy(m => m.ToLowerInvariant()).ToList();
        _maps.SetValues([AllMaps, .. maps]);
        if (!maps.Contains(_filter)) _filter = AllMaps;
        _maps.Select(_maps.Values.ToList().IndexOf(_filter));
        ShowPlaces(select);
    }

    private void ShowPlaces(string? select = null)
    {
        var rows = _places
            .OrderBy(p => p.Editable ? 1 : 0).ThenBy(p => MapOrder.GetValueOrDefault(MapTitle(p).ToLowerInvariant(), 99)).ThenBy(p => MapTitle(p).ToLowerInvariant())
            .ThenBy(p => p.Name.ToLowerInvariant())
            .Where(p => _filter == AllMaps || MapTitle(p) == _filter)
            .Select(p => new TableRow(p.Id,
            [
                p.Editable ? "[CỦA TÔI]" : "[SERVER]", p.Name, MapTitle(p), Number(p.X), Number(p.Y), Number(p.Z), Rotation(p.Rotation), DistanceText(p),
            ], MapColor(p))).ToList();
        _table.SetRows(rows);
        if (select != null) _table.Select([select]);
        var server = _places.Count(p => !p.Editable);
        _count.Text = _filter != AllMaps ? $"Hiện {rows.Count}/{_places.Count} vị trí ({_filter})" : $"Tổng {_places.Count} vị trí ({server} Server, {_places.Count - server} Của tôi)";
        ShowSelected();
    }

    private static string Number(float value) => value.ToString("F2", CultureInfo.InvariantCulture);

    private List<TelePlace> Selection() => _table.Selected.Where(_byId.ContainsKey).Select(id => _byId[id]).ToList();

    private void ShowSelected()
    {
        var selected = Selection();
        if (selected.Count == 1)
        {
            var place = selected[0];
            _name.Set(place.Name);
            _xyz[0].Set(Number(place.X));
            _xyz[1].Set(Number(place.Y));
            _xyz[2].Set(Number(place.Z));
            _rotEntry.Set(place.Rotation != null ? Rotation(place.Rotation) : "---");
            Note(place.Editable ? "Vị trí cá nhân của bạn" : "Vị trí Server (Chỉ đọc) - Không thể sửa/xóa", Theme.Dim);
        }
        else
        {
            _name.Set(selected.Count > 1 ? $"Đang chọn {selected.Count} vị trí" : "");
            foreach (var entry in _xyz) entry.Set(selected.Count > 1 ? "---" : "");
            _rotEntry.Set(selected.Count > 1 ? "---" : "");
        }
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        var selected = Selection();
        var editable = selected.Count == 1 && selected[0].Editable;
        _add.SetEnabled(_here.Position != null && _here.Map != null);
        _confirm.SetEnabled(selected.Count == 1);
        _update.SetEnabled(editable);
        _name.SetEnabled(editable);
        foreach (var entry in _xyz) entry.SetEnabled(editable);
        _rotEntry.SetEnabled(editable);
        _delete.SetEnabled(selected.Any(p => p.Editable));
    }

    /// <summary>Dịch chuyển tới vị trí đang chọn (lần đầu hỏi xác nhận rủi ro).</summary>
    private void Confirm()
    {
        var selected = Selection();
        if (selected.Count != 1)
        {
            Note(selected.Count == 0 ? "Chưa chọn dòng nào trong bảng vị trí" : "Vui lòng chỉ chọn 1 vị trí để dịch chuyển", Theme.Warn);
            return;
        }
        if (!Risk.IsAccepted(RiskFeature.Teleport))
        {
            if (!RiskDialog.Ask(App))
            {
                Note("Đã hủy dịch chuyển", Theme.Muted);
                return;
            }
            Risk.Set(RiskFeature.Teleport, true);
        }
        if (_device == null || _tracker == null)
        {
            Note("Chưa kết nối tab giả lập", Theme.Warn);
            return;
        }
        Note($"Đang dịch chuyển tới {selected[0].Name}...", Theme.Hex("#38bdf8"));
        _tracker.Go(selected[0]);
    }

    /// <summary>Lưu chỗ nhân vật đang đứng vào vị trí của tôi (không gửi lên máy chủ).</summary>
    private void AddPlace()
    {
        if (_here.Position is not { } p || _here.Map is not { } map) return;
        var rotation = _here.Rotation is { } r ? new[] { r.X, r.Y, r.Z, r.W } : null;
        var typed = _name.Text.Trim();
        var place = TelePlaces.Add(typed.Length > 0 ? typed : $"Vị trí của tôi {_places.Count(x => x.Editable) + 1}", map.Id, map.Name, p.X, p.Y, p.Z, rotation);
        Reload(place.Id);
        Note($"Đã lưu \"{place.Name}\" vào máy", Theme.Ok);
    }

    /// <summary>Sửa tên / toạ độ vị trí của tôi đang chọn.</summary>
    private void UpdatePlace()
    {
        if (Selection() is not [{ Editable: true } place]) return;
        var name = _name.Text.Trim();
        var values = _xyz.Select(e => float.TryParse(e.Text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : float.NaN).ToArray();
        if (values.Any(float.IsNaN))
        {
            Note("Tọa độ X, Y, Z phải là số", Theme.Warn);
            return;
        }
        if (name.Length == 0)
        {
            Note("Chưa nhập tên vị trí", Theme.Warn);
            return;
        }
        TelePlaces.Update(place.Id, name, values[0], values[1], values[2], place.Rotation);
        Reload(place.Id);
        Note($"Đã sửa \"{name}\"", Theme.Ok);
    }

    /// <summary>Xoá các vị trí của tôi đang chọn (vị trí máy chủ bỏ qua).</summary>
    private void DeletePlaces()
    {
        var selected = Selection();
        var mine = selected.Where(p => p.Editable).ToList();
        var skipped = selected.Count - mine.Count;
        if (mine.Count == 0)
        {
            MessageBox.Show(App, "Các vị trí được chọn đều là vị trí của Server (chỉ đọc). Không thể xóa!", "Không thể xóa");
            return;
        }
        var question = (mine.Count == 1 ? $"Xoá vị trí của bạn: \"{mine[0].Name}\"?" : $"Xoá {mine.Count} vị trí cá nhân đã chọn?")
                       + (skipped > 0 ? $"\n(Bỏ qua {skipped} vị trí của Server)" : "");
        if (MessageBox.Show(App, question, mine.Count == 1 ? "Xoá vị trí" : "Xoá nhiều vị trí", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        foreach (var place in mine) TelePlaces.Delete(place.Id);
        Note(mine.Count == 1 ? $"Đã xoá \"{mine[0].Name}\"" : $"Đã xoá {mine.Count} vị trí", Theme.Muted);
        Reload();
    }
}
