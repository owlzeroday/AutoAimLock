using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AutoAimLock.Supabase;

/// <summary>JWT access + refresh tokens from Supabase Auth.</summary>
public sealed class AuthSession
{
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public SupabaseUser? User { get; set; }
}

public sealed class SupabaseUser
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public string CreatedAt { get; set; } = "";
}

public sealed class AuthResult
{
    public bool Ok { get; set; }
    public string Error { get; set; } = "";
    public AuthSession? Session { get; set; }
}

/// <summary>Supabase Auth client (email+password) via HTTP — no external SDK.</summary>
public sealed class SupabaseAuthClient
{
    readonly HttpClient _http;
    readonly string _url;
    readonly string _anon;

    public SupabaseAuthClient(string url, string anon)
    {
        _url = url.TrimEnd('/');
        _anon = anon;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        _http.DefaultRequestHeaders.Add("apikey", _anon);
    }

    async Task<string> Call(string path, object? body)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, _url + path);
        req.Headers.Add("apikey", _anon);
        if (body is not null)
            req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var resp = await _http.SendAsync(req);
        var txt = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
        {
            string msg = txt;
            try { var j = JsonSerializer.Deserialize<JsonElement>(txt); if (j.TryGetProperty("msg", out var m)) msg = m.GetString() ?? "error"; } catch { }
            throw new SupabaseAuthException((int)resp.StatusCode, msg);
        }
        return txt;
    }

    public async Task<AuthResult> SignIn(string email, string password)
    {
        try
        {
            var txt = await Call("/auth/v1/token?grant_type=password", new { email, password });
            var j = JsonSerializer.Deserialize<JsonElement>(txt);
            return new AuthResult
            {
                Ok = true,
                Session = new AuthSession
                {
                    AccessToken = j.GetProperty("access_token").GetString() ?? "",
                    RefreshToken = j.GetProperty("refresh_token").GetString() ?? "",
                    User = new SupabaseUser
                    {
                        Id = j.GetProperty("user").GetProperty("id").GetString() ?? "",
                        Email = j.GetProperty("user").GetProperty("email").GetString() ?? "",
                        CreatedAt = j.GetProperty("user").TryGetProperty("created_at", out var ca) ? ca.GetString() ?? "" : ""
                    }
                }
            };
        }
        catch (SupabaseAuthException ex) { return new AuthResult { Error = ex.Message }; }
        catch (Exception ex) { return new AuthResult { Error = ex.Message }; }
    }

    public async Task<AuthResult> SignUp(string email, string password)
    {
        try
        {
            var txt = await Call("/auth/v1/signup", new { email, password });
            var j = JsonSerializer.Deserialize<JsonElement>(txt);
            // If email confirmation is on, no session yet — treat as "created, check email".
            bool hasSession = j.TryGetProperty("access_token", out var at) && at.GetString() is { Length: > 0 } s && s.Length > 10;
            return new AuthResult
            {
                Ok = true,
                Session = hasSession ? new AuthSession
                {
                    AccessToken = j.GetProperty("access_token").GetString() ?? "",
                    RefreshToken = j.GetProperty("refresh_token").GetString() ?? "",
                    User = new SupabaseUser { Id = j.GetProperty("user").GetProperty("id").GetString() ?? "", Email = j.GetProperty("user").GetProperty("email").GetString() ?? "" }
                } : null
            };
        }
        catch (SupabaseAuthException ex) { return new AuthResult { Error = ex.Message }; }
        catch (Exception ex) { return new AuthResult { Error = ex.Message }; }
    }
}

public sealed class SupabaseAuthException : Exception
{
    public int Status { get; }
    public SupabaseAuthException(int status, string msg) : base(msg) { Status = status; }
}
