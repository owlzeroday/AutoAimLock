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
    public int MinPixels { get; set; } = 14;
    public Band ColorBand { get; set; } = new();
    public bool InvertFovCheck { get; set; } = false;

    // No-recoil (continuous counter-pull while firing; per-shot when rapid fire is on)
    public bool RecoilOn { get; set; } = true;
    public int RecoilStrength { get; set; } = 25;  // px per second downward (per shot in combined mode)
    public int RecoilHorizontal { get; set; } = 0; // px per second bias

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
