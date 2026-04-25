using System.Configuration;
using System.Data;
using System.Windows;
using System;

namespace PhysicsSandbox;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public App()
    {
        // Initialize the mega logger early
        DebugLog.Init();
        Logger.MinimumLevel = Logger.LogLevel.Debug;
        // Capture unhandled UI thread exceptions
        this.DispatcherUnhandledException += (s, e) =>
        {
            Logger.LogError("Unhandled UI thread exception", e.Exception);
            e.Handled = true;
        };
        // Capture non-UI thread exceptions
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            Logger.LogFatal("Unhandled domain exception", e.ExceptionObject as Exception);
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Logger.LogInfo("Application exiting");
        DebugLog.Shutdown();
        base.OnExit(e);
    }
}
