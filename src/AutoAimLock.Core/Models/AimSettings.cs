namespace AUTOAIMLOCK.Core.Models;

/// <summary>All configurable settings for the aim assist engine.</summary>
public sealed class AimSettings
{
    /// <summary>FOV radius in pixels — only targets within this distance from crosshair are tracked.</summary>
    public int FovRadius { get; set; } = 160;

    /// <summary>Aim smoothing factor. 0 = instant, 100 = very slow/gentle.</summary>
    public int Smoothing { get; set; } = 35;

    /// <summary>Screen capture region size (square, centered on crosshair).</summary>
    public int CaptureSize { get; set; } = 400;

    /// <summary>Target color (HSV ranges) to detect enemies (primary band — e.g. hit marker).</summary>
    public ColorSettings TargetColor { get; set; } = new();

    /// <summary>
    /// Secondary detection band — the enemy character model itself (e.g. blue-gray
    /// bot uniforms). Detecting the body lets the assist lock onto targets at mid/long
    /// range where the hit-marker flash is too small to satisfy MinTargetPixels.
    /// Null = disabled.
    /// </summary>
    public ColorSettings? BodyColor { get; set; } = new()
    {
        HMin = 95, HMax = 120, // blue-gray bot uniform (calibrated H104). Broader than the
        SMin = 40, SMax = 95,  // raw color because shape-validation rejects walls/scenery —
        VMin = 180, VMax = 245 // a wider band plus humanoid-shape checks beats a tight band.
    };

    /// <summary>Whether the secondary body-detection band is enabled (dual-band tracking).</summary>
    public bool DetectBody { get; set; } = true;

    /// <summary>Minimum pixel count for a valid target (filters noise).</summary>
    public int MinTargetPixels { get; set; } = 12;

    /// <summary>Aim activation trigger: hold this mouse button to activate aim assist.</summary>
    public TriggerMode Trigger { get; set; } = TriggerMode.BothHold;

    /// <summary>
    /// Auto-lock: engage the assist automatically whenever a valid target is inside
    /// the FOV — no mouse button needed. Trigger-based activation still works and
    /// overrides this when held.
    /// </summary>
    public bool AutoLock { get; set; } = false;

    /// <summary>Humanization: add jitter to movements to appear more natural.</summary>
    public bool Humanize { get; set; } = true;

    /// <summary>Jitter intensity in pixels (±).</summary>
    public int JitterPx { get; set; } = 2;

    /// <summary>Movement step delay in milliseconds.</summary>
    public int StepDelayMs { get; set; } = 1;

    /// <summary>Hard cap on mouse movement per frame (px per axis). Prevents wild flings/overshoot.</summary>
    public int MaxMovePx { get; set; } = 14;

    /// <summary>Distance (px) from target at which the assist stops moving — stops hunting/oscillating around the target.</summary>
    public int DeadzonePx { get; set; } = 5;

    /// <summary>Predictive lead (ms) — aims ahead of the target's velocity so the crosshair
    /// follows moving bodies instead of lagging behind them. 0 = aim at current position.</summary>
    public int LeadMs { get; set; } = 60;

    /// <summary>Hotkey to toggle the assist on/off.</summary>
    public int ToggleHotkey { get; set; } = 0x77; // F8

    /// <summary>Movement algorithm: PID (snappy) or WindMouse (physics-based, most natural).</summary>
    public MovementAlgorithm Algorithm { get; set; } = MovementAlgorithm.WindMouse;

    // ── Macro settings (no-recoil / rapid-fire) ──────────────────────────
    /// <summary>Enable rapid-fire: while left/right fire button is held, repeatedly click at <see cref="RapidFireRpm"/>.</summary>
    public bool EnableRapidFire { get; set; } = false;

    /// <summary>Rapid-fire speed in rounds per minute.</summary>
    public int RapidFireRpm { get; set; } = 600;

    /// <summary>Enable no-recoil: pull the mouse down by <see cref="RecoilVertical"/> px per shot to counter upward kick.</summary>
    public bool EnableNoRecoil { get; set; } = false;

