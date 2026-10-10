using DTA.Engine.Bots;
using DTA.Engine.Movement;
using DTA.Runtime.Core;

namespace DTA.Features.Excavation;

/// <summary>Phần đào bản đồ thường + nhận quà của <see cref="ExcavationBot"/>.</summary>
public sealed partial class ExcavationBot
{
    private const double HitTimeout = 0.15, BlindGap = 3, ClaimWait = 3.5, ClaimGap = 0.25, ArmedFor = 0.3, RevealedStall = 60;
    private const int MissLimit = 3, BlindHits = 4, ClaimTries = 4;

    /// <summary>
    /// Vòng đào: xẻng chạm trong tầm + không bảng (vừa soát ≤ 0,3 s) thì luồng riêng gõ đều 10 lần / giây; hụt / phải nhích lại / có bảng
    /// thì dừng gõ. Đào đúng số nhát cần: sát thương 1 nhát đo ở nhát đầu sau khi lộ (gõ từng nhát tới khi biết), rồi chỉ gõ khi (nhát đã gõ
    /// mà máu chưa trừ) × sát thương còn nhỏ hơn máu còn lại - máu cập nhật trễ cũng không gõ lố (gõ lố = vung xuống đất, tốn xẻng). Điểm CHƯA LỘ: gõ mãi (3 s / 4
    /// nhát) mà không đổi hoặc hụt 3 lần thì bỏ. Điểm ĐÃ LỘ cổ vật: đào tới khi lên đồ, nhận được thưởng - hụt / không ăn thì đứng lại chỗ
    /// khác rồi đào tiếp; chỉ bỏ khi không tới được hoặc 60 s liền máu không giảm.
    /// </summary>
    private bool DigRegular(Spot spot)
    {
        var offset = _game.ShovelOffset();
        var name = Label(spot);
        int misses = 0, blind = 0, hit = 0, landedAt = Tapper.Taps;
        double pending = 0, checkedAt = Now, refreshed = 0, armed = 0, changed = Now;
        var closer = false;
        var previous = ExcavateState.None;
        while (Running)
        {
            var now = Now;
            var state = _game.State();
            if (state != ExcavateState.None) pending = 0;
            if (state == ExcavateState.Miss && previous != ExcavateState.Miss)
            {
                (misses, closer, armed, landedAt) = (misses + 1, true, 0, Tapper.Taps);
                if (spot.Kind != 0 && now - changed > RevealedStall) return Give($"Đào {name} mãi không ăn - chuyển sang điểm khác...");
                if (spot.Kind == 0 && misses >= MissLimit) return Give("Đào hụt nhiều lần ở điểm này - chuyển sang điểm khác...");
            }
            previous = state;
            if (armed != 0 && now - armed < ArmedFor && !closer && blind < BlindHits && state != ExcavateState.Miss)
            {
                if (!Tapper.Active)
                {
                    Tapper.Start(steady: true, () => NeedsHit(spot, hit, Tapper.Taps - landedAt));
                    changed = now;
                }
                else if (now - changed > BlindGap) blind = BlindHits;
                LastAction = now;
            }
            else if (Tapper.Active) Tapper.Pause();
            if ((state != ExcavateState.None || Tapper.Active) && now - refreshed < 0.1)
            {
                Thread.Sleep(20);
                continue;
            }
            refreshed = now;
            var fresh = _game.Reload(spot);
            if (fresh == null || fresh.Done) Tapper.Pause();
            if (fresh == null) return true;
            if (fresh.Kind != 0)
            {
                Source = _game.KindName(fresh.Kind);
                if (fresh.Done) return Claim(fresh);
                if (ChestFilter && !_game.IsChest(fresh.Kind))
                {
                    LastAction = 0;
                    Events.Status($"Lộ ra {Source}, không phải rương - chuyển sang điểm khác...", Level.Ok);
                    return false;
                }
            }
            if ((fresh.Kind, fresh.Hp) != (spot.Kind, spot.Hp))
            {
                var same = fresh.Kind != 0 && fresh.Kind == spot.Kind && fresh.Hp < spot.Hp;
                if (same && hit == 0) hit = spot.Hp - fresh.Hp;
                var landed = same && hit > 0 ? Math.Max(1, (int)Math.Round((spot.Hp - fresh.Hp) / (double)hit)) : int.MaxValue;
                landedAt = (int)Math.Min(Tapper.Taps, (long)landedAt + landed);
                (pending, blind, changed) = (0, 0, now);
                name = Label(fresh);
                Events.Status($"Đang đào {name} (còn {fresh.Hp}/{fresh.MaxHp})...", Level.Ok);
            }
            spot = fresh;
            if (now - checkedAt > 0.5)
            {
                checkedAt = now;
                if (Session.Open.Top().Addr != 0 || !_game.Spots().ContainsKey(spot.Uid)) return true;
            }
            var reach = (spot.Kind != 0 ? spot.Reach : FirstRange) - HitMargin;
            if (state != ExcavateState.None || Tapper.Active)
            {
                armed = !closer && blind < BlindHits && InShovelRange(spot, offset, reach) && Session.Open.Top().Addr == 0 ? Now : 0;
            }
            else if (pending == 0 || now - pending > HitTimeout)
            {
                if (blind >= BlindHits && spot.Kind != 0 && now - changed <= RevealedStall) (blind, closer, landedAt) = (0, true, Tapper.Taps);
                else if (blind >= BlindHits) return Give(spot.Kind != 0 ? $"Đào {name} mãi không ăn - chuyển sang điểm khác..." : "Đào mà điểm này không thay đổi gì - chuyển sang điểm khác...");
                var (p, _) = Session.Player.Facing() ?? throw new GameError("Không đọc được vị trí nhân vật", true);
                var distance = NavMap.Dist(spot.X, spot.Z, p.X, p.Z);
                Gather.Target(distance, name);
                if (closer || !InShovelRange(spot, offset, reach))
                {
                    closer = false;
                    var result = MoveCloser(spot, Math.Max(Math.Min(distance, reach + offset) - 0.5f, 0.25f));
                    if (result == WalkResult.Lost) throw new GameError("Không đọc được vị trí nhân vật hoặc hướng camera", true);
                    if (result == WalkResult.Stuck) return Give(null);
                    if (result != WalkResult.Arrived) return true;
                    continue;
                }
                if (Session.Open.Top().Addr != 0) return true;
                Session.Tools.Dig();
                pending = LastAction = armed = Now;
                blind++;
            }
            Thread.Sleep(10);
        }
        return true;
    }

