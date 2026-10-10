namespace DTA.Game.Geometry;

/// <summary>Điểm / vector 3 chiều trong bản đồ game (y là độ cao).</summary>
public readonly record struct Vec3(float X, float Y, float Z)
{
    public static readonly Vec3 Zero = new(0, 0, 0);

    /// <summary>Khoảng cách trên mặt đất (bỏ độ cao).</summary>
    public float FlatDistance(Vec3 other) => MathF.Sqrt((X - other.X) * (X - other.X) + (Z - other.Z) * (Z - other.Z));

    public float Distance(Vec3 other) => MathF.Sqrt((X - other.X) * (X - other.X) + (Y - other.Y) * (Y - other.Y) + (Z - other.Z) * (Z - other.Z));

    public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public bool IsFinite => float.IsFinite(X) && float.IsFinite(Y) && float.IsFinite(Z);

    public override string ToString() => $"({X:F2}, {Y:F2}, {Z:F2})";
}

/// <summary>Hướng xoay dạng quaternion (x, y, z, w).</summary>
public readonly record struct Quat(float X, float Y, float Z, float W)
{
    public static readonly Quat Identity = new(0, 0, 0, 1);

    /// <summary>Quaternion quay quanh trục đứng để mặt (trục z) nhìn theo góc <paramref name="yaw"/> (radian, atan2(x, z)).</summary>
    public static Quat FromYaw(double yaw) => new(0, (float)Math.Sin(yaw / 2), 0, (float)Math.Cos(yaw / 2));

    /// <summary>Quaternion nhìn từ <paramref name="from"/> sang <paramref name="to"/> trên mặt đất.</summary>
    public static Quat LookAt(Vec3 from, Vec3 to) => FromYaw(Math.Atan2(to.X - from.X, to.Z - from.Z));

    /// <summary>Hướng phía trước chiếu xuống mặt đất: vector đơn vị (x, z); null nếu đang nhìn thẳng lên / xuống.</summary>
    public (float X, float Z)? GroundForward()
    {
        float fx = 2 * (X * Z + W * Y), fz = 1 - 2 * (X * X + Y * Y);
        var length = MathF.Sqrt(fx * fx + fz * fz);
        return length > 0.05f ? (fx / length, fz / length) : null;
    }

    /// <summary>Góc phía trước tính bằng độ (atan2(x, z)).</summary>
    public float YawDegrees() => GroundForward() is var (x, z) ? MathF.Atan2(x, z) * 180 / MathF.PI : 0;

    /// <summary>Xoay vector theo quaternion: v + 2q x (q x v + w v).</summary>
    public Vec3 Rotate(Vec3 v)
    {
        float cx = Y * v.Z - Z * v.Y + W * v.X, cy = Z * v.X - X * v.Z + W * v.Y, cz = X * v.Y - Y * v.X + W * v.Z;
        return new Vec3(v.X + 2 * (Y * cz - Z * cy), v.Y + 2 * (Z * cx - X * cz), v.Z + 2 * (X * cy - Y * cx));
    }

    /// <summary>Chuẩn hoá về độ dài 1 (gần 0 thì trả Identity).</summary>
    public Quat Normalized()
    {
        var l = MathF.Sqrt(X * X + Y * Y + Z * Z + W * W);
        return l > 1e-6f ? new Quat(X / l, Y / l, Z / l, W / l) : Identity;
    }

    /// <summary>Nghịch đảo của quaternion đơn vị (liên hợp).</summary>
    public Quat Inverse() => new(-X, -Y, -Z, W);

    /// <summary>Tích quaternion this * other (xoay other trước rồi this).</summary>
    public Quat Multiply(Quat o) => new(
        W * o.X + X * o.W + Y * o.Z - Z * o.Y,
        W * o.Y - X * o.Z + Y * o.W + Z * o.X,
        W * o.Z + X * o.Y - Y * o.X + Z * o.W,
        W * o.W - X * o.X - Y * o.Y - Z * o.Z);
}

/// <summary>Tư thế 1 object: vị trí + hướng xoay trong bản đồ.</summary>
public readonly record struct Pose(Vec3 Position, Quat Rotation);
