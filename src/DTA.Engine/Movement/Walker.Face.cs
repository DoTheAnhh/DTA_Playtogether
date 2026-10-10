using DTA.Runtime.Core;

namespace DTA.Engine.Movement;

/// <summary>Phần quay mặt tại chỗ của <see cref="Walker"/>.</summary>
public sealed partial class Walker
{
    private const float FaceTilt = 0.4f, FaceDot = 0.985f;
    private const double FaceMax = 0.6;

    /// <summary>
    /// Quay mặt nhân vật về <paramref name="target"/> bằng 1 lần đè cần nhẹ (40%) theo hướng đó tới khi lệch ≤ 10° (tối đa 0,6 s) rồi nhấc -
    /// đúng 1 lần đè, không táp táp. True nếu đã quay đúng hướng.
    /// </summary>
    public bool Face((float X, float Z) target)
    {
        _stick ??= session.Ui.Joystick();
        if (_stick is not var (cx, cy, radius) || !touch.CanHold || session.Camera.Direction() is not var (fx, fz)) return false;
        bool Aligned(out float dx, out float dz)
        {
            (dx, dz) = (0, 0);
            if (session.Player.Facing() is not var (p, f)) return false;
            (dx, dz) = (target.X - p.X, target.Z - p.Z);
            var length = MathF.Sqrt(dx * dx + dz * dz);
            if (length < 1e-3f) return true;
            (dx, dz) = (dx / length, dz / length);
            return dx * f.X + dz * f.Z >= FaceDot;
        }
        if (Aligned(out var x, out var z)) return true;
        var end = Now + FaceMax;
        try
        {
            touch.Hold(cx, cy);
            Thread.Sleep(30);
            while (Now < end && running())
            {
                float up = x * fx + z * fz, right = x * fz - z * fx;
                touch.Hold((int)(cx + right * radius * FaceTilt), (int)(cy - up * radius * FaceTilt));
                Thread.Sleep(40);
                if (Aligned(out x, out z)) return true;
            }
            return Aligned(out _, out _);
        }
        finally
        {
            try
            {
                touch.Release();
            }
            catch (DeviceError e)
            {
                L.Swallowed("nhấc ngón", e);
            }
        }
    }
}
