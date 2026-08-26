using AutoAimLock.Core;

namespace AutoAimLock.Timing;

/// <summary>Human-like jitter on synthetic timings/moves. Seeded for deterministic tests.</summary>
public sealed class Humanizer
{
    readonly Random _rng;
    readonly object _lock = new();
    public Humanizer(int? seed = null) => _rng = seed is { } s ? new Random(s) : new Random();

    public int Delay(int ms, HumanizeSettings h) => h.Enabled && h.DelayJitterMs > 0 ? Math.Max(0, ms + Next(-h.DelayJitterMs, h.DelayJitterMs + 1)) : ms;
    public int Hold(int ms, HumanizeSettings h) => h.Enabled && h.HoldJitterMs > 0 ? Math.Max(1, ms + Next(-h.HoldJitterMs, h.HoldJitterMs + 1)) : Math.Max(1, ms);
    public (int dx, int dy) Move(int dx, int dy, HumanizeSettings h)
    {
        if (!h.Enabled || h.MoveJitterCounts <= 0) return (dx, dy);
        int j = h.MoveJitterCounts;
        return (dx + Next(-j, j + 1), dy + Next(-j, j + 1));
    }
    public bool SkipStep(HumanizeSettings h) => h.Enabled && h.StepSkipPercent > 0 && Next(0, 100) < h.StepSkipPercent;

    int Next(int min, int max) { lock (_lock) return _rng.Next(min, max); }
}
