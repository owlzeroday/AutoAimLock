using System.IO;
using System.Text.Json;
using AutoAimLock.Core;
using Dapper;
using Microsoft.Data.Sqlite;

namespace AutoAimLock.Database;

/// <summary>SQLite persistence for game profiles (JSON bodies) and settings.</summary>
public sealed class MacroDatabase : IDisposable
{
    public string DatabasePath { get; }
    readonly SqliteConnection _conn;
    readonly object _sync = new();
    static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    public MacroDatabase(string? path = null)
    {
        var dir = path is null
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "AutoAimLock", "data")
            : Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);
        DatabasePath = path ?? Path.Combine(dir, "autoaimlock.db");
        _conn = new SqliteConnection($"Data Source={DatabasePath}");
        _conn.Open();
        _conn.Execute("""
            CREATE TABLE IF NOT EXISTS profiles (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                body TEXT NOT NULL,
                created_utc TEXT NOT NULL,
                updated_utc TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS settings (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            """);
        Log.Information($"AutoAimLock database opened at {DatabasePath}");
    }

    public List<GameProfile> GetProfiles()
    {
        lock (_sync)
        {
            var rows = _conn.Query<(string name, string body)>("SELECT name, body FROM profiles ORDER BY name").ToList();
            return rows.Select(r =>
            {
                try { return JsonSerializer.Deserialize<GameProfile>(r.body, Json); }
                catch (Exception ex) { Log.Warning(ex, $"Bad profile {r.name}"); return null; }
            }).Where(p => p is not null).Cast<GameProfile>().ToList();
        }
    }

    public void SaveProfile(GameProfile p)
    {
        lock (_sync)
        {
            _conn.Execute("""
                INSERT INTO profiles (id, name, body, created_utc, updated_utc) VALUES (@id, @name, @body, @created, @updated)
                ON CONFLICT(id) DO UPDATE SET name=@name, body=@body, updated_utc=@updated
                """, new
            {
                id = p.Id, name = p.Name, body = JsonSerializer.Serialize(p, Json),
                created = DateTime.UtcNow, updated = DateTime.UtcNow
            });
        }
    }

    public void DeleteProfile(string id)
    {
        lock (_sync) _conn.Execute("DELETE FROM profiles WHERE id=@id", new { id });
    }

    public string? GetSetting(string key)
    {
        lock (_sync) return _conn.QueryFirstOrDefault<string>("SELECT value FROM settings WHERE key=@key", new { key });
    }

    public void SetSetting(string key, string value)
    {
        lock (_sync)
            _conn.Execute("""
                INSERT INTO settings (key, value) VALUES (@key, @value)
                ON CONFLICT(key) DO UPDATE SET value=@value
                """, new { key, value });
    }

    public void Dispose() => _conn.Dispose();
}

public static class AppPaths
{
    public static string Root =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "AutoAimLock");
    public static string DatabasePath => Path.Combine(Root, "data", "autoaimlock.db");
    public static void EnsureDirectories() => Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
}
