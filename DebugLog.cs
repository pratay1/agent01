using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;

namespace PhysicsSandbox;

/// <summary>
/// Updated DebugLog that delegates to the mega Logger for comprehensive logging.
/// </summary>
public static class DebugLog
{
    private static bool _initialized;

    public static void Init()
    {
        if (_initialized) return;
        Logger.Init();
        _initialized = true;
    }

    public static void Log(string message, string level = "INFO")
    {
        switch (level.ToUpper())
        {
            case "DEBUG":
                Logger.LogDebug(message);
                break;
            case "INFO":
                Logger.LogInfo(message);
                break;
            case "WARN":
                Logger.LogWarning(message);
                break;
            case "ERROR":
                Logger.LogError(message);
                break;
            default:
                Logger.Log(Logger.LogLevel.Info, message);
                break;
        }
    }

    public static void LogError(string message, Exception? ex = null) =>
        Logger.LogError(message, ex);

    public static void LogWarning(string message) => Logger.LogWarning(message);
    public static void LogDebug(string message) => Logger.LogDebug(message);

    public static void Shutdown() => Logger.Shutdown();
    
    /// <summary>Delegate to Logger for more control over log levels.</summary>
    public static void Log(Logger.LogLevel level, string message, Exception? ex = null) =>
        Logger.Log(level, message, ex);

    /// <summary>Capture stack trace for debugging (delegates to Logger).</summary>
    public static void CaptureStackTrace(string context) =>
        Logger.CaptureStackTrace(context);

    /// <summary>Get all logged entries (delegates to Logger).</summary>
    public static string[] Drain() => Logger.Drain();

    /// <summary>Set minimum log level.</summary>
    public static void SetMinimumLevel(Logger.LogLevel level) =>
        Logger.MinimumLevel = level;
}