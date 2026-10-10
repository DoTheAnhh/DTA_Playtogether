using System.Text.Json.Nodes;
using DTA.App.Shell;
using DTA.Engine.Core;

namespace DTA.App.Views;

/// <summary>1 mục menu máy chủ gửi: mã chức năng + bật / tắt.</summary>
public sealed record Feature(string Id, bool Enabled = true);

/// <summary>Danh sách chức năng (máy chủ quyết định thứ tự / bật tắt - Server-Driven UI) -> các trang của cửa sổ chính.</summary>
public static class Pages
{
    public const string Mod = "mod", Esp = "esp", Fishing = "fishing", Excavation = "excavation", Mining = "mining", Insect = "insect", Collect = "collect",
        Monster = "monster", Farm = "farm", Teleport = "teleport", LogCenter = "log", Settings = "settings";

    public static readonly List<Feature> Defaults =
    [
        new(Mod), new(Esp), new(Fishing), new(Excavation), new(Mining), new(Insect), new(Collect), new(Monster, false), new(Farm), new(Teleport),
        new(LogCenter), new(Settings),
    ];

    private static readonly Dictionary<string, Func<MainWindow, PageView>> Factories = new()
    {
        [Mod] = app => new ModView(app),
        [Esp] = app => new Esp.EspView(app),
        [Fishing] = app => new Fishing.FishingView(app),
        [Excavation] = app => new Gather.ExcavationView(app),
        [Mining] = app => new Gather.MiningView(app),
        [Insect] = app => new Gather.InsectView(app),
        [Collect] = app => new Gather.CollectView(app),
        [Monster] = app => new UpcomingView(app, "Quái vật", "monster"),
        [Farm] = app => new Farm.FarmView(app),
        [Teleport] = app => new Teleport.TeleportView(app),
        [LogCenter] = app => new Logs.LogView(app),
        [Settings] = app => new Settings.SettingsView(app),
    };

    /// <summary>Đọc mảng features [{id, enabled}] của máy chủ; null nếu không hợp lệ.</summary>
    public static List<Feature>? Parse(JsonNode? node) => node is JsonArray { Count: > 0 } items
        ? items.OfType<JsonObject>().Select(f => new Feature(f["id"]?.GetValue<string>() ?? "", f["enabled"]?.GetValue<bool>() ?? true)).ToList()
        : null;

    /// <summary>Trang theo danh sách: máy chủ bản cũ thiếu Mod thì chèn trên ESP; bản miễn phí chỉ có câu cá.</summary>
    public static IReadOnlyList<PageView> Create(MainWindow app, List<Feature> features)
    {
        var items = features.ToList();
        if (items.All(f => f.Id != Mod)) items.Insert(Math.Max(0, items.FindIndex(f => f.Id == Esp)), new Feature(Mod));
        if (AuthSession.Free) items = items.Where(f => f.Id == Fishing).ToList();
        return items.Where(f => f.Enabled && Factories.ContainsKey(f.Id)).Select(f => Factories[f.Id](app)).ToList();
    }
}
