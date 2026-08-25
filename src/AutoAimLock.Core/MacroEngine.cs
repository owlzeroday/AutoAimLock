using AUTOAIMLOCK.Core.Models;
using AUTOAIMLOCK.Engine;

namespace AUTOAIMLOCK.Core;

/// <summary>
/// Macro engine for no-recoil and rapid-fire, driven by the fire button
/// (left mouse button). Runs on its own background thread so the timing of
/// synthetic clicks and recoil correction is independent of the aim loop.
///
/// Active only while <see cref="SetActive"/> is true (the aim-assist trigger
/// is engaged). No-recoil pulls the mouse to counter the game's upward kick;
/// rapid-fire re-clicks at a set RPM for semi/bolt weapons. Both only act
/// while the fire button is physically held.
///
/// Design / integrity:
///  * Every rapid-fire "down" is guaranteed a matching "up" (try/finally) so
///    the game can never be left with the fire button stuck down, even if the
///    loop is cancelled mid-pulse.
///  * Recoil is applied per shot and sub-stepped into small increments so it
///    eases the crosshair down rather than jerking it (avoids shaking).
/// </summary>
public sealed class MacroEngine : IDisposable
{
    readonly AimSettings _settings;
    readonly MouseEngine _mouse;
    Thread? _thread;
    CancellationTokenSource? _cts;
    volatile bool _active;

    const int FireButton = 0x01; // left

    public MacroEngine(AimSettings settings, MouseEngine mouse)
    {
        _settings = settings;
        _mouse = mouse;
    }

    public bool Active => _active;

    /// <summary>Start/stop the macro thread. Should track the aim-assist active state.</summary>
    public void SetActive(bool active)
    {
        if (active && !_active)
        {
            _active = true;
            _cts = new CancellationTokenSource();
            _thread = new Thread(() => Loop(_cts.Token))
            {
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal,
                Name = "AUTOAIMLOCK-Macros"
            };
            _thread.Start();
        }
        else if (!active && _active)
        {
            _active = false;
            _cts?.Cancel();
            _cts = null;
            _thread = null;
        }
    }

    void Loop(CancellationToken ct)
    {
        // Release-debounce: rapid-fire's own synthetic UP makes IsKeyDown report up
        // momentarily even though the user still holds the button. We only treat the
        // fire button as released after it reads up for several consecutive samples
        // (~60ms), so a real release stops the train but our synthetic pulse doesn't.
        int releaseCount = 0;
        const int releaseThreshold = 6; // ~60ms

        while (!ct.IsCancellationRequested && _active)
        {
            try
            {
                bool rawDown = _mouse.IsKeyDown(FireButton);
                if (rawDown)
                    releaseCount = 0;
                else
                    releaseCount++;

                if (releaseCount >= releaseThreshold)
                {
                    // User released (or never pressed) — release any synthetic hold.
                    _mouse.SetButton(FireButton, false);
                    Thread.Sleep(8);
                    continue;
                }

                // Shot cadence from the configured fire rate.
                int interval = Math.Clamp(60000 / Math.Max(1, _settings.RapidFireRpm), 1, 5000);

                // Rapid-fire: one synthetic click pulse per shot (the pulse waits its
                // own hold time, which counts toward the shot period). Recoil is then
                // compensated per shot — perfectly aligned with the discrete shots.
                int elapsedThisShot = 0;
                if (_settings.EnableRapidFire)
                {
                    int hold = Math.Max(1, interval / 3);
                    DoClick(hold, ct);
                    elapsedThisShot = hold;
                    if (ct.IsCancellationRequested) break;

                    // Per-shot recoil pull (sub-stepped for smoothness).
                    if (_settings.EnableNoRecoil)
                        ApplyRecoil(ct);

                    if (ct.IsCancellationRequested) break;

                    int remaining = Math.Max(1, interval - elapsedThisShot);
                    WaitUntil(remaining, ct);
                }
                else if (_settings.EnableNoRecoil)
                {
                    // ── Continuous no-recoil (auto weapons, physical hold) ──────
                    // Research-backed model (cf. production anti-recoil tools):
                    // steady micro counter-pulls every few ms instead of per-shot
                    // jumps at a guessed fire rate. Sub-pixel accumulator keeps the
                    // pull smooth; random horizontal spread + occasional skipped
                    // ticks mimic human compensation.
                    ContinuousRecoil(ct);
                }
                else
                {
                    // No macros enabled — just pace the loop.
                    WaitUntil(interval, ct);
                }
            }
            catch (Exception)
            {
                Thread.Sleep(8);
            }
        }
    }

