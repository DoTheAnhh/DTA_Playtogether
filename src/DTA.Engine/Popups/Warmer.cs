using DTA.Game.Session;
using DTA.Runtime.Core;

namespace DTA.Engine.Popups;

/// <summary>
/// Làm nóng 1 lần mọi dữ liệu dùng chung ngay khi kết nối / đổi bản đồ (bảng vật phẩm, bảng chữ, vật thể bản đồ, bố cục native,
/// lưới mặt đất, đường vị trí côn trùng + tên loài): menu nào bật sau cũng chạy ngay, không phải dò. Chỉ đọc; bước nào lỗi thì bỏ.
/// </summary>
public static class Warmer
{
    private static readonly Logger L = Log.For("warm");

    /// <summary>Chạy ở luồng nền (không chặn vòng canh popup).</summary>
    public static void Run(GameSession session) => ThreadPool.QueueUserWorkItem(_ =>
    {
        var started = DateTime.UtcNow;
        (string Name, Action Run)[] steps =
        [
            ("bảng vật phẩm", () => session.Tables.Items()),
            ("bảng chữ", () => session.Tables.Strings()),
            ("vật thể", () => session.Map.Things()),
            ("bố cục", () => session.World.Layout()),
            ("mặt đất", () => session.Ground.Index()),
            ("côn trùng", () =>
            {
                var insects = session.Map.Insects();
                session.World.Poses(insects.Select(i => i.Control));
                foreach (var insect in insects) session.Tables.Item(insect.Item);
            }),
        ];
        foreach (var (name, run) in steps)
        {
            var at = DateTime.UtcNow;
            try
            {
                run();
                L.Debug($"Làm nóng {name}: {(DateTime.UtcNow - at).TotalMilliseconds:F0} ms");
            }
            catch (Exception e)
            {
                L.Debug($"Bỏ qua bước làm nóng: {e.Message}");
            }
        }
        session.SaveCache();
        L.Info($"Làm nóng dữ liệu xong trong {(DateTime.UtcNow - started).TotalMilliseconds:F0} ms");
    });
}
