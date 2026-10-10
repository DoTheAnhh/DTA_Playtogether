using System.Collections.Concurrent;
using DTA.Game.Actor;

namespace DTA.Engine.Core;

/// <summary>Chức năng rủi ro cao phải được người dùng xác nhận trước khi dùng.</summary>
public enum RiskFeature { Teleport, FreezeBugs, FishingPov, FastBite }

/// <summary>
/// Xác nhận rủi ro CHỈ trong RAM của phiên chạy (không bao giờ lưu đĩa). Mặc định tắt hết trừ khoá POV khi câu (tiện ích an toàn).
/// Mỗi chức năng độc lập; tầng dịch vụ luôn kiểm để bot / phím tắt không vượt được.
/// </summary>
public static class Risk
{
    private static readonly ConcurrentDictionary<RiskFeature, bool> Accepted = new();

    static Risk() => Reset();

    public static bool IsAccepted(RiskFeature feature) => Accepted.GetValueOrDefault(feature);

    public static void Set(RiskFeature feature, bool accepted) => Accepted[feature] = accepted;

    /// <summary>Về mặc định lúc mở tool.</summary>
    public static void Reset()
    {
        foreach (var feature in Enum.GetValues<RiskFeature>()) Accepted[feature] = feature == RiskFeature.FishingPov;
    }

    /// <summary>Gắn cổng rủi ro vào tầng Game (gọi 1 lần lúc khởi động).</summary>
    public static void Install() => TeleportService.Allowed = () => IsAccepted(RiskFeature.Teleport);
}
