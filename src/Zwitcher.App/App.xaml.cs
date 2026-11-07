using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using Zwitcher.App.Debugging;
using Zwitcher.Core;
using Zwitcher.Core.Input;
using Zwitcher.Core.Interop;
using Zwitcher.Core.Windows;

namespace Zwitcher.App;

public sealed partial class App : Application
{
    // Local\ scopes the mutex to the session, like the keyboard hook: another signed-in user
    // can run their own instance.
    private const string InstanceMutexName = @"Local\Zwitcher-7f3c1a52-8e4b-4d06-9b1e-2c5d6a90f3e7";

    private SingleInstanceGuard? _instanceGuard;
    private MainWindow? _overlay;
    private MessageWindow? _messageWindow;
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

#if DEBUG
        StartDebugLog();
#endif

        var tracker = new WindowTracker(new Win32WindowQuery());

        // Pre-create the overlay: build the HWND now but never call Show(), so later
        // show/hide is fast. EnsureHandle creates the native window without displaying it.
        _overlay = new MainWindow();
        new WindowInteropHelper(_overlay).EnsureHandle();

        var state = new SwitcherState();
        var controller = new SwitcherController(
            tracker,
            new LoggingOverlayView(),
            new LoggingWindowActivator(),
            state
        );
        _messageWindow = new MessageWindow(controller.Handle);

        _tray = new TrayIcon();
        _tray.AboutRequested += (_, _) => ShowAbout();
        _tray.ExitRequested += (_, _) => Shutdown();

#if DEBUG
        var poster = new CommandPoster(_messageWindow.Handle);
        _tray.PostTestCommandsRequested += (_, _) => PostTestCommands(poster, state);
        _tray.DumpWindowsRequested += (_, _) => DumpWindows(tracker);
#endif
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _messageWindow?.Dispose();
        _instanceGuard?.Dispose();
        base.OnExit(e);
    }

#if DEBUG
    private static void StartDebugLog()
    {
        // A WinExe has no console, so Debug output would need an attached debugger.
        var path = Path.Combine(Path.GetTempPath(), "Zwitcher-debug.log");
        Trace.Listeners.Add(new TextWriterTraceListener(path));
        Trace.AutoFlush = true;
        Debug.WriteLine($"--- started {DateTime.Now:O}");
    }

    private static void PostTestCommands(CommandPoster poster, SwitcherState state)
    {
        // The hook sets the flag itself when it posts an activate command.
        state.IsActive = true;
        poster.Post(SwitcherCommand.Activate);
        poster.Post(SwitcherCommand.Next);
        poster.Post(SwitcherCommand.Next);
        poster.Post(SwitcherCommand.Confirm);
    }

    private static void DumpWindows(IWindowTracker tracker)
    {
        var windows = tracker.Snapshot();
        Debug.WriteLine($"--- {windows.Count} windows");
        foreach (var window in windows)
        {
            Debug.WriteLine($"{window.Handle} pid={window.ProcessId} \"{window.Title}\"");
        }
    }
#endif

    private static void ShowAbout()
    {
        var version = typeof(App).Assembly.GetName().Version?.ToString(3);
        MessageBox.Show($"Zwitcher {version}", "About Zwitcher");
    }
}
