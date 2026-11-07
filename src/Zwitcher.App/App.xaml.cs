using System.Windows;
using System.Windows.Interop;

namespace Zwitcher.App;

public sealed partial class App : Application
{
    private MainWindow? _overlay;
    private TrayIcon? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Pre-create the overlay: build the HWND now but never call Show(), so later
        // show/hide is fast. EnsureHandle creates the native window without displaying it.
        _overlay = new MainWindow();
        new WindowInteropHelper(_overlay).EnsureHandle();

        _tray = new TrayIcon();
        _tray.ExitRequested += (_, _) => Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        base.OnExit(e);
    }
}
