using System.Diagnostics;
using System.Drawing;

namespace AutoAimLock.Engine;

/// <summary>
/// The entire tool in one deterministic loop on ONE thread at a fixed 125 Hz tick.
///
/// Stability-first movement law (the reason this tool doesn't go wild):
///   step = error × gain, gain &lt; 1, measured against the real cursor every tick.
/// Pure exponential convergence — the error shrinks geometrically and the law
/// contains no momentum, no velocity memory, no prediction and no filters, so
/// oscillation is mathematically impossible. A hard per-tick step clamp caps
/// speed, and a hysteresis deadzone (engage at Deadzone, release only beyond
/// Deadzone×Hysteresis) makes the lock sit still on the target instead of
/// micro-hunting around it.
///
/// No-recoil: continuous sub-pixel counter-pull while the fire button is held
/// (8ms micro-ticks, fractional accumulator, random spread, occasional skip).
/// Rapid-fire: click state machine paced to the configured RPM with a release
/// debounce so the synthetic up doesn't abort the train.
/// </summary>
public sealed class AimEngine : IDisposable
{
    readonly Grabber _grab = new();
    Settings _settings = new();
    Thread? _thread;
    volatile bool _run;

    // Hysteresis state
    bool _inDeadzone;

    long _lockSince = long.MinValue; // when current lock began (acquisition soft-start)

    public volatile bool Locked;
    public int Fps { get; private set; }

    public event Action? Stats;

    const int TickMs = 8;

    public void Start(Settings s)
    {
        _settings = s;
        if (_run) return;
        _run = true;
        _thread = new Thread(Loop) { IsBackground = true, Priority = ThreadPriority.AboveNormal, Name = "AimLock" };
        _thread.Start();
    }

    public void Stop()
    {
        _run = false;
        _thread = null;
        Locked = false;
    }

    void Loop()
    {
        var sw = Stopwatch.StartNew();
        int frames = 0;
        long fpsAt = 0;
        long now;

        while (_run)
        {
            try
            {
                now = sw.ElapsedMilliseconds;

                // Pure aim-lock: no click/recoil here — those run in the
                // AegisMouse MacroEngine (single macro driver) to avoid double input.

                int grabSize = Math.Clamp(_settings.Fov * 2, 220, 460);
                var bmp = _grab.Grab(grabSize, out int curX, out int curY, out _, out _);
                if (bmp == null) { SleepTick(); continue; }

                bool wasLocked = Locked;
                bool found = Detector.Find(bmp, _settings.ColorBand, curX, curY,
                    _settings.Fov, _settings.MinPixels, out double tx, out double ty, out _);
                Locked = found;
                if (found && !wasLocked) _lockSince = now; // new acquisition

                // ── Movement: pure exponential convergence ──
                if (found)
                {
                    double errX = tx - curX;
                    double errY = ty - curY;
                    double dist = Math.Sqrt(errX * errX + errY * errY);

                    if (dist <= _settings.Deadzone)
                    {
                        _inDeadzone = true;              // sit still — ON target
                    }
                    else if (_inDeadzone && dist < _settings.Deadzone * _settings.Hysteresis)
                    {
                        // hysteresis band: keep still, prevents hunt/oscillation
                    }
                    else
                    {
                        _inDeadzone = false;
                        double gain = Math.Clamp(_settings.GainPct / 100.0, 0.05, 0.60);
                        double sx = errX * gain, sy = errY * gain;

                        // Hard per-tick cap (speed limiter) + acquisition soft-start:
                        // a NEWLY acquired lock eases in at ≤2px/tick for 150ms so
                        // pointing near walls/scenery can never yank the view.
                        double mag = Math.Sqrt(sx * sx + sy * sy);
                        int maxStep = Math.Max(1, _settings.MaxStep);
                        if (now - _lockSince < 150) maxStep = Math.Min(maxStep, 2);
                        if (mag > maxStep)
                        {
                            sx = sx * maxStep / mag;
                            sy = sy * maxStep / mag;
                        }

                        Native.Move((int)Math.Round(sx), (int)Math.Round(sy));
                    }
                }
                else
                {
                    _inDeadzone = false;
                }

                // ── Stats ──
                frames++;
                if (now - fpsAt >= 1000)
                {
                    Fps = frames;
                    frames = 0;
                    fpsAt = now;
                    Stats?.Invoke();
                }

                SleepTick();
            }
            catch
            {
                SleepTick();
            }
        }
    }

    static void SleepTick() => Thread.Sleep(TickMs);

    public void Dispose()
    {
        Stop();
        _grab.Dispose();
    }
}
