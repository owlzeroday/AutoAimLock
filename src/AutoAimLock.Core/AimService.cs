using AUTOAIMLOCK.Core.Aim;
using AUTOAIMLOCK.Core.Detection;
using AUTOAIMLOCK.Core.Models;
using AUTOAIMLOCK.Core.Screen;
using AUTOAIMLOCK.Engine;

namespace AUTOAIMLOCK.Core;

/// <summary>
/// Main orchestration service. Runs the aim-assist loop:
/// capture screen → detect targets → compute movement → move mouse.
/// Designed to run on a high-frequency timer thread.
///
/// Tracking model:
///  * Detection and target tracking run EVERY frame, even while the trigger is
///    not held — so the lock is hot and acquisition is instant when you ADS.
///  * The target's velocity is tracked across frames and the aim point is
///    predicted ahead by <see cref="AimSettings.LeadMs"/> so the crosshair
///    follows MOVING bodies instead of always lagging behind.
///  * The last tracked position is fed back to the detector as a preference,
///    which keeps the lock "magnetic" to one body (no frame-to-frame switching
///    that caused wild rotation on walls/scenery).
///  * While firing, the hit-marker flash (red blood) and the body silhouette are
///    both detected; the body keeps the anchor, the flash confirms the hit point.
/// </summary>
public sealed class AimService : IDisposable
{
    readonly ScreenCapture _capture;
    readonly TargetDetector _detector;
    readonly AimController _controller;
    readonly WindMouse _windMouse;
    readonly MouseEngine _mouse;
    readonly AimSettings _settings;
    readonly MacroEngine _macros;

    Thread? _loopThread;
    CancellationTokenSource? _cts;
    volatile bool _enabled;

    // ── Tracking state ────────────────────────────────────────────
    // Target persistence: if detection flickers, keep the lock briefly.
    DetectedTarget? _lastTarget;
    DateTime _lastTargetTime = DateTime.MinValue;

    // Velocity / lead prediction
    double _lastX, _lastY;
    double _lastPosTime = -1; // ms since epoch; -1 = no sample yet
    double _velX, _velY; // px per second

    // Smoothed centroid (EMA toward detected position — filters frame-to-frame
    // detection jitter so the aim point doesn't micro-shiver while locked).
    double _smoothX, _smoothY;
    bool _hasSmooth;

    // Current target bitmap coords (for stats / draw)
    double _trackX, _trackY;

    // Stats
    public int Fps { get; private set; }
    public int LastTargetDistance { get; private set; }
    public bool TargetLocked { get; private set; }

    int _frameCount;
    DateTime _lastFpsUpdate = DateTime.Now;

    public event EventHandler? StatsUpdated;

