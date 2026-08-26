using System.Diagnostics;
using AutoAimLock.Core;
using AutoAimLock.Interop;

namespace AutoAimLock.Timing;

/// <summary>Precise macro action scheduler against an absolute ms timeline (Sleep + spin).</summary>
public sealed class MacroPlayer
{
    readonly Humanizer _humanizer;
    public MacroPlayer(int? seed = null) => _humanizer = new Humanizer(seed);

    public void Play(IReadOnlyList<MacroAction> actions, HumanizeSettings humanize, CancellationToken ct)
    {
        if (actions is null || actions.Count == 0) return;
        NativeInput.BeginHighResTimer();
        try
        {
            var sw = Stopwatch.StartNew();
            double timeline = 0;
            foreach (var a in actions)
            {
                ct.ThrowIfCancellationRequested();
                timeline += _humanizer.Delay(Math.Max(0, a.DelayMs), humanize);
                WaitUntil(sw, timeline, ct);
                int reps = Math.Max(1, a.Repeat);
                for (int i = 0; i < reps; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    Execute(a, humanize, ct);
                    if (i < reps - 1) { timeline += _humanizer.Delay(Math.Max(0, a.RepeatIntervalMs), humanize); WaitUntil(sw, timeline, ct); }
                }
            }
        }
        finally { NativeInput.EndHighResTimer(); }
    }

    void Execute(MacroAction a, HumanizeSettings h, CancellationToken ct)
    {
        switch (a.Kind)
        {
            case ActionKind.Click:
                NativeInput.ButtonDown(a.Button); WaitMs(_humanizer.Hold(a.HoldMs, h), ct); NativeInput.ButtonUp(a.Button); break;
            case ActionKind.HoldDown: NativeInput.ButtonDown(a.Button); break;
            case ActionKind.ReleaseUp: NativeInput.ButtonUp(a.Button); break;
            case ActionKind.MoveRelative:
                var (dx, dy) = _humanizer.Move(a.Dx, a.Dy, h); NativeInput.MoveRelative(dx, dy); break;
            case ActionKind.KeyTap: NativeInput.KeyDown(a.Vk); WaitMs(Math.Max(1, a.HoldMs), ct); NativeInput.KeyUp(a.Vk); break;
            case ActionKind.KeyDown: NativeInput.KeyDown(a.Vk); break;
            case ActionKind.KeyUp: NativeInput.KeyUp(a.Vk); break;
        }
    }

    static void WaitUntil(Stopwatch sw, double targetMs, CancellationToken ct)
    {
        double remaining;
        while ((remaining = targetMs - sw.Elapsed.TotalMilliseconds) > 0.25)
        {
            ct.ThrowIfCancellationRequested();
            if (remaining > 4) Thread.Sleep(1); else Thread.SpinWait(80);
        }
    }

    static void WaitMs(int ms, CancellationToken ct)
    {
        var deadline = Environment.TickCount64 + ms;
        while (Environment.TickCount64 < deadline)
        {
            ct.ThrowIfCancellationRequested();
            var rem = deadline - Environment.TickCount64;
            if (rem > 4) Thread.Sleep(1); else Thread.SpinWait(40);
        }
    }
}
