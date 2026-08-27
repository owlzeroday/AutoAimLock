using System.IO;

namespace AutoAimLock.Supabase;

/// <summary>
/// Reads runtime client config from a gitignored file (never ships/hardcodes secrets).
/// File: .secrets/client.env  (SUPABASE_URL + SUPABASE_ANON_KEY)
/// The anon/publishable key is safe to ship, but we keep it out of the binary so
/// the same build can point at any backend without recompiling.
/// </summary>
public static class SupabaseConfig
{
    public static string Url { get; private set; } = "https://lawqsbibfynlimcbcjkm.supabase.co";
    public static string AnonKey { get; private set; } = "";

    static SupabaseConfig()
    {
        foreach (var path in new[]
        {
            // next to the exe (deploy next to binary)
            Path.Combine(AppContext.BaseDirectory, ".secrets", "client.env"),
            // dev tree
            Path.Combine(Directory.GetCurrentDirectory(), ".secrets", "client.env"),
        })
        {
            if (!File.Exists(path)) continue;
            foreach (var line in File.ReadAllLines(path))
            {
                if (line.StartsWith("SUPABASE_URL=")) Url = line["SUPABASE_URL=".Length..].Trim();
                else if (line.StartsWith("SUPABASE_ANON_KEY=")) AnonKey = line["SUPABASE_ANON_KEY=".Length..].Trim();
            }
            break;
        }
    }
}
