using AUTOAIMLOCK.Core.Models;
using AUTOAIMLOCK.Engine;

namespace AUTOAIMLOCK.Core.Aim;

/// <summary>
/// WindMouse movement algorithm — physics-based model treating the cursor
/// as a particle affected by gravity (toward target), wind (random ambient
/// variance), and friction/drag. Produces the most human-like trajectories.
///
/// Based on BenLand100's canonical WindMouse implementation, adapted for
/// continuous aim tracking (not one-shot point-to-point movement).
///
/// Anti-shake: near the target the wind is damped toward zero and velocity is
/// exponentially braked, so the cursor settles instead of swinging/oscillating
/// around the target (the "violent shake on lock" symptom).
/// </summary>
public sealed class WindMouse
{
    readonly MouseEngine _mouse;
    readonly Random _rng = new();

    // Wind state — random walk that persists between frames
    double _windX, _windY;
    // Velocity state
    double _velX, _velY;

    // Tunable physics constants
    const double Gravity = 9.0;      // Pull toward target
    const double WindBase = 3.0;     // Ambient wind magnitude
    const double MaxVelocity = 12.0; // Velocity cap (px/frame)
    const double DragDistance = 10.0; // Below this distance, start braking
    const double SettleDistance = 4.0; // Below this, lock-on and stop (deadzone handled by caller)

    public WindMouse(MouseEngine mouse) => _mouse = mouse;

    /// <summary>
    /// Process one frame of WindMouse movement toward the target offset.
    /// Call this each frame while the target is locked.
    /// </summary>
    public (int dx, int dy) Process(double targetX, double targetY, AimSettings settings)
    {
        double dist = Math.Sqrt(targetX * targetX + targetY * targetY);
        if (dist < 0.5) return (0, 0);

        // Smoothing factor: higher smoothing = slower/gentler, lower = snappier
        double smooth = 1.0 - (settings.Smoothing / 100.0) * 0.85;
        smooth = Math.Clamp(smooth, 0.05, 1.0);

        double gravity = Gravity * smooth;
        double maxSpeed = MaxVelocity * smooth;

        if (dist >= SettleDistance)
        {
            // ── Approach phase ─────────────────────────────────────────
            // Random-walk wind only applies while far enough away.
            double windMag = WindBase * (settings.Humanize ? 1.0 : 0.3);
            if (dist >= DragDistance)
            {
                _windX = _windX / Math.Sqrt(3) + (_rng.NextDouble() * 2 - 1) * windMag / Math.Sqrt(5);
                _windY = _windY / Math.Sqrt(3) + (_rng.NextDouble() * 2 - 1) * windMag / Math.Sqrt(5);
            }
            else
            {
                // In the braking zone: decay wind quickly
                _windX /= Math.Sqrt(3);
                _windY /= Math.Sqrt(3);
            }

            // Gravity pulls toward target
            double gx = gravity * targetX / dist;
            double gy = gravity * targetY / dist;

            // Proportional correction toward the target: this term is what
            // actually converges the aim, and it is stable (no overshoot).
            double propX = targetX * 0.45;
            double propY = targetY * 0.45;

            _velX += _windX + gx * 0.5 + propX;
            _velY += _windY + gy * 0.5 + propY;

            // Clip velocity to max speed
            double velMag = Math.Sqrt(_velX * _velX + _velY * _velY);
            if (velMag > maxSpeed)
            {
                double clip = maxSpeed * 0.5 + _rng.NextDouble() * maxSpeed * 0.5;
                _velX = (_velX / velMag) * clip;
                _velY = (_velY / velMag) * clip;
            }

            // Human jitter — distance-scaled (no shaking when nearly on target)
            if (settings.Humanize && settings.JitterPx > 0 && dist > Math.Max(4, settings.DeadzonePx))
            {
                double scale = Math.Min(1.0, dist / 30.0);
                _velX += _rng.Next(-settings.JitterPx, settings.JitterPx + 1) * scale;
                _velY += _rng.Next(-settings.JitterPx, settings.JitterPx + 1) * scale;
            }
        }
        else
        {
            // ── Settle zone: on/near the target → brake hard, no wind, no jitter ──
            _velX *= 0.35;
            _velY *= 0.35;
            _windX = 0;
            _windY = 0;
        }

        int moveX = (int)Math.Round(_velX);
        int moveY = (int)Math.Round(_velY);
        if (moveX != 0 || moveY != 0)
            _mouse.MoveRelative(moveX, moveY);

        return (moveX, moveY);
    }

    /// <summary>Reset wind/velocity state.</summary>
    public void Reset()
    {
        _windX = 0;
        _windY = 0;
        _velX = 0;
        _velY = 0;
    }
}