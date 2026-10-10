using System.Buffers.Binary;

namespace DTA.Game.Invoke;

/// <summary>
/// Dựng payload ARM64 cho hook 1 lần vào slot OnUpdate: tự trả slot về hàm gốc ngay khi chạy, nạp tham số x0..x6, gọi hàm game, khôi phục
/// x0 (this) + d0 (deltaTime) rồi nhảy đuôi về OnUpdate gốc. Dãy LỆNH LUÔN Y HỆT nhau (mọi tham số nạp từ vùng dữ liệu, thiếu thì 0): giả lập
/// x86 (houdini) nhớ đệm bản dịch của vùng mã, nếu lệnh đổi giữa các lần gọi nó có thể chạy bản dịch cũ trên dữ liệu mới -> văng game.
/// </summary>
public static class Arm64Payload
{
    /// <summary>Lệnh đầu mọi payload của tool (stp x29, x30, [sp, #-0x40/-0x50]!) - nhận ra vùng trampoline đã bị ghi đè.</summary>
    public static readonly uint[] Heads = [0xA9BC7BFD, 0xA9BB7BFD];
    /// <summary>Số thanh ghi tham số (x0..x6) - đủ cho this + 5 tham số + MethodInfo.</summary>
    public const int MaxArgs = 7;

    /// <summary>
    /// Payload gọi <paramref name="function"/>(args...). <paramref name="args"/> gồm cả this ở đầu và MethodInfo* = 0 ở cuối. Dữ liệu sau mã:
    /// [gốc, slot, hàm, x0..x6]. Dài cố định 0xA8 byte.
    /// </summary>
    public static byte[] Build(ulong original, long slot, long function, IReadOnlyList<ulong> args)
    {
        if (args.Count > MaxArgs) throw new ArgumentException($"Tối đa {MaxArgs} tham số", nameof(args));
        var literals = new List<ulong> { original, (ulong)slot, (ulong)function };
        literals.AddRange(Enumerable.Range(0, MaxArgs).Select(i => i < args.Count ? args[i] : 0));
        var code = new List<Func<int, uint>>
        {
            _ => 0xA9BC7BFD,
            _ => 0x910003FD,
            _ => 0xF90013E0,
            _ => 0xFD0017E0,
            Load(8, 0),
            Load(9, 1),
            _ => 0xF9000128,
        };
        for (var i = 0; i < MaxArgs; i++) code.Add(Load(i, 3 + i));
        code.AddRange([Load(16, 2), _ => 0xD63F0200, _ => 0xF94013E0, _ => 0xFD4017E0, Load(16, 0), _ => 0xA8C47BFD, _ => 0xD61F0200]);
        if (code.Count % 2 == 1) code.Add(_ => 0xD503201F);
        var dataAt = code.Count * 4;
        var payload = new byte[dataAt + literals.Count * 8];
        for (var i = 0; i < code.Count; i++) BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(i * 4), code[i](dataAt - i * 4));
        for (var i = 0; i < literals.Count; i++) BinaryPrimitives.WriteUInt64LittleEndian(payload.AsSpan(dataAt + i * 8), literals[i]);
        return payload;
    }

    /// <summary>"ldr xRt, [pc, #off]" tới ô dữ liệu thứ <paramref name="literal"/>; nhận khoảng cách từ lệnh tới đầu vùng dữ liệu.</summary>
    private static Func<int, uint> Load(int rt, int literal) => distance => 0x58000000u | ((uint)((distance + literal * 8) >> 2) & 0x7FFFF) << 5 | (uint)rt;
}
