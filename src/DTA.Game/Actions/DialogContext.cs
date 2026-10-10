using System.Collections.Concurrent;
using DTA.Game.Invoke;
using DTA.Game.Session;
using DTA.Runtime.Core;

namespace DTA.Game.Actions;

/// <summary>Hành động muốn làm với 1 bảng; chỉ bảng kết quả cá / đào phụ thuộc cấu hình bot.</summary>
public enum DialogAction { Ok, No, Close, Sell, Keep, Open, Repair, Buy }

/// <summary>
/// Công cụ chung cho mọi bộ xử lý bảng: gọi hàm game lên bảng, tìm nút theo tên field / từ khoá, bấm UIButton, chuỗi dự phòng,
/// khoá "bấm 1 lần / bảng" (bấm bán lần 2 lúc chờ server là lỗi #11502). Dùng chung theo phiên.
/// </summary>
public sealed class DialogContext(GameSession session)
{
    private const int GateLimit = 128;
    private static readonly TimeSpan GateExpire = TimeSpan.FromSeconds(10);
    private readonly ConcurrentDictionary<(string Kind, long Dialog), DateTime> _gates = new();
    private readonly ConcurrentDictionary<(long Klass, string Field), int> _fields = new();

    public GameSession Session { get; } = session;

    /// <summary>Gọi hàm game với this = bảng (Invoker tự kiểm class chủ).</summary>
    public bool Call(GameFunction function, long dialog, params ulong[] args) => Session.Invoker.Call(function, dialog, args, alongside: TakePending());

    /// <summary>Ô nhớ chờ ghi kèm lần gọi hàm game kế (CloseAniSkip...) - gộp vào cùng lệnh shell cho nhanh.</summary>
    private readonly ThreadLocal<List<(long, byte[])>> _pending = new(() => []);

    /// <summary>Lấy hết ô chờ ghi (để ghi kèm lần gọi hàm).</summary>
    private List<(long, byte[])> TakePending()
    {
        var list = _pending.Value!.ToList();
        _pending.Value!.Clear();
        return list;
    }

    /// <summary>Ghi ngay các ô còn chờ (bộ xử lý không gọi hàm game nào).</summary>
    public void FlushPending()
    {
        var list = TakePending();
        if (list.Count > 0) Session.Memory.WriteMany(list);
    }

    /// <summary>Đóng an toàn bằng DialogUnit.DialogCloseFromBack (chuẩn mọi bảng).</summary>
    public bool CloseBack(long dialog) => Call(Fn.DialogCloseFromBack, dialog);

    /// <summary>Bấm UIButton qua UIButton.OnClick.</summary>
    public bool Click(long button) => Bin.IsPtr(button) && Session.Invoker.Call(Fn.UiButtonClick, button, alongside: TakePending());

    /// <summary>Con trỏ nút ở field đầu tiên có trong class bảng (offset nhớ theo class).</summary>
    public long Button(long dialog, params string[] fields)
    {
        var klass = Session.Managed.ClassOf(dialog);
        if (klass == 0) return 0;
        foreach (var field in fields)
        {
            var offset = _fields.GetOrAdd((klass, field), k => Session.Il2Cpp.HasField(k.Klass, k.Field) ? Session.Il2Cpp.Field(k.Klass, k.Field) : 0);
            var ptr = offset > 0 ? (long)Session.Memory.U64(dialog + offset) : 0;
            if (Bin.IsPtr(ptr)) return ptr;
        }
        return 0;
    }

    /// <summary>Nút đầu tiên có tên field chứa 1 trong các từ khoá (không phân biệt hoa thường).</summary>
    public long ButtonLike(long dialog, params string[] keywords)
    {
        foreach (var (field, offset) in Session.Il2Cpp.AllFields(Session.Managed.ClassOf(dialog)))
        {
            if (!keywords.Any(k => field.Contains(k, StringComparison.OrdinalIgnoreCase))) continue;
            var ptr = (long)Session.Memory.U64(dialog + offset);
            if (Bin.IsPtr(ptr)) return ptr;
        }
        return 0;
    }

    /// <summary>Bấm nút theo field, không có thì theo từ khoá.</summary>
    public bool ClickAny(long dialog, string[] fields, string[]? keywords = null)
    {
        var button = Button(dialog, fields);
        if (button == 0 && keywords != null) button = ButtonLike(dialog, keywords);
        return button != 0 && Click(button);
    }

    /// <summary>Chạy lần lượt tới bước đầu tiên thành công.</summary>
    public static bool FirstOf(params Func<bool>[] steps) => steps.Any(step => step());

    /// <summary>True = được làm <paramref name="kind"/> trên bảng (lần đầu trong <paramref name="window"/>) và ghi nhận; false = vừa làm rồi.</summary>
    public bool Once(string kind, long dialog, TimeSpan window)
    {
        var now = DateTime.UtcNow;
        if (_gates.Count > GateLimit)
            foreach (var key in _gates.Where(p => now - p.Value > GateExpire).Select(p => p.Key).ToList()) _gates.TryRemove(key, out _);
        if (Locked(kind, dialog, window)) return false;
        _gates[(kind, dialog)] = now;
        return true;
    }

    /// <summary>Bảng vừa được làm <paramref name="kind"/> trong <paramref name="window"/> (chỉ kiểm).</summary>
    public bool Locked(string kind, long dialog, TimeSpan window) =>
        _gates.TryGetValue((kind, dialog), out var at) && DateTime.UtcNow - at < window;

    /// <summary>
    /// Bỏ hoạt ảnh đóng của bảng (DialogUnit.CloseAniSkip = 1): đóng là biến mất ngay. Chỉ bỏ hoạt ảnh đóng, giữ nguyên hình của game -
    /// làm vô hình bảng (alpha / tắt camera) thì màn hình nháy đen trắng. Mỗi bảng ghi 1 lần, ghi kèm lần gọi hàm game kế.
    /// </summary>
    public void SkipCloseAnimation(long dialog)
    {
        var klass = Session.Managed.ClassOf(dialog);
        var offset = klass != 0 ? _fields.GetOrAdd((klass, "CloseAniSkip"), k => Session.Il2Cpp.HasField(k.Klass, k.Field) ? Session.Il2Cpp.Field(k.Klass, k.Field) : 0) : 0;
        if (offset > 0 && Once("closeskip", dialog, GateExpire)) _pending.Value!.Add((dialog + offset, [1]));
    }

    /// <summary>Tên class (kèm class cha) của bảng.</summary>
    public IReadOnlyList<string> Names(long dialog) => Session.Managed.Names(dialog);
}
