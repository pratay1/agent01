using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace PhysicsSandbox;

/// <summary>
/// Mega error logging system that logs literally everything and everything that goes wrong, including warnings.
/// Supports multiple sinks: console, file, and debug output.
/// </summary>
public static class Logger
{
    private static readonly ConcurrentQueue<string> _entries = new();
    private static StreamWriter? _fileWriter;
    private static string _logPath;
    private static bool _isInitialized;
    private static readonly object _initLock = new();
    private static bool _isShuttingDown;

    public static LogLevel MinimumLevel { get; set; } = LogLevel.Debug;

    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Fatal
    }

    public static void Init()
    {
        lock (_initLock)
        {
            if (_isInitialized) return;

            try
            {
                _logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mega_debug.log");
                _fileWriter = new StreamWriter(_logPath, false) { AutoFlush = true };
                _isInitialized = true;
                LogInternal(LogLevel.Info, "=== MEGA LOGGER STARTED ===");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to initialize logger: {ex}");
            }

            try
            {
                AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                {
                    LogFatal("Unhandled exception occurred", e.ExceptionObject as Exception);
                };

                AppDomain.CurrentDomain.ProcessExit += (_, _) => Shutdown();
            }
            catch (Exception ex)
            {
                LogError("Failed to register global exception handlers", ex);
            }
        }
    }

    public static void Shutdown()
    {
        if (_isShuttingDown) return;
        _isShuttingDown = true;

        try
        {
            LogInternal(LogLevel.Info, "=== MEGA LOGGER SHUTTING DOWN ===");
            _fileWriter?.Dispose();
            _fileWriter = null;
        }
        catch (Exception ex) { Console.Error.WriteLine($"Logger shutdown failed: {ex}"); }
    }

    private static void LogInternal(LogLevel level, string message, Exception? ex = null)
    {
        if (level < MinimumLevel) return;

        var time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var exMsg = ex != null ? $"\nException: {ex.Message}\nStack: {ex.StackTrace}" : "";
        var entry = $"[{time}] [{level}] {message}{exMsg}";

        // Write to console
        var color = level switch
        {
            LogLevel.Debug => ConsoleColor.Gray,
            LogLevel.Info => ConsoleColor.White,
            LogLevel.Warning => ConsoleColor.Yellow,
            LogLevel.Error => ConsoleColor.Red,
            LogLevel.Fatal => ConsoleColor.DarkRed,
            _ => ConsoleColor.White
        };

        lock (_initLock)
        {
            try
            {
                var prev = Console.ForegroundColor;
                Console.ForegroundColor = color;
                Console.WriteLine(entry);
                Console.ForegroundColor = prev;
            }
            catch (Exception exConsole) { Console.Error.WriteLine($"Logger console write failed: {exConsole}"); }

            try
            {
                _fileWriter?.WriteLine(entry);
            }
            catch (Exception exFile) { Console.Error.WriteLine($"Logger file write failed: {exFile}"); }
        }

        _entries.Enqueue(entry);
    }

    public static void LogDebug(string message) => LogInternal(LogLevel.Debug, message);
    public static void LogInfo(string message) => LogInternal(LogLevel.Info, message);
    public static void LogWarning(string message) => LogInternal(LogLevel.Warning, message);
    public static void LogError(string message, Exception? ex = null) => LogInternal(LogLevel.Error, message, ex);
    public static void LogFatal(string message, Exception? ex = null) => LogInternal(LogLevel.Fatal, message, ex);

    /// <summary>Log a general message with a specified level.</summary>
    public static void Log(LogLevel level, string message, Exception? ex = null)
    {
        LogInternal(level, message, ex);
    }

    /// <summary>Capture the current call stack for diagnostic purposes.</summary>
    public static void CaptureStackTrace(string context)
    {
        try
        {
            var st = new System.Diagnostics.StackTrace(true);
            LogDebug($"Stack trace [{context}]:\n{st}");
        }
        catch (Exception ex)
        {
            LogError("Failed to capture stack trace", ex);
        }
    }

    /// <summary>Drain all logged entries (for diagnostics).</summary>
    public static string[] Drain()
    {
        var list = new System.Collections.Generic.List<string>();
        while (_entries.TryDequeue(out var entry))
            list.Add(entry);
        return list.ToArray();
    }
}