    /// <summary>Vertical recoil compensation (px per shot, downward is positive).</summary>
    public int RecoilVertical { get; set; } = 2;

    /// <summary>Horizontal recoil compensation (px per shot, right is positive).</summary>
    public int RecoilHorizontal { get; set; } = 0;

    // ── Aim stability ────────────────────────────────────────────────────
    /// <summary>Centroid EMA smoothing (0-1). Higher = smoother but laggier target centroid. Reduces frame-to-frame jitter of the detected point.</summary>
    public double AimStability { get; set; } = 0.5;
}

public enum TriggerMode
{
    RightMouseHold,
    LeftMouseHold,
    BothHold,       // Either left OR right mouse button held activates aim
    AlwaysOn,
    Toggle
}

public enum MovementAlgorithm
{
    PID,
    WindMouse
}

public sealed class ColorSettings
{
    // HSV color range for target detection
    public int HMin { get; set; } = 0;    // 0-180 (OpenCV scale)
    public int HMax { get; set; } = 8;    // Bright red hit-flash range (Knives Out Hit Marker)
    public int SMin { get; set; } = 200;
    public int SMax { get; set; } = 255;
    public int VMin { get; set; } = 200;
    public int VMax { get; set; } = 255;

    /// <summary>Preset color profiles for common games.</summary>
    public static readonly Dictionary<string, ColorSettings> Presets = new()
    {
        // Generic presets
        ["Red (Enemy)"] = new() { HMin = 0, HMax = 10, SMin = 100, SMax = 255, VMin = 100, VMax = 255 },
        ["Red (Wide)"] = new() { HMin = 0, HMax = 20, SMin = 80, SMax = 255, VMin = 80, VMax = 255 },
        ["Orange"] = new() { HMin = 10, HMax = 30, SMin = 100, SMax = 255, VMin = 100, VMax = 255 },
        ["Magenta"] = new() { HMin = 140, HMax = 170, SMin = 100, SMax = 255, VMin = 100, VMax = 255 },
        ["Cyan"] = new() { HMin = 85, HMax = 105, SMin = 100, SMax = 255, VMin = 100, VMax = 255 },

        // Knives Out (NetEase) — TPS battle royale, no built-in enemy outlines
        // Hit marker: very bright saturated red flash (R>200, high S+V to reject health bar red which is duller)
        ["Knives Out — Hit Marker"] = new() { HMin = 0, HMax = 8, SMin = 200, SMax = 255, VMin = 200, VMax = 255 },
        // Hit marker (wider): catches slightly less saturated hit flashes for darker game environments
        ["Knives Out — Hit Marker (Wide)"] = new() { HMin = 0, HMax = 10, SMin = 170, SMax = 255, VMin = 170, VMax = 255 },
        // Nametag: red text above enemies in squad mode — medium saturation, medium-high value
        ["Knives Out — Nametag"] = new() { HMin = 0, HMax = 12, SMin = 160, SMax = 230, VMin = 140, VMax = 255 },
        // Blood hit effect: orange-red splash on hit — broader hue, lower saturation
        ["Knives Out — Blood"] = new() { HMin = 5, HMax = 18, SMin = 100, SMax = 220, VMin = 90, VMax = 220 },
        // Damage numbers: bright red-orange numbers that float up when you hit an enemy
        ["Knives Out — Damage Numbers"] = new() { HMin = 2, HMax = 12, SMin = 180, SMax = 255, VMin = 190, VMax = 255 },

        // PUBG-style — similar to Knives Out, no outlines, detect hit markers
        ["PUBG — Hit Marker"] = new() { HMin = 0, HMax = 10, SMin = 160, SMax = 255, VMin = 160, VMax = 255 },

        // Free Fire — mobile/PC, red hit markers
        ["Free Fire — Hit Marker"] = new() { HMin = 0, HMax = 10, SMin = 140, SMax = 255, VMin = 140, VMax = 255 },

        // White/skin tone — for detecting player models in games without outlines
        ["Skin/White Tone"] = new() { HMin = 0, HMax = 25, SMin = 20, SMax = 120, VMin = 150, VMax = 255 },
    };
}

