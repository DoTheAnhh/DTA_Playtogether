using DTA.Game.Session;
using DTA.Runtime.Core;

namespace DTA.Game.Invoke;

/// <summary>
/// Gọi hàm game trên Unity main thread bằng hook 1 lần vào slot OnUpdate trong vtable của object điều khiển nhân vật: ghi payload
/// vào vùng trampoline, đổi slot -> frame kế game chạy payload (tự trả slot, gọi hàm, về OnUpdate gốc). Không chạm màn hình.
/// Dùng chung theo phiên; mọi lần gọi qua <see cref="DeviceLock"/> của tab.
/// </summary>
public sealed class Invoker(GameSession session)
{
    private const int Polls = 120;
    private const int TrampolineSize = 0xA8;
    private const int SettleMs = 60;
    private const int TrampolineDoneMs = 250;
    private const int VtableStart = 0x138, VtableStride = 16, VtableSlots = 24;
    /// <summary>Khoá bộ đệm mã gốc vùng trampoline (v2: bỏ bản cũ có thể đọc nhầm base).</summary>
    private const string TrampolineKey = "trampoline_v2";
    private static readonly Logger L = Log.For("invoke");

    private long _base, _slotClass, _slot, _origClass;
    private ulong _orig;

    /// <summary>
    /// Gọi <paramref name="function"/>(instance, args...) trên main thread; true = game đã chạy. Từ chối nếu object không thuộc
    /// class chủ của hàm. <paramref name="restoreTrampoline"/> = hàm khởi động việc dài (chuyển bản đồ): chờ xong rồi trả mã gốc vùng trampoline.
    /// <paramref name="alongside"/> = ô nhớ ghi kèm ngay trước khi cài hook, cùng 1 lệnh shell (không tốn thêm lượt nào).
    /// </summary>
    public bool Call(GameFunction function, long instance, IReadOnlyList<ulong>? args = null, bool restoreTrampoline = false,
                     IReadOnlyList<(long Addr, byte[] Data)>? alongside = null)
    {
        if (!Bin.IsPtr(instance)) return false;
        if (!session.Il2Cpp.Is(session.Managed.ClassOf(instance), function.Owner))
        {
            L.Warn($"Từ chối gọi {function} lên object 0x{instance:X} không phải {function.Owner}");
            return false;
        }
        using var _ = DeviceLock.For(session.Device.Serial).Acquire();
        try
        {
            return Execute(function, instance, args ?? [], restoreTrampoline, alongside ?? []);
        }
        catch (GameError e)
        {
            L.Warn($"Gọi {function} lỗi: {e.Message}");
            return false;
        }
    }

    /// <summary>Gọi hàm của object điều khiển nhân vật (OnClickFishing, OnPickFieldObject(uid)...).</summary>
    public bool CallActor(GameFunction function, params ulong[] args)
    {
        if (session.Control == 0) session.LocateActor();
        return Call(function, session.Control, args);
    }

    /// <summary>Phần chạy khi đang giữ khoá: chuẩn bị slot / gốc, dựng payload, cài + chờ trong 1 lệnh shell.</summary>
    private bool Execute(GameFunction function, long instance, IReadOnlyList<ulong> args, bool restoreTrampoline, IReadOnlyList<(long, byte[])> alongside)
    {
        if (session.ControlClass == 0) session.LocateActor();
        var controlClass = session.ControlClass;
        if (controlClass == 0) return false;
        var slot = UpdateSlot(controlClass);
        var baseAddr = _base;
        var trampoline = baseAddr + Fn.Trampoline;
        if (_origClass != controlClass || _orig == 0)
        {
            var current = session.Memory.U64(slot);
            if (current == (ulong)trampoline || !Bin.IsPtr((long)current))
            {
                L.Warn("Slot OnUpdate đang bị hook dở - thử lại sau");
                return false;
            }
            (_orig, _origClass) = (current, controlClass);
        }
        var payload = Arm64Payload.Build(_orig, slot, baseAddr + function.Offset, [(ulong)instance, .. args, 0]);
        if (payload.Length > TrampolineSize) throw new GameError($"Payload {function} dài 0x{payload.Length:X} > vùng trampoline");
        RememberTrampoline(trampoline);
        var ran = session.Memory.SwapAndWait(slot, _orig, [.. alongside, (trampoline, payload), (slot, Bin.Pack(trampoline))], Polls);
        if (!ran)
        {
            L.Debug($"{function}: game chưa chạy hook (hết giờ) - giữ khoá thêm {SettleMs} ms");
            Thread.Sleep(SettleMs);
            return false;
        }
        L.Debug($"Đã gọi {function}");
        if (restoreTrampoline)
        {
            Thread.Sleep(TrampolineDoneMs);
            RestoreTrampoline(trampoline);
        }
        return true;
    }

