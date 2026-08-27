using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace AutoAimLock.Supabase;

/// <summary>Stable device identifier (hashed machine GUID) for license binding.
/// Never raw hardware data — only a SHA-256 of the machine GUID.</summary>
public static class DeviceFingerprint
{
    public static string Get()
    {
        string raw = "";
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            raw = k?.GetValue("MachineGuid") as string ?? "";
        }
        catch { }
        if (string.IsNullOrEmpty(raw)) raw = Environment.MachineName + Environment.ProcessorCount;
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes("aalock:" + raw))).ToLowerInvariant();
    }
}
