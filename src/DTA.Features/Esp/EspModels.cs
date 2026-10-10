using DTA.Game.Geometry;

namespace DTA.Features.Esp;

/// <summary>Nhóm vật thể ESP + màu nhãn.</summary>
public static class EspGroups
{
    public const string Relic = "Cổ vật", Ore = "Đá & quặng", Plant = "Cây & nguyên liệu", Insect = "Côn trùng", Card = "Thẻ";
    public static readonly string[] All = [Relic, Ore, Plant, Insect, Card];
    public static readonly IReadOnlyDictionary<string, string> Colors = new Dictionary<string, string>
    {
        [Relic] = "#fbbf24", [Ore] = "#93c5fd", [Plant] = "#86efac", [Insect] = "#f9a8d4", [Card] = "#fb923c",
    };
    public static readonly (int Size, string Label)[] FontSizes = [(10, "Nhỏ"), (12, "Vừa"), (14, "Lớn"), (16, "To")];
    public static readonly int[] Ranges = [30, 60, 0];
}

/// <summary>1 vật ESP thấy: khoá (nguồn, mã), nhóm, tên, vị trí, ghi chú (máu, bước, chờ nhận quà...), cấp nền.</summary>
public sealed record Thing(string Key, string Group, string Kind, float X, float Y, float Z, string Note, int Grade = 0);

/// <summary>Camera chính + nhân vật tại 1 thời điểm.</summary>
public readonly record struct Look(double Time, Vec3 Camera, Quat Rotation, float Fov, Vec3 Me);

/// <summary>Tuỳ chọn ESP (đọc thẳng lúc chạy): nhóm bật, loại tắt riêng, phạm vi, vẽ nhãn, ghi khoảng cách, cỡ chữ.</summary>
public sealed class EspOptions
{
    public HashSet<string> Groups { get; set; } = [.. EspGroups.All];
    public HashSet<string> Hidden { get; set; } = [];
    public int Radius { get; set; } = 60;
    public bool Overlay { get; set; } = true;
    public bool Distance { get; set; } = true;
    public int FontSize { get; set; } = 12;

    public void Normalize()
    {
        Groups = Groups.Where(EspGroups.All.Contains).ToHashSet();
        if (!EspGroups.Ranges.Contains(Radius)) Radius = 60;
        if (EspGroups.FontSizes.All(f => f.Size != FontSize)) FontSize = 12;
    }

    public static string HiddenKey(string group, string kind) => $"{group}|{kind}";

    public bool Shows(Thing thing) => (Groups.Count == 0 || Groups.Contains(thing.Group)) && !Hidden.Contains(HiddenKey(thing.Group, thing.Kind));
}

/// <summary>Phép chiếu + nội suy camera cho lớp vẽ nhãn.</summary>
public static class EspMath
{
    /// <summary>
    /// Điểm bản đồ hiện ở đâu trên màn hình game: (ngang, dọc) theo phần màn hình 0..1 (gốc trên trái); null nếu sau lưng camera. Đưa vector
    /// camera -> điểm về hệ trục camera (xoay ngược), chia phối cảnh theo góc nhìn dọc.
    /// </summary>
    public static (float X, float Y)? Project(Look look, Vec3 point, float aspect)
    {
        var local = look.Rotation.Inverse().Rotate(point - look.Camera);
        if (local.Z < 0.2f) return null;
        var scale = 1 / MathF.Tan(look.Fov * MathF.PI / 360);
        return (0.5f + 0.5f * local.X / local.Z * scale / aspect, 0.5f - 0.5f * local.Y / local.Z * scale);
    }

    /// <summary>Camera + nhân vật giữa 2 lần đọc (t 0..1): vị trí nội suy thẳng, hướng SLERP (gần trùng thì NLERP).</summary>
    public static Look Blend(Look a, Look b, float t)
    {
        static Vec3 Mix(Vec3 p, Vec3 q, float t) => new(p.X + (q.X - p.X) * t, p.Y + (q.Y - p.Y) * t, p.Z + (q.Z - p.Z) * t);
        var (r, s) = (a.Rotation, b.Rotation);
        var dot = r.X * s.X + r.Y * s.Y + r.Z * s.Z + r.W * s.W;
        var sign = dot < 0 ? -1f : 1f;
        dot = Math.Abs(dot);
        float w1, w2;
        if (dot > 0.9995f) (w1, w2) = (1 - t, t);
        else
        {
            var theta = MathF.Acos(Math.Clamp(dot, -1, 1));
            (w1, w2) = (MathF.Sin((1 - t) * theta) / MathF.Sin(theta), MathF.Sin(t * theta) / MathF.Sin(theta));
        }
        var rotation = new Quat(r.X * w1 + s.X * sign * w2, r.Y * w1 + s.Y * sign * w2, r.Z * w1 + s.Z * sign * w2, r.W * w1 + s.W * sign * w2).Normalized();
        return new Look(a.Time + (b.Time - a.Time) * t, Mix(a.Camera, b.Camera, t), rotation, b.Fov, Mix(a.Me, b.Me, t));
    }
}
