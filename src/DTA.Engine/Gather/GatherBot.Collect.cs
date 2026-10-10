using DTA.Engine.Bots;
using DTA.Game.Actions;
using DTA.Game.Data;
using DTA.Game.Ui;

namespace DTA.Engine.Gather;

/// <summary>Phần xử lý các bảng sau khi thu hoạch của <see cref="GatherBot{T}"/>.</summary>
public abstract partial class GatherBot<T>
{
    private const double OpenTime = 0.45, TapGap = 0.08, KeepGap = 0.7, ResultWait = 1.2;

    /// <summary>
    /// Qua các bảng game hiện sau khi thu hoạch (bảng kết quả, màn mở hộp, nhận thưởng...), ghi món nhận được, sửa dụng cụ nếu game mở bảng
    /// sửa, rồi chờ nhân vật rảnh tay. Bảng lạ (túi đầy, thông báo...) thì đứng chờ chứ không bấm bừa. Chuỗi = lỗi dừng hẳn.
    /// </summary>
    private string? Collect()
    {
        Recorded = false;
        if (!ShouldCollect())
        {
            Collected();
            return null;
        }
        var started = false;
        double lastTap = 0, opened = 0, waiting = 0;
        long tapped = 0, openedAddr = 0, recorded = 0;
        CatchInfo? first = null;
        var loot = new List<(string Name, int Grade, int Price)>();
        var shown = 0;
        var blocked = false;
        bool sell = false, sold = false;
        var handled = "keep";
        while (Running)
        {
            var now = Now;
            var top = Session.Open.Top();
            if (top.Addr == 0)
            {
                var phase = CurrentPhase();
                if (phase == Phase.Claim)
                {
                    waiting = waiting == 0 ? now : waiting;
                    if (now - waiting > 1) break;
                }
                else if (phase == Phase.Reward) started = true;
                else if (phase == Phase.Idle && (started || now - LastAction > ResultWait)) break;
                Thread.Sleep(20);
                continue;
            }
            waiting = 0;
            if (top.Addr != openedAddr) (openedAddr, opened, blocked) = (top.Addr, now, false);
            var due = top.Addr != tapped || now - lastTap >= TapGap;
            void Tapped() => (lastTap, tapped) = (now, top.Addr);
            if (top.Is(DialogReader.Repair))
            {
                if (!Options.AutoRepair) return Broken;
                var error = Repair(top.Addr);
                if (error != null || !Running) return error;
                continue;
            }
            if (top.Is(DialogReader.Result))
            {
                started = true;
                if (recorded != top.Addr)
                {
                    var info = Session.Open.Catch();
                    if (info != null || now - opened > 1)
                    {
                        recorded = top.Addr;
                        first ??= info;
                        sell = info != null && Session.Buttons.Find(top.Addr, "sell") != null && SellResult(top.Addr, info);
                        handled = sell ? "sell" : "keep";
                    }
                }
                if (recorded == top.Addr)
                {
                    if (sell && !sold)
                    {
                        if (now - opened >= OpenTime && (tapped != top.Addr || now - lastTap > 1.2))
                        {
                            Session.Dialogs.Handle(top.Addr, DialogAction.Sell);
                            sold = true;
                            Tapped();
                        }
                    }
                    else if ((!sold || now - lastTap > KeepGap) && (tapped != top.Addr || now - lastTap > KeepGap))
                    {
                        Session.Dialogs.Handle(top.Addr, DialogAction.Keep);
                        Tapped();
                    }
                }
            }
            else if (top.Is(DialogReader.ItemView) || top.Is(DialogReader.ItemList))
            {
                started = true;
                var item = top.Is(DialogReader.ItemView) ? Session.Open.OpenedItem(top.Addr) : 0;
                if (item != 0 && item != shown)
                {
                    shown = item;
                    var info = Session.Tables.Item(item);
                    loot.Add((info.Name, info.Grade, Session.Tables.Price(item)));
                }
                if (due)
                {
                    Session.Dialogs.Handle(top.Addr);
                    Tapped();
                }
            }
            else if ((top.Is(DialogReader.Question) || top.Is(DialogReader.Reward) || top.Is(DialogReader.Message)) && due)
            {
                started |= top.Is(DialogReader.Reward);
                Session.Dialogs.Handle(top.Addr);
                Tapped();
            }
            else if (due && Session.Dialogs.CloseObstruction(top.Addr, top.Names))
            {
                Events.Status("Lỡ mở thẻ thông tin người chơi - đóng rồi làm tiếp", Level.Quiet);
                Tapped();
            }
            else if (_walkedIn && due && now - opened > 0.6 && tapped != top.Addr && Session.Buttons.Close(top.Addr) != null)
            {
                Events.Status("Lỡ đi vào 1 cổng của bản đồ - đóng bảng đó rồi đi tiếp", Level.Warn);
                Session.Dialogs.Handle(top.Addr, DialogAction.Close);
                Tapped();
            }
            else if (!blocked && tapped != top.Addr && now - opened > 1.5)
            {
                blocked = true;
                var title = top.Is(DialogReader.Message) ? Session.Open.Text(top.Addr, "LabelTitle") : "";
                Events.Status(title.Length > 0 ? $"Game đang báo \"{title}\" - chờ đóng bảng..." : "Game đang mở bảng khác - chờ đóng bảng...", Level.Warn);
            }
            Thread.Sleep(20);
        }
        LastAction = 0;
        _walkedIn = false;
        if (Running && (first != null || loot.Count > 0)) Record(first, loot, handled);
        Collected();
        return null;
    }

    /// <summary>
    /// Báo 1 lượt thu hoạch: vật gì, nhận món gì. Có mở hộp thì món nhận là thứ trong hộp chứ không phải cái hộp; món trùng tên ghi 1 lần.
    /// </summary>
    /// <summary>Lượt vừa rồi đã ghi lịch sử từ bảng nhận đồ của game chưa.</summary>
    protected bool Recorded { get; private set; }

    /// <summary>Vừa soát xong các bảng sau thu hoạch (ghi thêm lịch sử riêng nếu cần).</summary>
    protected virtual void Collected() { }

    /// <summary>Có cần soát các bảng sau thu hoạch không (đảo: không có bảng thì đi tiếp ngay).</summary>
    protected virtual bool ShouldCollect() => true;

    private void Record(CatchInfo? first, List<(string Name, int Grade, int Price)> loot, string handled)
    {
        var price = first != null && int.TryParse(new string(first.Price.Where(char.IsDigit).ToArray()), out var p) ? p : 0;
        var items = loot.Where(i => i.Name.Length > 0).ToList();
        if (items.Count == 0 && first != null) items.Add((first.Name, first.Grade, price));
        if (items.Count == 0) return;
        Recorded = true;
        var source = Source.Length > 0 ? Source : first?.Name ?? char.ToUpper(Place[0]) + Place[1..];
        Source = "";
        var name = string.Join(", ", items.Select(i => i.Name).Distinct());
        Gather.Find(new FindRecord(DateTime.Now, source, name, items.Max(i => i.Grade), items.Sum(i => i.Price), handled));
        Events.Status(handled == "sell" ? $"Bán nhanh: {name}" : name != source ? $"{source}: nhận được {name}" : $"Đã nhận được: {name}", Level.Ok);
    }
}