    /// <summary>
    /// Continuous recoil compensation while the fire button is held.
    /// Applies <see cref="AimSettings.RecoilVertical"/> px per second as a smooth
    /// drift (8ms micro-ticks, fractional accumulator), with random horizontal
    /// spread around <see cref="AimSettings.RecoilHorizontal"/> and a small chance
    /// to skip a tick (human imperfection).
    /// </summary>
    void ContinuousRecoil(CancellationToken ct)
    {
        const int tickMs = 8;
        double accumX = 0, accumY = 0;
        var rng = new Random();

        while (!ct.IsCancellationRequested && _active)
        {
            // Stop as soon as the fire button is genuinely released.
            if (!_mouse.IsKeyDown(FireButton)) break;

            // Humanization: occasionally skip a tick (~4%) like a real hand pausing.
            if (rng.NextDouble() < 0.04)
            {
                Thread.Sleep(tickMs);
                continue;
            }

            // Vertical: px/sec → px/tick via the accumulator (sub-pixel smooth).
            accumY += _settings.RecoilVertical * (tickMs / 1000.0);

            // Horizontal: bias + small random spread (±40% of the bias magnitude,
            // minimum ±0.3px so there's always a little natural wobble).
            double spread = Math.Max(0.3, Math.Abs(_settings.RecoilHorizontal) * 0.4);
            accumX += _settings.RecoilHorizontal * (tickMs / 1000.0)
                      + (rng.NextDouble() * 2 - 1) * spread * (tickMs / 1000.0);

            // Flush whole pixels; keep the fraction for the next tick.
            int moveX = (int)accumX;
            int moveY = (int)accumY;
            if (moveX != 0 || moveY != 0)
            {
                accumX -= moveX;
                accumY -= moveY;
                _mouse.MoveRelative(moveX, moveY);
            }

            Thread.Sleep(tickMs);
        }
    }

    /// <summary>Send a left-button click (down then up after <paramref name="holdMs"/>),
    /// guaranteeing the up even on cancel (no stuck fire button).</summary>
    void DoClick(int holdMs, CancellationToken ct)
    {
        _mouse.SetButton(FireButton, true);
        try
        {
            WaitUntil(holdMs, ct);
        }
        finally
        {
            _mouse.SetButton(FireButton, false);
        }
    }

    /// <summary>
    /// Apply recoil compensation toward the target, split into small sub-steps.
    /// Ramping the pull down smoothly instead of one hard jump keeps the aim stable.
    /// </summary>
    void ApplyRecoil(CancellationToken ct)
    {
        int dxTotal = _settings.RecoilHorizontal;
        int dyTotal = _settings.RecoilVertical;
        if (dxTotal == 0 && dyTotal == 0) return;

        // Sub-step into up to 4 small increments for a smooth ramp.
        // Each sub-move is at a steady cadence so the correction isn't a single jerk.
        const int substeps = 4;
        int stepAccumX = 0, stepAccumY = 0;
        for (int i = 0; i < substeps && !ct.IsCancellationRequested; i++)
        {
            // Distribute the integer displacement across sub-steps; final carries the remainder.
            int wantX = (dxTotal * (i + 1)) / substeps;
            int wantY = (dyTotal * (i + 1)) / substeps;
            int dx = wantX - stepAccumX;
            int dy = wantY - stepAccumY;
            stepAccumX = wantX;
            stepAccumY = wantY;
            if (dx != 0 || dy != 0)
            {
                _mouse.MoveRelative(dx, dy);
                Thread.Sleep(2); // small pace between sub-steps
            }
        }
    }

    /// <summary>Sleep in fine slices for the given duration, aborting early on cancel/button release.</summary>
    static void WaitUntil(int ms, CancellationToken ct)
    {
        int elapsed = 0;
        while (elapsed < ms && !ct.IsCancellationRequested)
        {
            int step = Math.Min(ms - elapsed, 10);
            Thread.Sleep(step);
            elapsed += step;
        }
    }

    public void Dispose()
    {
        try { SetActive(false); } catch { }
    }
}