using DTA.Engine.Bots;
using DTA.Engine.Core;
using DTA.Engine.Gather;
using DTA.Engine.Popups;
using DTA.Features.Collect;
using DTA.Features.Excavation;
using DTA.Features.Farm;
using DTA.Features.Fishing;
using DTA.Features.Insect;
using DTA.Features.Mining;
using DTA.Runtime.Device;

/// <summary>Chạy thử 1 bot không giao diện (kèm canh popup như app) trong N giây, in mọi diễn biến: run (fish|mine|collect|insect|dig|island|farm) giây.</summary>
public static class BotRun
{
    public static void Run(EmulatorDevice device, string feature, int seconds, Action<string> step)
    {
        Risk.Install();
        var extras = feature.Split(':');
        Risk.Set(RiskFeature.Teleport, feature == "fishzone" || extras.Contains("tele"));
        feature = extras[0];
        if (extras.Contains("freeze"))
        {
            Risk.Set(RiskFeature.FreezeBugs, true);
            step($"Đóng băng bọ: {DTA.Features.Insect.FreezeService.For(device.Serial).SetEnabled(true)}");
        }
        if (extras.Contains("pov") || extras.Contains("fast"))
        {
            Risk.Set(RiskFeature.FastBite, true);
            var assist = FishingAssist.For(device.Serial);
            step($"POV: {assist.SetPov(extras.Contains("pov"))}, cắn nhanh: {assist.SetFastBite(extras.Contains("fast"))}");
        }
        var events = new BotEvents
        {
            Status = (text, level) => step($"[{level}] {text}"),
            Tool = tool => step($"[tool] {tool}"),
            Stopped = error => step($"[stopped] {error ?? "ok"}"),
        };
        var gather = new GatherEvents
        {
            Targets = (inRange, total) => step($"[targets] {inRange}/{total}"),
            Target = (distance, name) => { if (distance != null) step($"[target] {name} {distance:0.0} m"); },
            Find = record => step($"[FIND] {record.Source} -> {record.Name} nền {record.Grade} giá {record.Price} ({record.Action})"),
        };
        Bot bot = feature switch
        {
            "fish" => new FishingBot(device, events, new FishingEvents
            {
                Fish = id => step($"[fish] {id} bóng {FishCatalog.Installed(device).Catalog.ShadowOf(id ?? 0)}"), Catch = c => step($"[CATCH] {c.Name} id {c.FishId} bóng {c.Shadow} nền {c.Grade} {c.Size} cm ({c.Action})"),
            }, new FishingOptions { AutoRepair = true, FakeZoneOn = extras.Any(e => e.StartsWith("fake")), FakeZone = extras.FirstOrDefault(e => e.StartsWith("fake")) is { } fz ? int.Parse(fz[4..]) : 0, FilterOn = extras.Contains("filter"), WantedShadows = extras.Contains("filter") ? [1] : [], Sell = extras.Contains("sell"), HasPackage = extras.Contains("sell") }, FishCatalog.Installed(device).Catalog),
            "fishzone" => new FishingBot(device, events, new FishingEvents
            {
                Fish = id => step($"[fish] {id}"), Catch = c => step($"[CATCH] {c.Name} id {c.FishId} bóng {c.Shadow} nền {c.Grade} {c.Size} cm ({c.Action})"),
            }, new FishingOptions { AutoRepair = true, ZoneTele = true }, FishCatalog.Installed(device).Catalog),
            "mine" => new MiningBot(device, events, gather, new MiningOptions { AutoRepair = true }),
            "collect" => new CollectBot(device, events, gather, new CollectOptions()),
            "insect" => new InsectBot(device, events, gather, new InsectOptions { AutoRepair = true }),
            "dig" => new ExcavationBot(device, events, gather, new ExcavationOptions { AutoRepair = true, Radius = 0, Move = extras.Contains("tele") ? DTA.Engine.Gather.MoveMode.Teleport : DTA.Engine.Gather.MoveMode.Walk }),
            "island" => new ExcavationBot(device, events, gather, new ExcavationOptions { AutoRepair = true, Mode = ExcavationMode.LostIsland }),
            "farm" => new FarmBot(device, events, new FarmEvents { Info = i => step($"[farm] trong nông trại {i.InFarm}, cây {i.Crops.Count}, chín {i.Ripe.Count}") },
                new FarmOptions()) { Mode = FarmMode.Scan },
            _ => throw new ArgumentException($"không có chức năng {feature}"),
        };
        PopupWatcher.Instance.SetDevice(device);
        step($"Bật {feature} trong {seconds} s");
        bot.Start();
        var end = DateTime.UtcNow.AddSeconds(seconds);
        while (bot.Running && DateTime.UtcNow < end) Thread.Sleep(200);
        bot.Stop();
        Thread.Sleep(1500);
        bot.Close();
        PopupWatcher.Instance.Stop();
        if (extras.Contains("freeze")) DTA.Features.Insect.FreezeService.For(device.Serial).SetEnabled(false);
        step("Đã tắt");
    }
}