    /// <summary>Điểm xẻng chạm đất (trước mặt offset m) nằm trong tầm đào (đã trừ hao).</summary>
    private bool InShovelRange(Spot spot, float offset, float reach) =>
        Session.Player.Facing() is var (p, f) && NavMap.Dist(spot.X, spot.Z, p.X + f.X * offset, p.Z + f.Z * offset) <= reach;

    /// <summary>Bỏ điểm này (báo lý do nếu có).</summary>
    /// <summary>
    /// Còn cần gõ thêm nhát không: chưa lộ / chưa biết sát thương thì từng nhát một (chờ máu đổi); đã biết thì gõ khi số nhát đang chờ máu
    /// trừ (<paramref name="pending"/>) nhân sát thương còn nhỏ hơn máu còn lại.
    /// </summary>
    private static bool NeedsHit(Spot spot, int hit, int pending) =>
        spot.Kind == 0 || hit <= 0 ? pending < 1 : spot.Hp > 0 && (long)pending * hit < spot.Hp;

    private bool Give(string? reason)
    {
        LastAction = 0;
        if (reason != null) Events.Status(reason, Level.Warn);
        return false;
    }

    /// <summary>
    /// Nhận quà cổ vật / rương đã đào xong (loại không tự phát quà): CollectSystem.RequestExcvationReward(uid) (đúng hàm bong bóng gọi),
    /// không được thì bấm bong bóng. Xong khi: có bảng, trạng thái ≥ RewardReq, điểm bị dọn / cất nút. Cách 0,25 s, tối đa 4 lần.
    /// </summary>
    private bool Claim(Spot spot)
    {
        if (!_game.Kinds.TryGetValue(spot.Kind, out var kind) || kind.Auto) return true;
        var name = _game.KindName(spot.Kind);
        Events.Status($"Đã đào xong {name} - đang nhận quà...", Level.Ok);
        Gather.Target(null, "");
        int taps = 0;
        double lastTap = 0, deadline = Now + ClaimWait;
        while (Running && Now < deadline)
        {
            var now = Now;
            if (Session.Open.Top().Addr != 0 || (taps > 0 && _game.State() >= ExcavateState.RewardReq)) return Claimed(now);
            var fresh = _game.Reload(spot);
            if (fresh == null || (taps > 0 && fresh.Button == 0 && !fresh.Claiming)) return Claimed(now);
            if (!fresh.Claiming && now - lastTap >= ClaimGap)
            {
                if (taps >= ClaimTries) break;
                var sent = _game.RequestReward(spot.Uid) || (_game.ClaimButton(fresh) is var button and not 0 && Session.Tools.PressHeadButton(button));
                if (sent) (taps, lastTap, deadline) = (taps + 1, now, now + ClaimWait);
            }
            Thread.Sleep(30);
        }
        if (!Running) return false;
        Source = "";
        Events.Status($"Chưa nhận được quà của {name} (quá thời gian chờ phản hồi) - sẽ thử lại sau", Level.Warn);
        return false;
    }

    private bool Claimed(double now)
    {
        LastAction = now;
        return true;
    }
}
