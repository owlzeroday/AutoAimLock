using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AutoAimLock.Supabase;

/// <summary>Caches the latest license/session locally (DPAPI-encrypted JSON) so a
/// user who has validated stays usable during short offline periods (grace).</summary>
public sealed class LocalSession
{
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public string UserId { get; set; } = "";
    public string Email { get; set; } = "";
    public string Tier { get; set; } = "";
    public string Plan { get; set; } = "";
    public string ExpiresAt { get; set; } = "";
    public DateTime CachedAtUtc { get; set; } = DateTime.UtcNow;

    static string Path => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AutoAimLock", "session.bin");

    public void Save()
    {
        try
        {
            var plain = JsonSerializer.Serialize(this);
            var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), null, DataProtectionScope.CurrentUser);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.WriteAllBytes(Path, bytes);
        }
        catch { }
    }

    public static LocalSession? Load()
    {
        try
        {
            if (!File.Exists(Path)) return null;
            var bytes = File.ReadAllBytes(Path);
            var plain = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<LocalSession>(Encoding.UTF8.GetString(plain));
        }
        catch { return null; }
    }

    public static void Delete() { try { if (File.Exists(Path)) File.Delete(Path); } catch { } }
}
