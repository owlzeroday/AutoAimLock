using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AutoAimLock.Supabase;

/// <summary>Result of calling the get-license edge function.</summary>
public sealed class LicenseInfo
{
    public bool Ok { get; set; }
    public string Tier { get; set; } = "expired";   // trial | paid
    public string Plan { get; set; } = "";           // trial | 3days | 7days | 1month
    public string ExpiresAt { get; set; } = "";
    public string TrialStartedAt { get; set; } = "";
    public string Error { get; set; } = "";
    public bool IsUsable => Ok && Tier is "trial" or "paid";
}

/// <summary>Calls the hosted get-license Supabase function with the user's JWT.</summary>
public sealed class LicenseClient
{
    readonly HttpClient _http;
    readonly string _url;

    public LicenseClient(string supabaseUrl)
    {
        _url = supabaseUrl.TrimEnd('/');
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
    }

    public async Task<LicenseInfo> Check(string accessToken)
    {
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Post, $"{_url}/functions/v1/get-license");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            req.Content = new StringContent(JsonSerializer.Serialize(new { device = DeviceFingerprint.Get() }),
                Encoding.UTF8, "application/json");
            var resp = await _http.SendAsync(req);
            var txt = await resp.Content.ReadAsStringAsync();
            var j = JsonSerializer.Deserialize<JsonElement>(txt);
            var info = new LicenseInfo { Ok = j.TryGetProperty("ok", out var ok) && ok.GetBoolean() };
            if (j.TryGetProperty("tier", out var tier)) info.Tier = tier.GetString() ?? "";
            if (j.TryGetProperty("plan", out var plan)) info.Plan = plan.GetString() ?? "";
            if (j.TryGetProperty("expires_at", out var exp)) info.ExpiresAt = exp.GetString() ?? "";
            if (j.TryGetProperty("trial_started_at", out var t)) info.TrialStartedAt = t.GetString() ?? "";
            if (j.TryGetProperty("error", out var e)) info.Error = e.GetString() ?? "";
            return info;
        }
        catch (Exception ex)
        {
            return new LicenseInfo { Ok = false, Error = ex.Message };
        }
    }
}