    public AimService(MouseEngine mouse, AimSettings settings)
    {
        _mouse = mouse;
        _settings = settings;
        _capture = new ScreenCapture();
        _detector = new TargetDetector();
        _controller = new AimController(mouse);
        _windMouse = new WindMouse(mouse);
        _macros = new MacroEngine(settings, mouse);
    }

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value) return;
            _enabled = value;
            if (value)
            {
                StartLoop();
            }
            else
            {
                StopLoop();
                _macros.SetActive(false);
                _controller.Reset();
                _windMouse.Reset();
                ResetTracking();
                TargetLocked = false;
            }
        }
    }

    void ResetTracking()
    {
        _lastTarget = null;
        _lastTargetTime = DateTime.MinValue;
        _velX = _velY = 0;
        _lastPosTime = -1;
    }

    void StartLoop()
    {
        _cts = new CancellationTokenSource();
        _loopThread = new Thread(() => Loop(_cts.Token))
        {
            IsBackground = true,
            Priority = ThreadPriority.AboveNormal,
            Name = "AUTOAIMLOCK-Loop"
        };
        _loopThread.Start();
    }

    void StopLoop()
    {
        _cts?.Cancel();
        _cts = null;
        _loopThread = null;
    }

    void Loop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                bool shouldAim = _settings.Trigger switch
                {
                    TriggerMode.AlwaysOn => true,
                    TriggerMode.Toggle => true, // F8 hotkey handles the on/off toggle
                    TriggerMode.RightMouseHold => _mouse.IsKeyDown(0x02),
                    TriggerMode.LeftMouseHold => _mouse.IsKeyDown(0x01),
                    TriggerMode.BothHold => _mouse.IsKeyDown(0x01) || _mouse.IsKeyDown(0x02),
                    _ => false
                };

                // Capture screen region (run every frame — tracking stays hot)
                var bmp = _capture.CaptureRegion(_settings.CaptureSize);
                if (bmp == null)
                {
                    Thread.Sleep(Math.Max(2, _settings.StepDelayMs));
                    continue;
                }

                var (cx, cy) = _capture.CrosshairInBitmap;

                // Detect with a preference point = last tracked position, so the lock
                // sticks to one moving body instead of re-picking nearest-to-crosshair.
                bool usePref = _lastTarget != null && (DateTime.Now - _lastTargetTime).TotalMilliseconds < 150;
                var target = _detector.Detect(bmp, _settings.TargetColor, _settings.BodyColor, _settings.DetectBody,
                    _settings.MinTargetPixels, _settings.FovRadius, cx, cy,
                    usePref, _lastX, _lastY, 80);

                // Target persistence — keep the lock briefly when detection flickers
                if (target != null)
                {
                    _lastTarget = target;
                    _lastTargetTime = DateTime.Now;
                }
                else if (_lastTarget != null && (DateTime.Now - _lastTargetTime).TotalMilliseconds < 150)
                {
                    target = _lastTarget;
                }

                // Track velocity (px/sec) for lead prediction
                double nowMs = (System.DateTime.UtcNow - new System.DateTime(1970, 1, 1)).TotalMilliseconds;
                if (target != null)
                {
                    if (_lastPosTime > 0)
                    {
                        double dt = nowMs - _lastPosTime;
                        if (dt > 1 && dt < 500)
                        {
                            _velX = target.CenterX - _lastX;
                            _velY = target.CenterY - _lastY;
                            _velX = _velX * 1000.0 / dt;
                            _velY = _velY * 1000.0 / dt;
                            // Clamp lead velocity to something sane (rejects jitter spikes)
                            double vm = Math.Sqrt(_velX * _velX + _velY * _velY);
                            if (vm > 3000) { _velX = _velX * 3000 / vm; _velY = _velY * 3000 / vm; }
                        }
                    }
                    _lastX = target.CenterX;
                    _lastY = target.CenterY;
                    _lastPosTime = nowMs;
                    _trackX = target.CenterX;
                    _trackY = target.CenterY;

                    // Centroid EMA — damps frame-to-frame detection jitter so the
                    // aim point doesn't micro-shiver while locked. AimStability
                    // higher = smoother (but laggier) target centroid.
                    if (_settings.AimStability > 0)
                    {
                        if (!_hasSmooth)
                        {
                            _smoothX = target.CenterX;
                            _smoothY = target.CenterY;
                            _hasSmooth = true;
                        }
                        else
                        {
                            double a = Math.Clamp(_settings.AimStability, 0.0, 0.9);
                            _smoothX += (target.CenterX - _smoothX) * a;
                            _smoothY += (target.CenterY - _smoothY) * a;
                        }
                        target.CenterX = _smoothX;
                        target.CenterY = _smoothY;
                    }
                    else
                    {
                        _hasSmooth = false;
                    }
                }
                else
                {
                    _velX = _velY = 0;
                    _lastPosTime = -1;
                    _hasSmooth = false;
                }

                // ── Movement gating ───────────────────────────────────────────
                // Move when the trigger is held, OR when Auto-Lock is on and a valid
                // target is inside the FOV (engage automatically, no button needed).
                bool moveNow = shouldAim || (_settings.AutoLock && target != null);
                if (!moveNow)
                {
                    // Not aiming: release lock state + macros, keep tracking hot at a
                    // reduced cadence (fast enough to keep velocity/lead current).
                    if (TargetLocked)
                    {
                        _windMouse.Reset();
                        _controller.Reset();
                        TargetLocked = false;
                        LastTargetDistance = 0;
                    }
                    _macros.SetActive(false);
                    Thread.Sleep(14);
                    continue;
                }

                // Macros (no-recoil / rapid-fire) engage with the assist; they only
                // act when the fire button is actually held.
                _macros.SetActive(_settings.EnableNoRecoil || _settings.EnableRapidFire);

                if (target != null)
                {
                    TargetLocked = true;

                    // Offset from actual crosshair position within bitmap
                    double offsetX = target.CenterX - cx;
                    double offsetY = target.CenterY - cy;
                    LastTargetDistance = (int)Math.Sqrt(offsetX * offsetX + offsetY * offsetY);

                    // Deadzone is evaluated on the RAW error, BEFORE predictive lead.
                    // If the crosshair is already on the target, stop — the lead must
                    // not push the crosshair off and cause oscillation/shaking.
                    int deadzone = Math.Max(0, _settings.DeadzonePx);
                    if (Math.Abs(offsetX) <= deadzone && Math.Abs(offsetY) <= deadzone)
                    {
                        Thread.Sleep(Math.Max(2, _settings.StepDelayMs));
                        continue;
                    }

                    // Predictive lead: aim ahead of the moving target by LeadMs.
                    // Only applied while the target is meaningfully off-center, and
                    // clamped so it can't override the deadzone or dominate the aim.
                    if (_settings.LeadMs > 0 && _lastPosTime > 0)
                    {
                        double leadX = _velX * (_settings.LeadMs / 1000.0);
                        double leadY = _velY * (_settings.LeadMs / 1000.0);
                        // Cap the lead so it never exceeds a fraction of the current error
                        // (prevents lead-induced overshoot that shakes the aim).
                        double err = Math.Sqrt(offsetX * offsetX + offsetY * offsetY);
                        double leadMag = Math.Sqrt(leadX * leadX + leadY * leadY);
                        if (leadMag > err * 0.5 && err > 1)
                        {
                            double s = (err * 0.5) / leadMag;
                            leadX *= s;
                            leadY *= s;
                        }
                        offsetX += leadX;
                        offsetY += leadY;
                    }

                    // Choose movement algorithm
                    if (_settings.Algorithm == MovementAlgorithm.WindMouse)
                        _windMouse.Process(offsetX, offsetY, _settings);
                    else
                        _controller.ProcessTarget(target, _settings, cx, cy);
                }
                else
                {
                    if (TargetLocked) { _windMouse.Reset(); _controller.Reset(); }
                    TargetLocked = false;
                    LastTargetDistance = 0;
                }

                // FPS tracking
                _frameCount++;
                var now = DateTime.Now;
                if ((now - _lastFpsUpdate).TotalSeconds >= 1)
                {
                    Fps = _frameCount;
                    _frameCount = 0;
                    _lastFpsUpdate = now;
                    StatsUpdated?.Invoke(this, EventArgs.Empty);
                }

                // Step delay — floor of 2ms keeps the loop from spinning a full core
                Thread.Sleep(Math.Max(2, _settings.StepDelayMs));
            }
            catch (Exception)
            {
                // Swallow — the loop must not crash
                Thread.Sleep(10);
            }
        }
    }

    public void Dispose()
    {
        Enabled = false;
        _capture.Dispose();
        _macros.Dispose();
    }
}