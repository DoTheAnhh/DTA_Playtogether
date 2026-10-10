using DTA.Engine.Movement;

namespace DTA.Engine.Gather;

/// <summary>Tuỳ chọn người dùng chỉnh; luồng làm việc đọc thẳng object này nên đổi lúc đang chạy có tác dụng ngay.</summary>
public class GatherOptions
{
    /// <summary>Các mức phạm vi (mét từ chỗ đứng lúc Bật); 0 = cả bản đồ.</summary>
    public static readonly int[] Ranges = [30, 60, 0];

    public bool AutoRepair { get; set; }
    public int Radius { get; set; } = 60;
    /// <summary>Bản đồ hết vật phù hợp thì sang bản đồ khác (trong <see cref="Maps"/>).</summary>
    public bool Hop { get; set; }
    public HashSet<int> Maps { get; set; } = [.. Travel.Maps.Keys];
    /// <summary>Cách tới vật: đi bộ (mặc định, không lưu chế độ khác cho lần mở sau) / dịch chuyển (cần xác nhận rủi ro).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public MoveMode Move { get; set; } = MoveMode.Walk;

    /// <summary>Sửa dữ liệu hỏng về mặc định (đọc từ file cũ).</summary>
    public virtual void Normalize()
    {
        if (!Ranges.Contains(Radius)) Radius = 60;
        Maps = Maps.Where(Travel.Maps.ContainsKey).ToHashSet();
    }
}

/// <summary>Cách tới vật.</summary>
public enum MoveMode { Walk, Teleport }

/// <summary>1 lượt thu hoạch xong: lúc, nguồn (vật), tên món, cấp nền cao nhất, tổng giá, giữ / bán.</summary>
public sealed record FindRecord(DateTime Time, string Source, string Name, int Grade, int Price, string Action);

/// <summary>Vật thu hoạch được trên bản đồ (mã + vị trí).</summary>
public interface IGatherTarget
{
    int Uid { get; }
    float X { get; }
    float Y { get; }
    float Z { get; }
}

/// <summary>Bước của việc thu hoạch: rảnh tay / đang vung / xong nhưng quà chờ nhận / game đang trả quà.</summary>
public enum Phase { Idle, Busy, Claim, Reward }

/// <summary>Sự kiện riêng của bot thu hoạch: (số vật tới được, tổng), (khoảng cách tới vật đang nhắm, tên), lượt thu hoạch mới.</summary>
public sealed class GatherEvents
{
    public Action<int, int> Targets { get; init; } = (_, _) => { };
    public Action<float?, string> Target { get; init; } = (_, _) => { };
    public Action<FindRecord> Find { get; init; } = _ => { };
}
