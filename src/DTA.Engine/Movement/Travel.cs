using DTA.Game.Actor;
using DTA.Game.Session;

namespace DTA.Engine.Movement;

/// <summary>
/// Chuyển bản đồ trực tiếp qua ConnectToZoneMove (KHÔNG NPC / cổng / điện thoại) - dùng chung cho bot đổi bản đồ khi hết vật và menu tele.
/// </summary>
public static class Travel
{
    /// <summary>Các bản đồ hỗ trợ: mã -> tên hiển thị.</summary>
    public static readonly IReadOnlyDictionary<int, string> Maps = new Dictionary<int, string>
    {
        [1001] = "Plaza", [1301] = "Khu nghỉ dưỡng", [1201] = "Khu cắm trại", [1101] = "Khu trung tâm",
    };

    public static string Label(int mapId) => Maps.GetValueOrDefault(mapId, $"Bản đồ {mapId}");

    /// <summary>Bản đồ trong <paramref name="wanted"/> (rỗng = mọi bản đồ hỗ trợ; khác chỗ đang đứng) chuyển thẳng tới được.</summary>
    public static List<int> Reachable(int here, IReadOnlyCollection<int> wanted) => (wanted.Count == 0 ? Maps.Keys : wanted).Where(m => m != here && Maps.ContainsKey(m)).ToList();

    /// <summary>Sang bản đồ <paramref name="target"/>; null = đã đứng ở bản đồ mới, sẵn sàng; chuỗi = lỗi.</summary>
    public static string? Go(GameSession session, int target, Action<string>? status, CancellationToken cancel)
    {
        if (session.Camera.Map() is not { } here) return "Không đọc được bản đồ đang đứng";
        if (here.Id == target) return null;
        if (!Maps.ContainsKey(target)) return $"Bản đồ ID {target} không nằm trong danh sách hỗ trợ";
        status?.Invoke($"Đang chuyển trực tiếp sang {Label(target)}...");
        return session.Teleport.Travel(target, status, cancel) switch
        {
            TeleportStatus.Success => null,
            TeleportStatus.Cancelled => null,
            TeleportStatus.ConnectFailed => $"Không thể kích hoạt chuyển cảnh sang {Label(target)}",
            _ => $"Quá thời gian chờ tải bản đồ {Label(target)}",
        };
    }
}
