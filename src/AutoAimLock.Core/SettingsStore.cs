using System.IO;
using System.Text.Json;
using AUTOAIMLOCK.Core.Models;

namespace AUTOAIMLOCK.Core;

/// <summary>
/// Simple JSON file persistence for AimSettings.
/// No database dependency — just a single settings.json in %AppData%/AUTOAIMLOCK/.
/// </summary>
public sealed class SettingsStore
{
    static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AUTOAIMLOCK");

    static readonly string FilePath = Path.Combine(Dir, "settings.json");

    static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>Load settings from disk, or return defaults if file doesn't exist.</summary>
    public static AimSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
            var json = File.ReadAllText(FilePath);
                var s = JsonSerializer.Deserialize<AimSettings>(json, JsonOpts) ?? new AimSettings();
                // Older configs won't have the body-detection band. Normalize so the
                // secondary (body) band is always present and enabled by default.
                if (s.BodyColor == null)
                    s.BodyColor = new ColorSettings { HMin = 95, HMax = 120, SMin = 40, SMax = 95, VMin = 180, VMax = 245 };
                return s;
            }
        }
        catch
        {
            // Corrupt or unreadable — fall back to defaults
        }
        return new AimSettings();
    }

    /// <summary>Save current settings to disk.</summary>
    public static void Save(AimSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, JsonOpts));
        }
        catch
        {
            // Silent fail — settings persistence is non-critical
        }
    }

    /// <summary>Get the path where settings are stored (for display in UI).</summary>
    public static string GetPath() => FilePath;
}
