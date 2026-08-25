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

    // Recoil accumulators (fractional)
    double _accX, _accY;

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

                // ── No-recoil: continuous counter-pull while firing ──
                if (_settings.RecoilOn && held)
                {
                    // Humanization: occasionally skip a tick (~4%)
                    if (_rng.NextDouble() >= 0.04)
                    {
                        _accY += _settings.RecoilStrength * (TickMs / 1000.0);
                        double spread = Math.Max(0.3, Math.Abs(_settings.RecoilHorizontal) * 0.4);
                        _accX += _settings.RecoilHorizontal * (TickMs / 1000.0)
                                 + (_rng.NextDouble() * 2 - 1) * spread * (TickMs / 1000.0);

                        int mx = (int)_accX, my = (int)_accY;
                        if (mx != 0 || my != 0)
                        {
                            _accX -= mx;
                            _accY -= my;
                            Native.Move(mx, my);
                        }
                    }
                }
                else
                {
                    _accX = _accY = 0;
                }

                // ── Rapid-fire click state machine ──
                if (_settings.RapidOn && held)
                {
                    int interval = Math.Clamp(60000 / Math.Max(1, _settings.RapidRpm), 20, 5000);
                    if (!_clickDown && now >= _nextClickAt)
                    {
                        Native.LeftButton(true);
                        _clickDown = true;
                        _clickUpAt = now + 10;
                    }
                    else if (_clickDown && now >= _clickUpAt)
                    {
                        Native.LeftButton(false);
                        _clickDown = false;
                        _nextClickAt = now + Math.Max(1, interval - 10);
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

    static void SleepTick() => Thread.Sleep(TickMs);

    public void Dispose()
    {
        Stop();
        _grab.Dispose();
    }
}
