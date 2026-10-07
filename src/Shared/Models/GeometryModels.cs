using System.Text.Json.Serialization;

namespace DTA.Shared.Models;

public readonly struct Vector3 : IEquatable<Vector3>
{
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }

    [JsonConstructor]
    public Vector3(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public static readonly Vector3 Zero = new(0f, 0f, 0f);
    public static readonly Vector3 One = new(1f, 1f, 1f);
    public static readonly Vector3 Up = new(0f, 1f, 0f);

    public float SqrMagnitude => (X * X) + (Y * Y) + (Z * Z);
    public float Magnitude => MathF.Sqrt(SqrMagnitude);

    public static float Distance(Vector3 a, Vector3 b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        float dz = a.Z - b.Z;
        return MathF.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }

    public static float Distance2D(Vector3 a, Vector3 b)
    {
        float dx = a.X - b.X;
        float dz = a.Z - b.Z;
        return MathF.Sqrt((dx * dx) + (dz * dz));
    }

    public bool Equals(Vector3 other) =>
        MathF.Abs(X - other.X) < 1e-4f &&
        MathF.Abs(Y - other.Y) < 1e-4f &&
        MathF.Abs(Z - other.Z) < 1e-4f;

    public override bool Equals(object? obj) => obj is Vector3 other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y, Z);
    public override string ToString() => $"({X:F2}, {Y:F2}, {Z:F2})";

    public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vector3 operator *(Vector3 a, float d) => new(a.X * d, a.Y * d, a.Z * d);
}

public readonly struct Quaternion : IEquatable<Quaternion>
{
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }
    public float W { get; init; }

    [JsonConstructor]
    public Quaternion(float x, float y, float z, float w)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    public static readonly Quaternion Identity = new(0f, 0f, 0f, 1f);

    public static Quaternion FromEuler(float pitch, float yaw, float roll)
    {
        float deg2rad = MathF.PI / 180f;
        float p = pitch * deg2rad * 0.5f;
        float y = yaw * deg2rad * 0.5f;
        float r = roll * deg2rad * 0.5f;

        float sinP = MathF.Sin(p);
        float cosP = MathF.Cos(p);
        float sinY = MathF.Sin(y);
        float cosY = MathF.Cos(y);
        float sinR = MathF.Sin(r);
        float cosR = MathF.Cos(r);

        return new Quaternion(
            (sinR * cosP * cosY) - (cosR * sinP * sinY),
            (cosR * sinP * cosY) + (sinR * cosP * sinY),
            (cosR * cosP * sinY) - (sinR * sinP * cosY),
            (cosR * cosP * cosY) + (sinR * sinP * sinY)
        );
    }

    public (float Pitch, float Yaw, float Roll) ToEulerAngles()
    {
        float rad2deg = 180f / MathF.PI;
        float sinr_cosp = 2f * ((W * X) + (Y * Z));
        float cosr_cosp = 1f - (2f * ((X * X) + (Y * Y)));
        float roll = MathF.Atan2(sinr_cosp, cosr_cosp) * rad2deg;

        float sinp = 2f * ((W * Y) - (Z * X));
        float pitch;
        if (MathF.Abs(sinp) >= 1f)
            pitch = MathF.CopySign(90f, sinp);
        else
            pitch = MathF.Asin(sinp) * rad2deg;

        float siny_cosp = 2f * ((W * Z) + (X * Y));
        float cosy_cosp = 1f - (2f * ((Y * Y) + (Z * Z)));
        float yaw = MathF.Atan2(siny_cosp, cosy_cosp) * rad2deg;

        return (pitch, yaw, roll);
    }

    public bool Equals(Quaternion other) =>
        MathF.Abs(X - other.X) < 1e-4f &&
        MathF.Abs(Y - other.Y) < 1e-4f &&
        MathF.Abs(Z - other.Z) < 1e-4f &&
        MathF.Abs(W - other.W) < 1e-4f;

    public override bool Equals(object? obj) => obj is Quaternion other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y, Z, W);
    public override string ToString() => $"({X:F3}, {Y:F3}, {Z:F3}, {W:F3})";
}

/// <summary>
/// Góc nhìn nhân vật và camera (Viewpoint / POV / Orientation)
/// Đáp ứng yêu cầu: "vị trí tele luôn phải có thêm cả góc nhìn nhân vật"
/// </summary>
public sealed class CharacterViewpoint : IEquatable<CharacterViewpoint>
{
    /// <summary>
    /// Góc quay ngang của nhân vật và camera (Yaw trong khoảng 0..360 hoặc -180..180 độ)
    /// </summary>
    public float Yaw { get; set; }

    /// <summary>
    /// Góc ngẩng/cúi của camera (Pitch)
    /// </summary>
    public float Pitch { get; set; }

    /// <summary>
    /// Khoảng cách camera tới nhân vật (mặc định 4.5m)
    /// </summary>
    public float CameraDistance { get; set; } = 4.5f;

    /// <summary>
    /// Tự động căn chỉnh Camera sau lưng nhân vật khi tới đích
    /// </summary>
    public bool AlignCameraBehind { get; set; } = true;

    public CharacterViewpoint() { }

    public CharacterViewpoint(float yaw, float pitch, float distance = 4.5f, bool alignCamera = true)
    {
        Yaw = yaw;
        Pitch = pitch;
        CameraDistance = distance;
        AlignCameraBehind = alignCamera;
    }

    public static CharacterViewpoint FromQuaternion(Quaternion q, float cameraDistance = 4.5f)
    {
        var (_, yaw, pitch) = q.ToEulerAngles();
        return new CharacterViewpoint(yaw, pitch, cameraDistance, true);
    }

    public Quaternion ToQuaternion() => Quaternion.FromEuler(Pitch, Yaw, 0f);

    public bool Equals(CharacterViewpoint? other)
    {
        if (other is null) return false;
        return MathF.Abs(Yaw - other.Yaw) < 1e-2f &&
               MathF.Abs(Pitch - other.Pitch) < 1e-2f &&
               AlignCameraBehind == other.AlignCameraBehind;
    }

    public override bool Equals(object? obj) => obj is CharacterViewpoint other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Yaw, Pitch, AlignCameraBehind);
    public override string ToString() => $"Yaw={Yaw:F1}°, Pitch={Pitch:F1}°, Distance={CameraDistance:F1}m";
}
