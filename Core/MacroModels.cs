using System.Text.Json.Serialization;

namespace AutoAimLock.Core;

/// <summary>Physical mouse buttons (gaming convention: Side1 = back, Side2 = forward).</summary>
public enum MouseButton
{
    Left = 0, Right = 1, Middle = 2, Side1 = 3, Side2 = 4,
    WheelUp = 5, WheelDown = 6, WheelTiltLeft = 7, WheelTiltRight = 8
}

/// <summary>What a binding does when its trigger button is pressed.</summary>
public enum BindingMode
{
    Default = 0, HoldMacro = 1, RapidFire = 2, BurstFire = 3,
    RecoilComp = 4, KeyRemap = 5, RapidFireRecoil = 6
}

public enum ActionKind { Click = 0, HoldDown = 1, ReleaseUp = 2, MoveRelative = 3, Delay = 4, KeyTap = 5, KeyDown = 6, KeyUp = 7 }

/// <summary>Single macro action. Timing in ms.</summary>
public sealed class MacroAction
{
    public ActionKind Kind { get; set; } = ActionKind.Click;
    public MouseButton Button { get; set; } = MouseButton.Left;
    public int Vk { get; set; }
    public int Dx { get; set; }
    public int Dy { get; set; }
    public int DelayMs { get; set; }
    public int HoldMs { get; set; } = 10;
    public int Repeat { get; set; } = 1;
    public int RepeatIntervalMs { get; set; }
}

/// <summary>One compensation step fired per shot (physical counts).</summary>
public sealed class RecoilStep
{
    public int Shot { get; set; }
    public int Dx { get; set; }   // negative = left
    public int Dy { get; set; }   // positive = pull down
    public int DelayMs { get; set; } = 8;
}

/// <summary>Jitter settings that make synthetic input look organic.</summary>
public sealed class HumanizeSettings
{
    public bool Enabled { get; set; } = true;
    public int DelayJitterMs { get; set; } = 2;
    public int HoldJitterMs { get; set; } = 1;
    public int MoveJitterCounts { get; set; } = 1;
    public int StepSkipPercent { get; set; } = 2;
}

/// <summary>One binding: trigger button -> behavior.</summary>
public sealed class ButtonBinding
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public MouseButton Trigger { get; set; } = MouseButton.Side1;
    public BindingMode Mode { get; set; } = BindingMode.Default;
    public int FireRateRpm { get; set; } = 600;
    public int BurstCount { get; set; } = 3;
    public MouseButton? ArmButton { get; set; }
    public int RemapVk { get; set; }
    public List<MacroAction> Actions { get; set; } = [];
}

/// <summary>A named, per-game profile.</summary>
public sealed class GameProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New Profile";
    public bool Enabled { get; set; } = true;
    public List<ButtonBinding> Bindings { get; set; } = [];
    public List<RecoilStep> RecoilTable { get; set; } = [];
    public int Dpi { get; set; } = 800;
    public double RecoilStrength { get; set; } = 1.0;
    public HumanizeSettings Humanize { get; set; } = new();
}

/// <summary>Application-wide settings.</summary>
public sealed class AppSettings
{
    public bool MinimizeToTray { get; set; } = true;
    public bool UseHighResTimer { get; set; } = true;
    public string LastProfileId { get; set; } = "";
}

public static class Presets
{
    /// <summary>Common FPS recoil archetypes (per-shot pull-down in counts at 800 DPI mid sens).</summary>
    public static IReadOnlyDictionary<string, List<RecoilStep>> Weapons { get; } = new Dictionary<string, List<RecoilStep>>
    {
        ["SMG / MP"] = Table([(3, 0),(3, 0),(3, 0),(4, 0),(4, 0),(4, 0),(4, 1),(5, 1),(5, 1),(5, 0),(5, -1),(5, -1)]),
        ["AR (M4-style)"] = Table([(4, 0),(5, 0),(5, 1),(6, 1),(6, 2),(6, 2),(7, 2),(7, 1),(7, 0),(7, -1),(8, -1),(8, -1),(8, 0),(8, 1),(8, 1)]),
        ["AK-style"] = Table([(6, 0),(7, 0),(8, 1),(8, 2),(9, 2),(9, 3),(9, 3),(10, 3),(10, 2),(10, 1),(10, 0),(11, 0),(11, -1),(11, -1)]),
        ["LMG"] = Table([(4, 0),(5, 0),(5, 0),(6, 0),(6, 0),(7, 0),(7, 0),(8, 0),(8, 0),(8, 0),(9, 0),(9, 0)]),
        ["Pistol (spam)"] = Table([(5, 0),(5, 1),(5, -1),(5, 1),(5, -1)])
    };

    static List<RecoilStep> Table((int dy, int dx)[] rows)
    {
        var list = new List<RecoilStep>(rows.Length);
        for (int i = 0; i < rows.Length; i++)
            list.Add(new RecoilStep { Shot = i + 1, Dy = rows[i].dy, Dx = rows[i].dx });
        return list;
    }
}
