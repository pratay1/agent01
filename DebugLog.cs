using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;

namespace PhysicsSandbox;

public static class DebugLog
{
    private static readonly string _logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug.log");
    private static readonly object _lock = new();
    private static StreamWriter? _writer;
    private static bool _initialized;

    public static void Init()
    {
        if (_initialized) return;
        try
        {
            _writer = new StreamWriter(_logPath, false) { AutoFlush = true };
            _writer.WriteLine($"=== DEBUG LOG STARTED: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} ===");
            _initialized = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to init debug log: {ex.Message}");
        }
    }

    public static void Log(string message, string level = "INFO")
    {
        lock (_lock)
        {
            var entry = $"[{DateTime.Now:HH:mm:ss.fff}] [{level}] {message}";
            Console.WriteLine(entry);
            try { _writer?.WriteLine(entry); } catch { }
        }
    }

    public static void LogError(string message, Exception? ex = null)
    {
        var msg = ex != null ? $"{message}: {ex.Message}\n{ex.StackTrace}" : message;
        Log(msg, "ERROR");
    }

    public static void LogWarning(string message) => Log(message, "WARN");
    public static void LogDebug(string message) => Log(message, "DEBUG");

    public static void Shutdown()
    {
        lock (_lock)
        {
            Log("=== DEBUG LOG SHUTDOWN ===");
            _writer?.Dispose();
            _initialized = false;
        }
    }
}