/// <summary>Per-game configuration profile.</summary>
public sealed class GameProfile
{
    public string Name { get; set; } = "";
    public string Game { get; set; } = "";
    public AimSettings Settings { get; set; } = new();
    public string Notes { get; set; } = "";
}

/// <summary>
/// Full game presets — applies optimal settings for a specific game in one shot.
/// Includes color, FOV, smoothing, algorithm, capture size, and min pixels.
/// </summary>
public static class GamePresets
{
    public static readonly Dictionary<string, GameConfig> All = new()
    {
        ["Knives Out — Hit Assist"] = new()
        {
            Game = "Knives Out",
            DetectionMode = "Hit Marker + Body (dual-band lock)",
            Color = new() { HMin = 0, HMax = 8, SMin = 200, SMax = 255, VMin = 200, VMax = 255 },
            Settings = new AimSettings
            {
                FovRadius = 160,       // mid-range acquisition room
                Smoothing = 35,        // natural feel, still tracks moving targets
                CaptureSize = 400,     // big enough for mid-range silhouettes, fast enough
                MinTargetPixels = 12,  // hit markers are tiny (10-25px); bodies are much bigger
                BodyColor = new() { HMin = 95, HMax = 120, SMin = 40, SMax = 95, VMin = 180, VMax = 245 },
                DetectBody = true,
                Algorithm = MovementAlgorithm.WindMouse,
                Trigger = TriggerMode.BothHold,
                Humanize = true,
                JitterPx = 2,
                StepDelayMs = 1,
                MaxMovePx = 14,
                DeadzonePx = 5,
                LeadMs = 60,
            },
            Notes = "Dual-band lock — detects hit-marker flash (red) while firing plus the enemy body (blue-gray uniform). Velocity lead rides moving targets. Shape validation rejects walls/scenery. Best for TPS tracking at close-to-mid range.",
        },
        ["Knives Out — Squad Nametag"] = new()
        {
            Game = "Knives Out",
            DetectionMode = "Nametag (squad mode)",
            Color = new() { HMin = 0, HMax = 12, SMin = 160, SMax = 230, VMin = 140, VMax = 255 },
            Settings = new AimSettings
            {
                FovRadius = 250,       // Wider — nametags can be above enemies at various distances
                Smoothing = 60,
                CaptureSize = 500,
                MinTargetPixels = 8,   // Nametags are small text clusters
                Algorithm = MovementAlgorithm.WindMouse,
                Trigger = TriggerMode.RightMouseHold,
                Humanize = true,
                JitterPx = 2,
                StepDelayMs = 1,
            },
            Notes = "Detects red nametags above enemies in squad/duo mode. Wider FOV for initial acquisition. Only works when enemy nametags are visible.",
        },
        ["Knives Out — Damage Track"] = new()
        {
            Game = "Knives Out",
            DetectionMode = "Damage numbers (active combat)",
            Color = new() { HMin = 2, HMax = 12, SMin = 180, SMax = 255, VMin = 190, VMax = 255 },
            Settings = new AimSettings
            {
                FovRadius = 120,
                Smoothing = 50,
                CaptureSize = 400,
                MinTargetPixels = 10,
                Algorithm = MovementAlgorithm.WindMouse,
                Trigger = TriggerMode.RightMouseHold,
                Humanize = true,
                JitterPx = 3,
                StepDelayMs = 1,
            },
            Notes = "Tracks floating damage numbers. Good mid-combat when you're already dealing damage — numbers appear near the enemy's body.",
        },
        ["Valorant — Flick"] = new()
        {
            Game = "Valorant",
            DetectionMode = "Enemy outline (red)",
            Color = new() { HMin = 0, HMax = 10, SMin = 100, SMax = 255, VMin = 100, VMax = 255 },
            Settings = new AimSettings
            {
                FovRadius = 120,
                Smoothing = 30,
                CaptureSize = 350,
                MinTargetPixels = 40,   // Outlines are large clusters
                Algorithm = MovementAlgorithm.PID,
                Trigger = TriggerMode.RightMouseHold,
                Humanize = true,
                JitterPx = 2,
                StepDelayMs = 1,
            },
            Notes = "Valorant has red enemy outlines — easy detection. PID for fast flick shots. Lower smoothing for snappy response.",
        },
        ["Apex Legends — Track"] = new()
        {
            Game = "Apex Legends",
            DetectionMode = "Enemy outline (red, wide)",
            Color = new() { HMin = 0, HMax = 20, SMin = 80, SMax = 255, VMin = 80, VMax = 255 },
            Settings = new AimSettings
            {
                FovRadius = 150,
                Smoothing = 40,
                CaptureSize = 400,
                MinTargetPixels = 35,
                Algorithm = MovementAlgorithm.PID,
                Trigger = TriggerMode.RightMouseHold,
                Humanize = true,
                JitterPx = 2,
                StepDelayMs = 1,
            },
            Notes = "Apex has red enemy outlines. Wider hue range for different outline intensities. PID with moderate smoothing.",
        },
        ["PUBG — Hit Assist"] = new()
        {
            Game = "PUBG",
            DetectionMode = "Hit marker",
            Color = new() { HMin = 0, HMax = 10, SMin = 160, SMax = 255, VMin = 160, VMax = 255 },
            Settings = new AimSettings
            {
                FovRadius = 80,
                Smoothing = 55,
                CaptureSize = 350,
                MinTargetPixels = 12,
                Algorithm = MovementAlgorithm.WindMouse,
                Trigger = TriggerMode.RightMouseHold,
                Humanize = true,
                JitterPx = 2,
                StepDelayMs = 1,
            },
            Notes = "Same approach as Knives Out — no outlines, detect hit markers. Tight FOV for aim correction.",
        },
        ["Free Fire — Hit Assist"] = new()
        {
            Game = "Free Fire",
            DetectionMode = "Hit marker",
            Color = new() { HMin = 0, HMax = 10, SMin = 140, SMax = 255, VMin = 140, VMax = 255 },
            Settings = new AimSettings
            {
                FovRadius = 100,
                Smoothing = 45,
                CaptureSize = 400,
                MinTargetPixels = 15,
                Algorithm = MovementAlgorithm.WindMouse,
                Trigger = TriggerMode.RightMouseHold,
                Humanize = true,
                JitterPx = 2,
                StepDelayMs = 1,
            },
            Notes = "Free Fire has red hit markers. Moderate FOV, WindMouse for natural tracking.",
        },
    };
}

/// <summary>A complete game configuration — color + settings + metadata.</summary>
public sealed class GameConfig
{
    public string Game { get; set; } = "";
    public string DetectionMode { get; set; } = "";
    public ColorSettings Color { get; set; } = new();
    public AimSettings Settings { get; set; } = new();
    public string Notes { get; set; } = "";
}

public sealed class DetectedTarget
{
    /// <summary>X position of the target's center of mass (bitmap coords).</summary>
    public double CenterX { get; set; }
    /// <summary>Y position of the target's center of mass (bitmap coords).</summary>
    public double CenterY { get; set; }
    /// <summary>Total matched pixels in the cluster (hit-marker + body).</summary>
    public int PixelCount { get; set; }
    /// <summary>Distance from the crosshair center (px).</summary>
    public double DistanceFromCenter { get; set; }

    /// <summary>Whether the cluster contains body-detection pixels (a full humanoid silhouette, not just a hit flash).</summary>
    public bool IsBody { get; set; }
    /// <summary>Body-band matched pixels in this cluster.</summary>
    public int BodyPixelCount { get; set; }
    /// <summary>Hit-marker matched pixels in this cluster.</summary>
    public int HitPixelCount { get; set; }
}
