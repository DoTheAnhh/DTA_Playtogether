using DTA.Game.Invoke;

/// <summary>Kiểm offline payload ARM64: giải mã mọi lệnh ldr literal / mov xzr, so với giá trị mong đợi của từng thanh ghi.</summary>
internal static class PayloadCheck
{
    /// <summary>Chạy các ca: gọi không tham số, 1 tham số int, ConnectToZoneMove 7 tham số.</summary>
    public static void Run(Action<string> step)
    {
        const ulong orig = 0x7000_1111_0000, fn = 0x7000_2222_0000;
        const long slot = 0x7000_3333_0000;
        Check(step, "no-arg", [0x7000_4444_0000, 0]);
        Check(step, "int-arg", [0x7000_4444_0000, 1234, 0]);
        Check(step, "zone-move", [0x7000_5555_0000, 1502, 10, 0, 1, 1, 0]);

        void Check(Action<string> log, string name, ulong[] args)
        {
            var payload = Arm64Payload.Build(orig, slot, (long)fn, args);
            var regs = new Dictionary<int, ulong>();
            var loads = new List<(int Reg, ulong Value)>();
            for (var at = 0; at + 4 <= payload.Length; at += 4)
            {
                var ins = BitConverter.ToUInt32(payload, at);
                if ((ins & 0xFF000000) == 0x58000000)
                {
                    var target = at + (int)((ins >> 5) & 0x7FFFF) * 4;
                    loads.Add(((int)(ins & 31), BitConverter.ToUInt64(payload, target)));
                }
                else if ((ins & 0xFFFFFFE0) == 0xAA1F03E0) loads.Add(((int)(ins & 31), 0));
                if (ins == 0xD61F0200) break;
            }
            foreach (var (reg, value) in loads) regs[reg] = value;
            var ok = loads[0] == (8, orig) && loads[1] == (9, (ulong)slot) && loads[^2] == (16, fn) && loads[^1] == (16, orig)
                     && args.Select((v, i) => regs.GetValueOrDefault(i) == v).All(b => b) && payload.Length <= 0xA8;
            var code = Convert.ToHexString(payload, 0, payload.Length - 80);
            log($"{name}: mã {code.GetHashCode():X8} {(ok ? "OK" : "SAI")} dài 0x{payload.Length:X}, nạp {string.Join(" ", loads.Select(l => $"x{l.Reg}=0x{l.Value:X}"))}");
        }
    }
}
