using DTA.Engine.Gather;

namespace DTA.Features.Excavation;

/// <summary>Trạng thái đào (ActorDefaultControl.eExcavateState).</summary>
public enum ExcavateState { None = 0, Digging = 1, Miss = 2, Complete = 3, RewardReq = 4, RewardFail = 5, Boasting = 6, Finish = 7 }

/// <summary>1 loại cổ vật (TableExcavationObjectListImpl): tên tài nguyên, HP, tầm đào, tự phát quà, cả bản đồ cùng đào.</summary>
public sealed record RelicKind(string Asset, int Hp, float Reach, bool Auto, bool Shared);

/// <summary>
/// 1 điểm đào: mã, object, vị trí, loại (0 = chưa lộ), máu còn / tối đa, tầm đào, nút nhận quà (0 = chưa có), đang chờ server trả quà.
/// </summary>
public sealed record Spot(int Uid, long Ref, float X, float Z, int Kind, int Hp, int MaxHp, float Reach, float Y, long Button = 0, bool Claiming = false) : IGatherTarget
{
    /// <summary>Đã lộ và đã đào hết máu.</summary>
    public bool Done => Kind != 0 && Hp <= 0 && MaxHp > 0;

    /// <summary>Đã xong và có nút nhận quà đang chờ.</summary>
    public bool Reward => Done && Button != 0;
}

/// <summary>Chế độ đào: bản đồ thường / Hòn đảo bị mất.</summary>
public enum ExcavationMode { Excavation, LostIsland }

/// <summary>Tuỳ chọn đào: lọc rương (chỉ trên đảo), chế độ.</summary>
public sealed class ExcavationOptions : GatherOptions
{
    public bool OnlyChest { get; set; }
    public ExcavationMode Mode { get; set; } = ExcavationMode.Excavation;
}
