using System.IO;
using System.Text.Json;

namespace AutoAimLock.Engine;

/// <summary>Color match band (HSV, OpenCV hue scale 0-180, S/V 0-255).</summary>
public sealed class Band
{
    public int HMin { get; set; } = 95;
    public int HMax { get; set; } = 120;
    public int SMin { get; set; } = 40;
    public int SMax { get; set; } = 95;
    public int VMin { get; set; } = 180;
    public int VMax { get; set; } = 245;
}

/// <summary>All user settings + JSON persistence.</summary>
public sealed class Settings
{
    // Targeting
    public int Fov { get; set; } = 150;
    public int GainPct { get; set; } = 30;       // fraction of remaining error per tick (stability core)
    public int MaxStep { get; set; } = 5;        // hard px/frame cap
    public int Deadzone { get; set; } = 6;       // engage threshold
    public double Hysteresis { get; set; } = 1.6;// release at Deadzone*Hysteresis
    public int MinPixels { get; set; } = 40;     // characters only — walls/textures fail this
    public Band ColorBand { get; set; } = new();
    public bool InvertFovCheck { get; set; } = false;

    // Macro trigger (AegisMouse-style): which held input drives the fire macro.
    // 0x01 = left mouse, 0x05 = side button 5, 0x06 = side button 6.
    public int MacroKey { get; set; } = 0x05;

    // No-recoil — AegisMouse model: per-shot pull from a progression table,
    // scaled by a strength MULTIPLIER, paced at the weapon's fire rate (RPM).
    public bool RecoilOn { get; set; } = true;

    /// <summary>Recoil compensation multiplier (AegisMouse-style). 1.0 = table base;
    /// raise if the crosshair still climbs, lower if it drifts down.</summary>
    public double RecoilStrength { get; set; } = 1.0;

    public int RecoilHorizontal { get; set; } = 0; // px per shot bias

    // Rapid fire
    public bool RapidOn { get; set; } = true;
    public int RapidRpm { get; set; } = 600;

    // Presets: name -> band
    public static Dictionary<string, Band> Presets { get; } = new()
    {
        ["Knives Out — Bot Body"]   = new Band { HMin = 95,  HMax = 120, SMin = 40,  SMax = 95,  VMin = 180, VMax = 245 },
        ["Red — Hit Marker"]        = new Band { HMin = 0,   HMax = 8,   SMin = 200, SMax = 255, VMin = 200, VMax = 255 },
        ["Red — Wide (outlines)"]   = new Band { HMin = 0,   HMax = 10,  SMin = 140, SMax = 255, VMin = 140, VMax = 255 },
        ["Orange"]                  = new Band { HMin = 10,  HMax = 30,  SMin = 100, SMax = 255, VMin = 100, VMax = 255 },
        ["Magenta/Pink"]            = new Band { HMin = 140, HMax = 170, SMin = 100, SMax = 255, VMin = 100, VMax = 255 },
    };

    static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AutoAimLock");
    static string File => Path.Combine(Dir, "settings.json");

    public static Settings Load()
    {
        try
        {
            if (System.IO.File.Exists(File))
                return JsonSerializer.Deserialize<Settings>(System.IO.File.ReadAllText(File)) ?? new Settings();
        }
        catch { }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            System.IO.File.WriteAllText(File, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
