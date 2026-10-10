using DTA.Game.Geometry;
using DTA.Game.Session;
using DTA.Game.Ui;
using DTA.Runtime.Core;

namespace DTA.Game.Data;

/// <summary>Trang hội thoại NPC: lời thoại, các nút trả lời (loại, điểm), điểm bấm sang câu kế.</summary>
public sealed record TalkPage(string Text, List<(int Kind, ScreenPoint Point)> Answers, ScreenPoint? Next);

/// <summary>Bong bóng trên đầu vật thể (HudSystem), hội thoại NPC, nút nhặt đồ - dùng chung theo phiên.</summary>
public sealed class HeadUps(GameSession session)
{
    private const float NearHead = 1.0f;

    /// <summary>
    /// Điểm màn hình của nút game đang hiện trên đầu thứ đứng ở <paramref name="place"/> (bong bóng NPC, nút nhặt...): mọi nút là
    /// HeadUpSelectButton của HudSystem bám theo targetPos. Null nếu chưa hiện.
    /// </summary>
    public ScreenPoint? Above(Vec3 place) => session.Optional<ScreenPoint?>(() =>
    {
        var hud = session.System("sysHud");
        foreach (var group in session.Managed.ListItems(session.Managed.Ptr(hud, "_selectButtonList")))
        {
            var shown = session.Managed.ListItems(session.Managed.Ptr(group, "onHeadUpList"));
            var klass = shown.Count > 0 ? session.Managed.ClassOf(shown[0]) : 0;
            if (klass == 0 || !session.Il2Cpp.HasField(klass, "targetPos")) continue;
            int target = session.Il2Cpp.Field(klass, "targetPos"), button = session.Il2Cpp.Field(klass, "selectButton"), camera = session.Il2Cpp.Field(klass, "uiCamera");
            foreach (var raw in session.Memory.ReadObjects(shown, new[] { target + 12, button + 8, camera + 8 }.Max()).Values)
            {
                var (x, _, z) = Bin.Vec3(raw, target);
                if (MathF.Sqrt((x - place.X) * (x - place.X) + (z - place.Z) * (z - place.Z)) > NearHead) continue;
                if (session.World.ComponentPoint(Bin.U64(raw, button), Bin.U64(raw, camera)) is { } p) return p;
            }
        }
        return null;
    }, "nút trên đầu");

    /// <summary>
    /// Bong bóng bàn tay (HeadUpSelectButton) trên vật nhặt được: MapObjectManager.CollectObjectComp.cacheSelectButton; game chỉ dựng
    /// khi đứng đủ gần. 0 nếu chưa có.
    /// </summary>
    public long PickButton(long thing) => session.Optional(() =>
    {
        var comp = session.Managed.Ptr(thing, "CollectObjectComp");
        var klass = session.Managed.ClassOf(comp);
        var button = klass != 0 && session.Il2Cpp.HasField(klass, "cacheSelectButton") ? session.Managed.Ptr(comp, "cacheSelectButton") : 0;
        return button != 0 && session.World.IsShown(button) == true ? button : 0;
    }, "nút nhặt");

    /// <summary>
    /// Bảng hội thoại NPC đang hiện gì. Nút nằm trong danh sách dùng lại (nút câu trước còn dữ liệu cũ) nên chỉ tin số nút của
    /// trang này (_buttonDataList).
    /// </summary>
    public TalkPage? Talk(long dialog) => session.Optional(() =>
    {
        var m = session.Managed;
        var answers = new List<(int, ScreenPoint)>();
        foreach (var button in m.ListItems(m.Ptr(dialog, "ButtonList")))
        {
            var kind = m.I32(m.Ptr(button, "_data"), "ButtonType");
            if (kind is { } k && session.Widgets.Point(m.Ptr(button, "SpriteButtonBG")) is { } p) answers.Add((k, p));
        }
        var used = m.ListItems(m.Ptr(dialog, "_buttonDataList")).Count;
        var text = m.Ptr(dialog, "_ment");
        var ment = text != 0 ? session.Memory.Strings([text], 200).GetValueOrDefault(text, "") : "";
        return new TalkPage(ment, answers.Take(used).ToList(), session.Widgets.Point(m.Ptr(dialog, "LabelMessage")));
    }, "hội thoại");
}
