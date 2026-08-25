using AUTOAIMLOCK.Core.Models;
using AUTOAIMLOCK.Engine;

namespace AUTOAIMLOCK.Core.Aim;

/// <summary>
/// Aim controller that processes detected targets and computes smooth
/// mouse movements toward them.
///
/// Movement model: PURE PROPORTIONAL control (error × gain), as used by
/// production aimbots (cf. taffe.dev "Aimbot Smoothing Using a Proportional
/// Control"). There is NO velocity accumulator / low-pass filter — those
/// ring and oscillate around the setpoint when the error flips sign (the
/// symptom is violent shaking when the crosshair is near the target).
/// A pure proportional step of error*k (k&lt;1) converges geometrically and
/// never overthrows, because it responds only to the CURRENT error.
/// </summary>
public sealed class AimController
{
    readonly MouseEngine _mouse;
    readonly Random _rng = new();

    public AimController(MouseEngine mouse) => _mouse = mouse;

    /// <summary>
    /// Process a detected target and move the mouse toward it using
    /// proportional control. Returns the offset that was applied (dx, dy).
    /// </summary>
    public (int dx, int dy) ProcessTarget(DetectedTarget target, AimSettings settings, int crosshairX, int crosshairY)
    {
        // Offset from actual crosshair position to target center
        double offsetX = target.CenterX - crosshairX;
        double offsetY = target.CenterY - crosshairY;
        double dist = Math.Sqrt(offsetX * offsetX + offsetY * offsetY);
        if (dist < 0.5) return (0, 0);

        // Proportional gain: fraction of remaining error to move per frame.
        // Smoothing 0 → snappy, 100 → very slow/crawl.
        // Clamped well below 1 so the correction can never over level.
        double k = 1.0 - (settings.Smoothing / 100.0);
        k = Math.Clamp(k, 0.03, 0.85);

        double moveX = offsetX * k;
        double moveY = offsetY * k;

        // Hard clamp per-frame movement (stability)
        int maxMove = Math.Max(1, settings.MaxMovePx);
        moveX = Math.Clamp(moveX, -maxMove, maxMove);
        moveY = Math.Clamp(moveY, -maxMove, maxMove);

        // Human jitter is distance-ranged: only when clearly off target, and scaled
        // by error so it can never push a lock into shaking.
        if (settings.Humanize && settings.JitterPx > 0 && dist > Math.Max(3, settings.DeadzonePx))
        {
            double scale = Math.Min(1.0, dist / 25.0);
            moveX += _rng.Next(-settings.JitterPx, settings.JitterPx + 1) * scale;
            moveY += _rng.Next(-settings.JitterPx, settings.JitterPx + 1) * scale;
        }

        int mx = (int)Math.Round(moveX);
        int my = (int)Math.Round(moveY);
        if (mx != 0 || my != 0) _mouse.MoveRelative(mx, my);
        return (mx, my);
    }

    /// <summary>Reset any state (no-op for the stateless proportional model).</summary>
    public void Reset() { }

    /// <summary>
    /// Generate a Bezier curve path from (0,0) to (targetX, targetY).
    /// Returns a list of intermediate points for smooth multi-step movement.
    /// </summary>
    public static List<(double x, double y)> BezierPath(double targetX, double targetY, int steps)
    {
        var path = new List<(double x, double y)>(steps);
        if (steps <= 0) return path;

        // Two control points with slight offset for natural curve
        var rng = new Random();
        double cx1 = targetX * 0.3 + (rng.NextDouble() - 0.5) * Math.Abs(targetX) * 0.2;
        double cy1 = targetY * 0.3 + (rng.NextDouble() - 0.5) * Math.Abs(targetY) * 0.2;
        double cx2 = targetX * 0.7 + (rng.NextDouble() - 0.5) * Math.Abs(targetX) * 0.2;
        double cy2 = targetY * 0.7 + (rng.NextDouble() - 0.5) * Math.Abs(targetY) * 0.2;

        for (int i = 1; i <= steps; i++)
        {
            double t = (double)i / steps;
            double mt = 1 - t;

            // Cubic Bezier: B(t) = (1-t)^3*P0 + 3(1-t)^2*t*C1 + 3(1-t)*t^2*C2 + t^3*P1
            double x = mt * mt * mt * 0
                     + 3 * mt * mt * t * cx1
                     + 3 * mt * t * t * cx2
                     + t * t * t * targetX;

            double y = mt * mt * mt * 0
                     + 3 * mt * mt * t * cy1
                     + 3 * mt * t * t * cy2
                     + t * t * t * targetY;

            path.Add((x, y));
        }

        return path;
    }
}