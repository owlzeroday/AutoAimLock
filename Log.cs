using System;

namespace AutoAimLock;

/// <summary>Minimal logging (Console for run, kept dependency-free).</summary>
public static class Log
{
    public static bool Verbose { get; set; }
    public static void Information(string msg) { if (Verbose) Console.WriteLine($"[INF] {msg}"); }
    public static void Information(string msg, params object?[] a) { if (Verbose) Console.WriteLine("[INF] " + string.Format(msg, a)); }
    public static void Debug(string msg) { if (Verbose) Console.WriteLine($"[DBG] {msg}"); }
    public static void Warning(string msg) { Console.WriteLine($"[WRN] {msg}"); }
    public static void Warning(string msg, params object?[] a) { Console.WriteLine("[WRN] " + string.Format(msg, a)); }
    public static void Warning(Exception ex, string msg) { Console.WriteLine($"[WRN] {msg}: {ex.Message}"); }
    public static void Warning(Exception ex, string msg, params object?[] a) { Console.WriteLine("[WRN] " + string.Format(msg, a) + ": " + ex.Message); }
}
