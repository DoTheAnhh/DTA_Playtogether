namespace DTA.Features.Fishing;

/// <summary>
/// Sổ ghi vùng câu giả (thuần, không đọc / ghi bộ nhớ - để kiểm thử được): nhớ ID gốc của từng FishingZoneInfo đã ghi đè, tính các ô cần
/// ghi khi bật / đổi vùng giả và các ô cần trả khi tắt. Chỉ làm việc với object còn sống (<c>live</c> = {object: ID hiện tại} vừa đọc lại);
/// object không còn trong <c>live</c> (game đã giải phóng) thì không bao giờ ghi.
/// </summary>
public sealed class FakeZoneLedger
{
    /// <summary>ID vùng câu hợp lệ (như game: số dương, nhỏ hơn 1 triệu).</summary>
    public const int MaxZoneId = 1_000_000;

    private readonly Dictionary<long, uint> _originals = [];

    /// <summary>Đang có ô bị ghi đè (cần trả khi tắt).</summary>
    public bool Active => _originals.Count > 0;

    public static bool IsValid(int zoneId) => zoneId is > 0 and < MaxZoneId;

    /// <summary>ID gốc của 1 ô (đã ghi đè thì lấy bản nhớ, chưa thì chính ID hiện tại).</summary>
    public uint Original(long info, uint current) => _originals.GetValueOrDefault(info, current);

    /// <summary>
    /// Bật / đổi vùng giả <paramref name="zoneId"/>: các ô còn sống đang khác ID giả -> (ô, ID giả) cần ghi; nhớ ID gốc lần đầu (đổi sang vùng
    /// giả khác vẫn giữ ID gốc thật, không nhớ nhầm ID giả cũ). <paramref name="known"/> = ID gốc biết trước (lúc quét). ID không hợp lệ -> rỗng.
    /// </summary>
    public List<(long Info, uint Id)> Apply(IReadOnlyDictionary<long, uint> live, int zoneId, IReadOnlyDictionary<long, uint>? known = null)
    {
        if (!IsValid(zoneId)) return [];
        var writes = new List<(long, uint)>();
        foreach (var (info, current) in live)
        {
            if (current == (uint)zoneId) continue;
            _originals.TryAdd(info, known?.GetValueOrDefault(info, current) ?? current);
            writes.Add((info, (uint)zoneId));
        }
        return writes;
    }

    /// <summary>Tắt vùng giả: (ô còn sống, ID gốc) cần ghi trả; xoá sổ.</summary>
    public List<(long Info, uint Id)> Restore(IReadOnlyDictionary<long, uint> live)
    {
        var writes = _originals.Where(o => live.ContainsKey(o.Key)).Select(o => (o.Key, o.Value)).ToList();
        _originals.Clear();
        return writes;
    }

    /// <summary>Các ô đã ghi đè (để đọc lại trạng thái sống trước khi trả).</summary>
    public IReadOnlyCollection<long> Touched => _originals.Keys;

    /// <summary>Đổi cảnh: object cũ không còn - quên hết, không ghi trả.</summary>
    public void Forget() => _originals.Clear();
}
