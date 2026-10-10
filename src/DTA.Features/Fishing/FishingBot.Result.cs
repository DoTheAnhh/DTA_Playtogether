using DTA.Engine.Bots;
using DTA.Game.Actions;
using DTA.Game.Data;
using DTA.Game.Ui;

namespace DTA.Features.Fishing;

/// <summary>Phần bảng kết quả của <see cref="FishingBot"/>.</summary>
public sealed partial class FishingBot
{
    private const double ResultTimeout = 15, ReadText = 1.5, TapGap = 0.2;
    private static readonly string[] ModalWords = ["Reward", "Question", "Message", "ResultGetItem"];

    /// <summary>
    /// Bước kết quả: đọc tên / giá / nền trên bảng (game cần ~0,5 s để điền chữ), quyết định giữ / bán, ghi lịch sử, rồi bảo quản / bán nhanh
    /// ĐÚNG 1 LẦN (bán lần 2 là lỗi #11502). Bán xong còn bảng "Hoàn tất bán hàng"; câu được hộp thì có màn nhận đồ - xử lý tới khi sạch.
    /// </summary>
    private void Finish(int fishId, FishState st, bool ownCast)
    {
        var sell = options.Sell;
        CatchInfo? info = null;
        bool prepared = false, sold = false, rewardSeen = false, blocked = false;
        double lastTap = 0, shown = 0, opened = Now;
        long tapped = 0;
        while (Running && Now - opened < ResultTimeout)
        {
            var top = Session.Open.Top();
            if (_game.Poll().State != FishStates.Result && top.Addr == 0)
            {
                if (sold && !rewardSeen && Now - lastTap < 0.35)
                {
                    Thread.Sleep(20);
                    continue;
                }
                break;
            }
            var result = Session.Open.ResultDialog();
            if (result != 0 && info == null && Now - opened < ReadText && (info = Session.Open.Catch()) == null)
            {
                Thread.Sleep(30);
                continue;
            }
            if (result != 0 && !prepared)
            {
                prepared = true;
                var kept = sell ? KeepReason(result, fishId, info) : "";
                if (kept.Length > 0) sell = false;
                if (ownCast) Record(fishId, st, info, sell, kept);
            }
            if (result == 0 && top.Is(DialogReader.Result)) (result, sell) = (top.Addr, false);
            var now = Now;
            var modal = top.Addr != 0 && top.Addr != result && top.Names.Any(n => ModalWords.Any(n.Contains));
            if (modal || (result == 0 && top.Addr != 0))
            {
                if (top.Addr != tapped || now - lastTap >= TapGap)
                {
                    rewardSeen |= top.Names.Any(n => n.Contains("Reward"));
                    if (!Session.Dialogs.CloseObstruction(top.Addr, top.Names)) Session.Dialogs.Handle(top.Addr);
                    (lastTap, tapped) = (now, top.Addr);
                }
                else if (!blocked && now - Math.Max(opened, lastTap) > 2)
                {
                    blocked = true;
                    Events.Status("Game đang mở bảng khác - chờ đóng bảng...", Level.Warn);
                }
            }
            else if (result != 0)
            {
                shown = shown == 0 ? now : shown;
                if (sell && !sold && (info != null || now - shown >= 0.08))
                {
                    Session.Dialogs.Handle(result, DialogAction.Sell);
                    sold = true;
                    (lastTap, tapped) = (now, result);
                }
                else if (!sell && (tapped != result || now - lastTap >= TapGap))
                {
                    Session.Dialogs.Handle(result, DialogAction.Keep);
                    (lastTap, tapped) = (now, result);
                }
            }
            Thread.Sleep(20);
        }
        _resultClosed = Now;
        if (!prepared && ownCast) Record(fishId, st, info, sell, "");
    }

    /// <summary>
    /// Đang chọn Bán nhanh: con này có phải giữ không (lý do; rỗng = bán). Bán là mất hẳn nên không đọc được thông tin cần để quyết định thì
    /// giữ. Giữ khi khớp ĐỦ mọi mục giữ đang chọn; không chọn mục nào = giữ tất cả. Đồ vật (không phải cá) có nút bán thì bán.
    /// </summary>
    private string KeepReason(long result, int fishId, CatchInfo? info)
    {
        if (Session.Buttons.Find(result, "sell") == null && Session.Buttons.Find(result, "keep") != null) return "tài khoản chưa có gói bán nhanh - game không hiện nút Bán";
        if (info is { IsFish: false }) return "";
        if (!options.HasKeepConditions) return "không chọn điều kiện giữ nên giữ tất cả";
        var reasons = new List<string>();
        if (options.KeepIds.Count > 0)
        {
            if (fishId == 0) return "chưa rõ ID cá";
            if (!options.KeepIds.Contains(fishId)) return "";
            reasons.Add($"ID {fishId}");
        }
        if (options.KeepShadows.Count > 0)
        {
            var shadow = catalog.ShadowOf(fishId);
            if (shadow == 0) return "chưa rõ cỡ bóng";
            if (!options.KeepShadows.Contains(shadow)) return "";
            reasons.Add($"bóng {shadow}");
        }
        if (options.KeepGrades.Count > 0)
        {
            var grade = info?.Grade ?? 0;
            if (!GameNames.Grades.ContainsKey(grade)) return "chưa rõ nền";
            if (!options.KeepGrades.Contains(grade)) return "";
            reasons.Add($"nền {GameNames.Grades[grade]}");
        }
        if (options.KeepMutant || options.KeepVariant)
        {
            var traits = _game.Traits();
            var matched = new List<string>();
            if (options.KeepMutant)
            {
                if (traits.Mutant == true) matched.Add("cá đột biến");
                else if (traits.Mutant == null) return "chưa rõ đặc điểm cá";
            }
            if (options.KeepVariant)
            {
                if (traits.Variant == true)
                {
                    var hit = options.KeepVariantTypes.Count == 0 ? traits.VariantTypes : traits.VariantTypes.Where(options.KeepVariantTypes.Contains).ToList();
                    if (hit.Count > 0) matched.Add($"biến thể {string.Join(", ", hit.Select(FishMutations.NameOf))}");
                }
                else if (traits.Variant == null) return "chưa rõ đặc điểm cá";
            }
            if (matched.Count == 0) return "";
            reasons.Add(matched[0]);
        }
        return string.Join(", ", reasons);
    }

    /// <summary>Ghi 1 dòng lịch sử; cá (không phải đồ vật) mà catalog chưa có dưới ID đó thì catalog học thêm.</summary>
    private void Record(int fishId, FishState st, CatchInfo? info, bool sell, string kept)
    {
        if (info is { IsFish: true }) catalog.Learn(fishId, st.CatchItem, info.Name, info.Grade);
        var name = info?.Name is { Length: > 0 } n ? n : catalog.Names.GetValueOrDefault(st.CatchItem) ?? $"Vật phẩm {st.CatchItem}";
        var grade = info?.Grade is > 0 and var g ? g : catalog.ItemGrades.GetValueOrDefault(st.CatchItem);
        fishing.Catch(new CatchRecord(DateTime.Now, fishId, catalog.ShadowOf(fishId), grade, st.CatchItem, name, st.CatchSize, info?.Price ?? "", sell ? "sell" : "keep"));
        Events.Status(kept.Length > 0 ? $"Giữ lại ({kept}): {name}"
            : sell ? $"Bán nhanh: {name}{(info?.Price is { Length: > 0 } price ? $" ({price} sao)" : "")}"
            : $"Đã câu được: {name} ({st.CatchSize} cm)", Level.Ok);
    }
}
