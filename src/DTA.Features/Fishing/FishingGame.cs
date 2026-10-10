using System.Text.RegularExpressions;
using DTA.Game.Data;
using DTA.Game.Session;
using DTA.Runtime.Core;

namespace DTA.Features.Fishing;

/// <summary>Đọc game riêng của câu cá: trạng thái câu, ID cá, HP cá lớn, bảng cỡ bóng, đặc điểm cá vừa câu, phao + bóng cá.</summary>
public sealed partial class FishingGame(GameSession session)
{
    public const string DifficultyTable = "TableFishingDifficultyImpl";
    private static readonly string[] ControlFields = ["_fishingState", "_catchFishID", "_catchFishSize", "_hiddenLevel"];
    private static readonly Dictionary<string, int> ShadowTiers = new() { ["s"] = 1, ["m"] = 2, ["l"] = 3, ["xl"] = 4, ["xxl"] = 5, ["xxxl"] = 6, ["4xl"] = 7 };
    private static readonly Regex ShadowAsset = new(@"^fish_(\w+?)_shadow(_\w+)?$", RegexOptions.Compiled);

    private int[] _offsets = [];
    private int _span;
    private long _controlClass;
    private int _lastState;
    private (int Max, int Current)? _hpLayout;

    public GameSession Session { get; } = session;

    /// <summary>Kiểm kiểu điều khiển + offset các field (đổi object điều khiển thì dò lại).</summary>
    private void CheckControl()
    {
        if (Session.ControlClass == 0) Session.LocateActor();
        if (Session.ControlClass == _controlClass) return;
        if (!Session.Il2Cpp.Is(Session.ControlClass, "ActorDefaultControl")) throw new GameError("Nhân vật đang ở trạng thái không câu cá được (đang lái xe?)");
        _controlClass = Session.ControlClass;
        _offsets = ControlFields.Select(f => Session.Il2Cpp.Field(_controlClass, f)).ToArray();
        _span = _offsets.Max() + 16;
    }

    /// <summary>
    /// Trạng thái câu (1 lượt đọc ~6 ms). _fishingState là ObscuredInt {hash, hidden, key, fake}: giá trị = (hidden - key) XOR key; đọc trúng
    /// lúc game đang mã hoá lại (&gt; 64) thì giữ trạng thái trước.
    /// </summary>
    public FishState Poll()
    {
        CheckControl();
        var data = Session.Memory.Read(Session.Control, _span);
        if (data == null || Bin.U64(data, 0) != _controlClass)
        {
            Session.LocateActor();
            CheckControl();
            data = Session.Memory.Read(Session.Control, _span) ?? throw new GameError("Không đọc được trạng thái nhân vật", true);
        }
        uint hidden = Bin.U32(data, _offsets[0] + 4), key = Bin.U32(data, _offsets[0] + 8);
        var state = (int)((hidden - key) ^ key);
        if (state is < 0 or > FishStates.Max) state = _lastState;
        _lastState = state;
        return new FishState(state, (int)Bin.U32(data, _offsets[1]), Bin.I32(data, _offsets[2]), Bin.U32(data, _offsets[3]), Bin.I32(data, _offsets[3] + 4));
    }

