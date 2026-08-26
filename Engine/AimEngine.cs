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
    readonly Random _rng = new();
    Settings _settings = new();
    Thread? _thread;
    volatile bool _run;

    // Hysteresis state
    bool _inDeadzone;

    // Rapid-fire click state machine
    bool _clickDown;
    long _nextClickAt, _clickUpAt;

    // Spray progression (AegisMouse/Lua-style: pull ramps up over the spray)
    long _sprayStartMs; // 0 = not firing
    int _shotIndex;     // shots fired in current spray (drives table progression)
    long _lastShotAt;   // last recoil-only pull time

    public volatile bool Locked;
    public volatile bool Firing;
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
        Firing = false;
        ReleaseClick();
    }

    void Loop()
    {
        var sw = Stopwatch.StartNew();
        int frames = 0;
        long fpsAt = 0;
        int releaseCount = 0;
        long now;

        while (_run)
        {
            try
            {
                now = sw.ElapsedMilliseconds;

                // Fire button with release-debounce. CRITICAL: our own synthetic
                // click-down ALSO sets GetAsyncKeyState — if it reset the release
                // counter, the click train would become self-sustaining after the
                // user lets go (runaway auto-fire). So while a synthetic click is
                // down, the reading is "unknown": the counter is frozen, and only
                // a press detected OUTSIDE our synthetic window counts as held.
                bool rawDown = Native.LeftDown;
                if (_clickDown)
                {
                    // synthetic in flight — leave releaseCount untouched
                }
                else if (rawDown)
                {
                    releaseCount = 0;
                }
                else
                {
                    releaseCount++;
                }
                bool held = releaseCount < 8; // ~64ms of continuous up = released
                Firing = held;

                int grabSize = Math.Clamp(_settings.Fov * 2, 220, 460);
                var bmp = _grab.Grab(grabSize, out int curX, out int curY, out _, out _);
                if (bmp == null) { SleepTick(); continue; }

                bool found = Detector.Find(bmp, _settings.ColorBand, curX, curY,
                    _settings.Fov, _settings.MinPixels, out double tx, out double ty, out _);
                Locked = found;

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

                        // Hard per-tick cap (speed limiter)
                        double mag = Math.Sqrt(sx * sx + sy * sy);
                        int maxStep = Math.Max(1, _settings.MaxStep);
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

                // ── Spray progression (resets the moment firing stops) ──
                if (!held) { _sprayStartMs = 0; _shotIndex = 0; }
                else if (_sprayStartMs == 0) { _sprayStartMs = now; _shotIndex = 0; }

                // ── No-recoil — AegisMouse model ──
                // Per-shot pull from a progression table × strength multiplier,
                // paced at the weapon's fire rate (RPM). Combined with rapid fire:
                // one pull per synthetic click. Recoil-only: one pull per fire-rate
                // interval while the physical button is held.
                if (_settings.RecoilOn && held)
                {
                    int interval = Math.Clamp(60000 / Math.Max(1, _settings.RapidRpm), 20, 5000);
                    if (!_settings.RapidOn)
                    {
                        // Recoil-only (auto weapon): pull once per weapon shot.
                        if (now - _lastShotAt >= interval * Vol(0.85, 1.15))
                        {
                            PerShotPull();
                            _lastShotAt = now;
                        }
                    }
                    // combined mode pulls happen in the clicker below (per click)
                }
                else
                {
                    _lastShotAt = 0;
                }

                // ── Rapid-fire click state machine (volatile cadence) ──
                if (_settings.RapidOn && held)
                {
                    int interval = Math.Clamp(60000 / Math.Max(1, _settings.RapidRpm), 20, 5000);
                    if (!_clickDown && now >= _nextClickAt)
                    {
                        Native.LeftButton(true);
                        _clickDown = true;
                        _clickUpAt = now + 8 + (int)Vol(0, 6);   // hold volatility (8-14ms)
                    }
                    else if (_clickDown && now >= _clickUpAt)
                    {
                        Native.LeftButton(false);
                        _clickDown = false;
                        if (_settings.RecoilOn)
                        {
                            // AegisMouse RapidFireRecoil: one table pull per shot.
                            PerShotPull();
                        }
                        // Delay volatility: ±15% around the weapon interval.
                        _nextClickAt = now + (long)Math.Max(1, (interval - 10) * Vol(0.85, 1.15));
                    }
                }
                else if (_clickDown)
                {
                    ReleaseClick();
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

    void ReleaseClick()
    {
        if (_clickDown)
        {
            Native.LeftButton(false);
            _clickDown = false;
        }
    }

    /// <summary>Random multiplier in [lo, hi) — the Lua "Volatility" pattern
    /// (70-140% on pulls, 80-120% on delays) that makes macros non-robotic.</summary>
    double Vol(double lo, double hi) => lo + _rng.NextDouble() * (hi - lo);

    /// <summary>
    /// AegisMouse-model per-shot pull: progression-table base (3px first shot →
    /// 10px deep in the spray, like the Lua recoil_coords tables) × the strength
    /// MULTIPLIER × 70-140% volatility, eased in two sub-moves. One call = one shot.
    /// </summary>
    void PerShotPull()
    {
        double baseY = 3.0 + Math.Min(7.0, _shotIndex * 0.35); // 3 → 10 px
        double totalY = baseY * _settings.RecoilStrength * Vol(0.7, 1.4);
        double totalX = _settings.RecoilHorizontal * _settings.RecoilStrength * Vol(0.7, 1.4)
                        + (_rng.NextDouble() * 2 - 1) * Math.Max(0.2, Math.Abs(_settings.RecoilHorizontal) * 0.3);
        _shotIndex++;

        if (Math.Abs(totalY) < 0.5 && Math.Abs(totalX) < 0.5) return;
        // Two sub-steps (60% / 40%) — smooth ramp, no visible jerk.
        int y1 = (int)Math.Round(totalY * 0.6), y2 = (int)Math.Round(totalY * 0.4);
        int x1 = (int)Math.Round(totalX * 0.6), x2 = (int)Math.Round(totalX * 0.4);
        Native.Move(x1, y1);
        Native.Move(x2, y2);
    }

    static void SleepTick() => Thread.Sleep(TickMs);

    public void Dispose()
    {
        Stop();
        _grab.Dispose();
    }
}
