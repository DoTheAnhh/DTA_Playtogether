using DTA.Engine.Gather;

namespace DTA.Features.Insect;

/// <summary>Mức cảnh giác (InsectController._senseState).</summary>
public enum SenseState { None = 0, Sense = 1, Alert = 2, Escape = 3 }

/// <summary>Trạng thái vợt (ActorDefaultControl._insectState): trúng 3 -> 5 -> 6 / 8; hụt 4 (hoặc 7) -> 9 -> 0.</summary>
public enum NetState { None = 0, Begin = 1, Swing = 2, SwingDown = 3, SwingFail = 4, CatchReq = 5, CatchSuccess = 6, CatchFail = 7, Boast = 8, Finish = 9, ReloadPole = 10 }

/// <summary>
/// 1 con đang sống: mã, object điều khiển, ID loài, tên, cấp nền, loại, vị trí, _state (2 đứng, 3 di chuyển, ≥ 4 đang rời đi), mức cảnh
/// giác, bán kính cảnh giác (0 = không cảnh giác), tốc độ an toàn trong vòng, tốc độ khi nó đang nghi.
/// </summary>
public sealed record Bug(int Uid, long Ref, int Item, string Name, int Grade, string Kind, float X, float Y, float Z,
                         int State, SenseState Sense, float Radius, float Calm, float Still) : IGatherTarget
{
    public const int Leaving = 4;
}

/// <summary>Loại sinh vật bắt bằng vợt.</summary>
public static class BugKinds
{
    public const string Insect = "insect", Bird = "bird", Card = "card", StarBox = "star_box", GemBox = "gem_box", Other = "other";
    public static readonly (string Kind, string Label)[] Labels = [(Insect, "Côn trùng"), (Bird, "Chim"), (Card, "Thẻ bay"), (StarBox, "Hộp sao"), (GemBox, "Hộp kim cương")];
    private static readonly Dictionary<int, string> Boxes = new() { [25040005] = StarBox, [25040004] = GemBox };
    private static readonly string[] CardWords = ["thẻ", "card"];

    /// <summary>Loại theo ID vật phẩm + tên: 25040005 / 25040004 hộp; tên có "thẻ" = gói thẻ; 3303xxxx chim; 33xxxxxx côn trùng.</summary>
    public static string Of(int item, string name)
    {
        if (Boxes.TryGetValue(item, out var box)) return box;
        if (CardWords.Any(w => name.Contains(w, StringComparison.OrdinalIgnoreCase))) return Card;
        var group = item / 10000;
        return group == 3303 ? Bird : group / 100 == 33 ? Insect : Other;
    }
}

/// <summary>Tuỳ chọn bắt bọ: loại, cấp nền, cấp nền giữ lại khi bán nhanh, bán / giữ.</summary>
public sealed class InsectOptions : GatherOptions
{
    public HashSet<string> Kinds { get; set; } = [.. BugKinds.Labels.Select(l => l.Kind)];
    public HashSet<int> Grades { get; set; } = [1, 2, 3, 4, 5];
    public HashSet<int> KeepGrades { get; set; } = [4, 5];
    public bool Sell { get; set; }
    public bool HasPackage { get; set; }

    public InsectOptions() => Radius = 0;

    public override void Normalize()
    {
        base.Normalize();
        Kinds = Kinds.Where(k => BugKinds.Labels.Any(l => l.Kind == k)).ToHashSet();
        Grades = Grades.Where(g => g is >= 1 and <= 5).ToHashSet();
        KeepGrades = KeepGrades.Where(g => g is >= 1 and <= 5).ToHashSet();
    }
}