    /// <summary>ID cá (FishingDifficultyID) - có ngay khi bóng cá hiện. Game hay mã hoá lại: đọc xong kiểm cặp (key, rand) không đổi mới tin.</summary>
    public int FishId(FishState state)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (state.LevelKey == 0) return 0;
            var value = Session.DecodeEncryptInt(state.LevelKey, state.LevelRand);
            var check = Poll();
            if ((check.LevelKey, check.LevelRand) == (state.LevelKey, state.LevelRand)) return value is > 0 and < 100_000_000 ? value : 0;
            state = check;
        }
        return 0;
    }

    /// <summary>(HP còn, tối đa) cá lớn đang đấu (sysFishing.BigFishMaxHP + _bigFishHP mã hoá).</summary>
    public (int Hp, int Max)? BigFishHp() => Session.Optional<(int, int)?>(() =>
    {
        var system = Session.System("sysFishing");
        if (system == 0) return null;
        var klass = Session.Managed.ClassOf(system);
        _hpLayout ??= (Session.Il2Cpp.Field(klass, "BigFishMaxHP"), Session.Il2Cpp.Field(klass, "_bigFishHP"));
        var raw = Session.Memory.ReadMany([(system + _hpLayout.Value.Max, 4), (system + _hpLayout.Value.Current, 8)]);
        if (raw is not [{ } max, { } current]) return null;
        var maximum = Bin.I32(max, 0);
        var key = Bin.U32(current, 0);
        var hp = key != 0 ? Session.DecodeEncryptInt(key, Bin.I32(current, 4)) : 0;
        return hp >= 0 && hp <= maximum && maximum > 0 ? (hp, maximum) : null;
    }, "HP cá lớn");

    /// <summary>Cỡ bóng (1..7) mọi ID cá theo cột AssetName bảng FishingDifficulty (fish_xl_shadow_mutant: xl = 4); nhớ theo phiên bản game.</summary>
    public Dictionary<int, int> Shadows()
    {
        var saved = Session.Cache.Get<Dictionary<int, int>>("shadows");
        if (saved is { Count: > 0 }) return saved;
        saved = Session.Optional(() =>
        {
            var rows = Session.Tables.Rows(DifficultyTable).Select(r => r.Row).ToList();
            if (rows.Count == 0) return null;
            var klass = Session.Managed.ClassOf(rows[0]);
            int idAt = Session.Il2Cpp.Field(klass, "FishingDifficultyId"), assetAt = Session.Il2Cpp.Field(klass, "AssetName");
            var data = Session.Memory.ReadObjects(rows, Math.Max(idAt + 4, assetAt + 8));
            var texts = Session.Memory.Strings(data.Values.Select(d => Bin.U64(d, assetAt)));
            var result = new Dictionary<int, int>();
            foreach (var d in data.Values)
                if (ShadowAsset.Match(texts.GetValueOrDefault(Bin.U64(d, assetAt), "")) is { Success: true } m && ShadowTiers.TryGetValue(m.Groups[1].Value, out var tier))
                    result[(int)Bin.U32(d, idAt)] = tier;
            return result;
        }, "bảng cỡ bóng") ?? [];
        if (saved.Count > 0) Session.Cache.Put("shadows", saved);
        return saved;
    }

    /// <summary>Cấp nền (bảng Item) các loài cá; loài bảng Item không có thì ghi 0 để khỏi đọc lại.</summary>
    public void LoadGrades(IEnumerable<int> itemIds, Dictionary<int, int> into)
    {
        foreach (var item in itemIds.Where(i => !into.ContainsKey(i)))
            into[item] = Session.Tables.Item(item).Grade;
    }

    /// <summary>Đặc điểm cá trong bảng kết quả đang mở (gọi sau khi đã đọc được tên cá).</summary>
    public CatchTraits Traits()
    {
        var dialog = Session.Open.ResultDialog();
        var mutant = Session.Optional<bool?>(() =>
        {
            var asset = Session.Managed.Ptr(Session.Managed.Ptr(dialog, "_item"), "AssetName");
            var name = asset != 0 ? Session.Memory.Strings([asset], 80).GetValueOrDefault(asset, "") : "";
            return name.Length > 0 ? name.Contains("mutant", StringComparison.OrdinalIgnoreCase) : null;
        }, "cá đột biến");
        var bits = Session.Optional(() => MutationBits(dialog), "biến thể");
        return new CatchTraits(mutant, bits == null ? null : bits != 0, bits is { } b ? FishMutations.FromBits(b) : []);
    }

    /// <summary>
    /// Cờ biến thể của cá vừa câu: bảng kết quả _userItem[0] (không có thì _sellItem) -> UserItemOption -> ItemExtraData.Mutations. Không có
    /// ItemExtraData = không biến thể (0); không thấy vật phẩm = chưa đọc được (null).
    /// </summary>
    private uint? MutationBits(long dialog)
    {
        var m = Session.Managed;
        var item = m.ListItems(m.Ptr(dialog, "_userItem")).FirstOrDefault(i => i != 0);
        if (item == 0) item = m.Ptr(dialog, "_sellItem");
        if (item == 0) return null;
        var extra = m.Ptr(m.Ptr(item, "<UserItemOption>k__BackingField"), "<ItemExtraData>k__BackingField");
        return extra == 0 ? 0 : (uint?)m.I32(extra, "<Mutations>k__BackingField");
    }

    /// <summary>Phao (ActorDefaultControl._fishingFloat); 0 nếu chưa quăng.</summary>
    public long Float() => Session.Control != 0 ? Session.Managed.Ptr(Session.Control, "_fishingFloat") : 0;

    /// <summary>Bóng cá quanh phao (FishingFloatController._shadowControl); 0 nếu chưa hiện.</summary>
    public long ShadowControl() => Float() is var f and not 0 ? Session.Managed.Ptr(f, "_shadowControl") : 0;
}
