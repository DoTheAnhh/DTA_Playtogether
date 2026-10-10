using DTA.Game.Session;
using DTA.Runtime.Core;

namespace DTA.Features.Fishing;

/// <summary>
/// Khoá POV khi câu: lúc quăng chốt góc FreeLook đang nhìn; trong lượt câu giữ CinemachineBrain.mSoloCamera = FreeLook (game không đổi sang
/// FishingView), tắt tự quay về (m_YAxisRecentering, m_RecenterToTargetHeading) và IsCameraLookAt - KHÔNG ghi đè góc nên người chơi vẫn xoay
/// được. Hết lượt trả nguyên. Ghi theo tên field (dump).
/// </summary>
public sealed class PovLock(GameSession session)
{
    private static readonly Logger L = Log.For("fishing");
    private long _freeLook, _soloSlot;
    private byte[]? _yRecenter, _headingRecenter;

    public GameSession Session { get; } = session;

    private int F(long obj, string name) => Session.Il2Cpp.Field(Session.Managed.ClassOf(obj), name);

    /// <summary>Ô static CinemachineBrain.mSoloCamera.</summary>
    private long SoloSlot()
    {
        if (_soloSlot != 0) return _soloSlot;
        var klass = Session.Class("Cinemachine.CinemachineBrain");
        return _soloSlot = klass != 0 ? Session.Il2Cpp.StaticFieldPtr(klass, "mSoloCamera") : 0;
    }

    /// <summary>Chốt góc lúc quăng (chỉ 1 lần / lượt).</summary>
    public void Capture()
    {
        if (_freeLook != 0) return;
        var look = Session.Camera.FreeLook();
        if (look == 0) return;
        var reads = Session.Memory.ReadMany([(look + F(look, "m_YAxisRecentering"), 1), (look + F(look, "m_RecenterToTargetHeading"), 1)]);
        (_freeLook, _yRecenter, _headingRecenter) = (look, reads[0] ?? [0], reads[1] ?? [0]);
        Maintain();
        L.Debug("Khoá POV lúc quăng cần");
    }

    /// <summary>Giữ camera ở FreeLook (1 lệnh ghi gộp).</summary>
    public void Maintain()
    {
        if (_freeLook == 0 || SoloSlot() == 0) return;
        Session.Memory.WriteMany([(SoloSlot(), Bin.Pack(_freeLook)), (_freeLook + F(_freeLook, "m_YAxisRecentering"), [0]),
            (_freeLook + F(_freeLook, "m_RecenterToTargetHeading"), [0])]);
        Session.Camera.ClearLookAt();
    }

    /// <summary>Nhả: bỏ solo camera, trả cờ tự quay về.</summary>
    public void Release()
    {
        if (_freeLook == 0) return;
        var writes = new List<(long, byte[])> { (_freeLook + F(_freeLook, "m_YAxisRecentering"), _yRecenter!), (_freeLook + F(_freeLook, "m_RecenterToTargetHeading"), _headingRecenter!) };
        if (SoloSlot() != 0) writes.Add((SoloSlot(), new byte[8]));
        Session.Optional(() => Session.Memory.WriteMany(writes), "nhả POV");
        _freeLook = 0;
    }
}

/// <summary>
/// Cá cắn nhanh: phao FakeCountMin / FakeCountMax = 0 (không rỉa giả); bóng cá _remainTouchCnt (EncryptInt) về 0 bằng cách đặt randValue =
/// giá trị đang lưu. Nhớ số gốc của phao để trả khi tắt.
/// </summary>
public sealed class FastBite(FishingGame game)
{
    private long _float, _shadow;
    private byte[]? _original;
    private bool _applied;

    /// <summary>Lượt quăng mới.</summary>
    public void NewCast() => (_applied, _shadow) = (false, 0);

    /// <summary>Quên phao (lượt kết thúc bình thường - game tự dựng phao mới).</summary>
    public void Forget() => (_float, _original, _applied, _shadow) = (0, null, false, 0);

    /// <summary>Áp lên phao (1 lần / lượt) + bóng cá (1 lần / bóng).</summary>
    public void Apply()
    {
        var s = game.Session;
        var flt = game.Float();
        if (!_applied && Bin.IsPtr(flt))
        {
            var at = s.Il2Cpp.Field(s.Managed.ClassOf(flt), "FakeCountMin");
            _original = s.Memory.Read(flt + at, 8);
            s.Memory.Write(flt + at, new byte[8]);
            (_float, _applied) = (flt, true);
        }
        var shadow = game.ShadowControl();
        if (!Bin.IsPtr(shadow) || shadow == _shadow) return;
        _shadow = shadow;
        var at2 = s.Il2Cpp.Field(s.Managed.ClassOf(shadow), "_remainTouchCnt");
        var raw = s.Memory.Read(shadow + at2, 8);
        if (raw != null && Bin.U32(raw, 0) is var key and not 0 && s.EncryptStored(key) is { } stored) s.Memory.Write(shadow + at2 + 4, Bin.Pack(stored));
    }

    /// <summary>Trả số rỉa gốc cho phao (tắt cắn nhanh / dừng câu).</summary>
    public void Restore()
    {
        if (_float != 0 && _original != null)
        {
            var s = game.Session;
            s.Optional(() => s.Memory.Write(_float + s.Il2Cpp.Field(s.Managed.ClassOf(_float), "FakeCountMin"), _original), "trả phao");
        }
        Forget();
    }
}
