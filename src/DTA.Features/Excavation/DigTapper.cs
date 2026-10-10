using DTA.Runtime.Core;

namespace DTA.Features.Excavation;

/// <summary>
/// Luồng gõ xẻng riêng, độc lập vòng đọc bộ nhớ (gọi hàm đào, không bấm màn hình). Đào thường: chỉ gõ khi nhân vật đã vung xong nhát
/// trước (ExcavateState None, cách ≥ 0,15 s) nên 1 lần gõ = 1 nhát thật, và hỏi <c>allow</c> trước mỗi nhát (đủ nhát thì thôi - không đào
/// thừa tốn xẻng); đếm số nhát đã gõ (<see cref="Taps"/>). Đảo: hết vung là đào tiếp (≥ 0,12 s), lâu nhất 0,35 s. Mọi lệnh qua khoá hook
/// chung nên không bao giờ hook chồng.
/// </summary>
public sealed class DigTapper : IDisposable
{
    private const double SteadyGap = 0.15, IslandMax = 0.35, IslandMin = 0.12;
    private static readonly Logger L = Log.For("excavation");
    private readonly ExcavationGame _game;
    private readonly ManualResetEventSlim _active = new(false);
    private volatile bool _stopped, _steady;
    private volatile Func<bool>? _allow;
    private int _taps;

    public DigTapper(ExcavationGame game)
    {
        _game = game;
        new Thread(Run) { IsBackground = true, Name = "DigTapper" }.Start();
    }

    public bool Active => _active.IsSet;

    /// <summary>Số nhát đã gõ từ khi tạo luồng (đếm tăng dần).</summary>
    public int Taps => Volatile.Read(ref _taps);

    /// <summary>Bắt đầu gõ; <paramref name="steady"/> = đào thường; <paramref name="allow"/> = còn được gõ nhát nữa không (null = luôn được).</summary>
    public void Start(bool steady = false, Func<bool>? allow = null)
    {
        (_steady, _allow) = (steady, allow);
        _active.Set();
    }

    public void Pause() => _active.Reset();

    public void Dispose()
    {
        _stopped = true;
        _active.Set();
    }

    private void Run()
    {
        double last = 0;
        while (!_stopped)
        {
            if (!_active.Wait(50) || _stopped) continue;
            var since = Environment.TickCount64 / 1000.0 - last;
            try
            {
                var due = _steady ? since >= SteadyGap && _game.State() == ExcavateState.None
                    : since >= IslandMax || (since >= IslandMin && _game.State() == ExcavateState.None);
                if (due && _allow?.Invoke() != false && _game.Session.Tools.Dig())
                {
                    Interlocked.Increment(ref _taps);
                    last = Environment.TickCount64 / 1000.0;
                }
            }
            catch (Exception e)
            {
                L.Debug($"Gõ xẻng lỗi: {e.Message}");
                last = Environment.TickCount64 / 1000.0;
            }
            Thread.Sleep(5);
        }
    }
}
