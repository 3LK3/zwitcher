using System.Windows;
using System.Windows.Interop;
using Zwitcher.Core;

namespace Zwitcher.App;

public sealed partial class App : Application
{
    // Local\ scopes the mutex to the session, like the keyboard hook: another signed-in user
    // can run their own instance.
    private const string InstanceMutexName = @"Local\Zwitcher-7f3c1a52-8e4b-4d06-9b1e-2c5d6a90f3e7";

    private SingleInstanceGuard? _instanceGuard;
    private MainWindow? _overlay;
    private TrayIcon? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _instanceGuard = new SingleInstanceGuard(InstanceMutexName);
        if (!_instanceGuard.IsFirstInstance)
        {
            Shutdown();
            return;
        }

        // Pre-create the overlay: build the HWND now but never call Show(), so later
        // show/hide is fast. EnsureHandle creates the native window without displaying it.
        _overlay = new MainWindow();
        new WindowInteropHelper(_overlay).EnsureHandle();

        _tray = new TrayIcon();
        _tray.AboutRequested += (_, _) => ShowAbout();
        _tray.ExitRequested += (_, _) => Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _instanceGuard?.Dispose();
        base.OnExit(e);
    }

    private static void ShowAbout()
    {
        var version = typeof(App).Assembly.GetName().Version?.ToString(3);
        MessageBox.Show($"Zwitcher {version}", "About Zwitcher");
    }
}
