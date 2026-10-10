namespace DTA.Game.World;

/// <summary>
/// Bố cục object native của bản Unity đang chạy (đã kiểm chứng lúc chạy) + tâm camera giao diện của màn chơi.
/// Cached = offset m_CachedPtr (object game -> object native); ComponentGo = Component -> GameObject; GoComponents = GameObject -> mảng
/// component; GoActive = cờ "đang hiện" trong GameObject; Hierarchy = Transform -> bảng vị trí.
/// </summary>
public sealed record NativeLayout(int Cached, int ComponentGo, int GoComponents, int GoActive, int Hierarchy, float CameraX, float CameraY)
{
    /// <summary>Các bộ (ComponentGo, GoComponents, GoActive, Hierarchy) theo đời Unity (mới trước); lúc chạy chỉ nhận bộ cho ra đúng vị trí.</summary>
    public static readonly (int ComponentGo, int GoComponents, int GoActive, int Hierarchy)[] Candidates =
        [(0x20, 0x20, 0x47, 0x28), (0x30, 0x30, 0x57, 0x38)];

    /// <summary>Những chỗ engine có thể đặt ô trỏ ngược từ object native về object phía game.</summary>
    public static readonly int[] HandleOffsets = [0x18, 0x10, 0x20, 0x28];

    /// <summary>Bản Unity này không nhận ra bố cục nào: thôi không thử nữa.</summary>
    public static readonly NativeLayout Unsupported = new(0, 0, 0, 0, 0, 0, 0);

    public bool Supported => Hierarchy != 0;
}
