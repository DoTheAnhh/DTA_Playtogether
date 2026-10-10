namespace DTA.Runtime.Core;

/// <summary>Lỗi kèm thông báo hiển thị được cho người dùng. <see cref="Retry"/> = lỗi tạm (mất kết nối...), thử lại có thể hết.</summary>
public class DeviceError(string message, bool retry = false) : Exception(message)
{
    public bool Retry { get; } = retry;
}

/// <summary>Lỗi khi đọc / điều khiển game.</summary>
public sealed class GameError(string message, bool retry = false) : DeviceError(message, retry);