    /// <summary>
    /// Slot OnUpdate trong vtable + xác nhận luôn địa chỉ nạp libil2cpp: base đúng là base mà vtable có ô trỏ đúng base + OnUpdate (giả lập có
    /// thể ánh xạ libil2cpp.so nhiều chỗ). Không khớp base nào thì KHÔNG đoán slot (đoán sai = văng game) mà báo lỗi.
    /// </summary>
    private long UpdateSlot(long controlClass)
    {
        if (_slotClass == controlClass && _base != 0) return _slot;
        var start = controlClass + VtableStart;
        var raw = session.Memory.Read(start, VtableSlots * VtableStride) ?? throw new GameError("Không đọc được vtable của nhân vật", true);
        foreach (var candidate in BaseCandidates())
        {
            var target = candidate + Fn.ActorUpdate.Offset;
            var index = Enumerable.Range(0, VtableSlots).FirstOrDefault(i => Bin.U64(raw, i * VtableStride) == target, -1);
            if (index < 0) continue;
            (_base, _slotClass, _slot) = (candidate, controlClass, start + index * VtableStride);
            return _slot;
        }
        throw new GameError("Không dò được slot OnUpdate (phiên bản game khác bảng hàm?) - không gọi hàm game để tránh văng game");
    }

    /// <summary>Các địa chỉ nạp libil2cpp.so: vùng địa chỉ cao (bản ARM64 thật) trước, rồi các vùng khác.</summary>
    private IEnumerable<long> BaseCandidates()
    {
        var starts = session.Memory.Regions().Where(r => r.Name.EndsWith("libil2cpp.so")).Select(r => r.Start).ToList();
        var high = starts.Where(s => s >= 0x7fff00000000).ToList();
        return new[] { high.Count > 0 ? high.Min() : 0, starts.Count > 0 ? starts.Min() : 0 }.Concat(starts).Where(s => s != 0).Distinct();
    }

    /// <summary>Nhớ mã gốc vùng trampoline (chỉ khi chưa bị payload ghi đè), lưu theo phiên bản game.</summary>
    private void RememberTrampoline(long trampoline)
    {
        if (session.Cache.Get<string>(TrampolineKey) is { Length: >= 2 * TrampolineSize }) return;
        var raw = session.Memory.Read(trampoline, TrampolineSize);
        if (raw == null || Arm64Payload.Heads.Contains(BitConverter.ToUInt32(raw, 0))) return;
        session.Cache.Put(TrampolineKey, Convert.ToHexString(raw));
        session.SaveCache();
        L.Info("Đã nhớ mã gốc vùng trampoline");
    }

    /// <summary>Ghi trả mã gốc vùng trampoline (đang giữ khoá, payload đã chạy xong).</summary>
    private void RestoreTrampoline(long trampoline)
    {
        if (session.Cache.Get<string>(TrampolineKey) is not { Length: >= 2 * TrampolineSize } hex) return;
        if (!session.Memory.Write(trampoline, Convert.FromHexString(hex[..(2 * TrampolineSize)]))) L.Warn("Không trả được mã gốc trampoline");
    }
}
