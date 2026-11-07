using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using Zwitcher.Core;
using Zwitcher.Core.Activation;
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
    private WinEventSource? _windowEvents;
    private OverlayWindow? _overlay;
    private MessageWindow? _messageWindow;
    private TrayIcon? _tray;
    private KeyboardHook? _hook;
    private SettingsWindow? _settingsWindow;

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

        var settings = new SettingsService(new JsonSettingsStore(JsonSettingsStore.DefaultPath));
        var startup = StartupRegistration.ForCurrentUser(Environment.ProcessPath!);
        startup.RepairPath();

        _windowEvents = new WinEventSource();
        var tracker = new WindowTracker(new Win32WindowQuery(), _windowEvents);
        tracker.Start();

        // Pre-create the overlay: build the HWND now but never call Show(), so later
        // show/hide is fast. EnsureHandle creates the native window without displaying it.
        var overlay = new OverlayWindow();
        overlay.ApplySettings(settings.Current.ShowOn, settings.Current.Layout);
        new WindowInteropHelper(overlay).EnsureHandle();
        overlay.WarmUp();
        _overlay = overlay;

        var state = new SwitcherState();
        var controller = new SwitcherController(tracker, _overlay, new WindowActivator(), state);
        _messageWindow = new MessageWindow(controller.Handle);

        var tray = new TrayIcon();
        tray.SetEnabled(settings.Current.Enabled);
        tray.EnabledChanged += (_, enabled) => settings.Update(s => s with { Enabled = enabled });
        tray.SettingsRequested += (_, _) => ShowSettings(settings, startup);
        tray.AboutRequested += (_, _) => ShowAbout();
        tray.ExitRequested += (_, _) => Shutdown();
        _tray = tray;

        var poster = new CommandPoster(_messageWindow.Handle);

        // Posted, not called: WinEvents can arrive during any message pumping, so the controller
        // could be in the middle of something. Only while active, so idle costs nothing.
        tracker.WindowsChanged += () =>
        {
            if (state.IsActive)
            {
                poster.Post(SwitcherCommand.WindowsChanged);
            }
        };

#if DEBUG
        _tray.PostTestCommandsRequested += (_, _) => PostTestCommands(poster, state);
        _tray.DumpWindowsRequested += (_, _) => DumpWindows(tracker);
#endif

        // Last: commands may arrive from here on, so everything else must be ready.
        var hook = new KeyboardHook(poster, state) { Enabled = settings.Current.Enabled };
        hook.Start();
        Debug.WriteLine($"Keyboard hook installed: {hook.IsInstalled}");
        _hook = hook;

        settings.Changed += current =>
        {
            hook.Enabled = current.Enabled;
            overlay.ApplySettings(current.ShowOn, current.Layout);
            tray.SetEnabled(current.Enabled);
        };
    }

    private void ShowSettings(SettingsService settings, StartupRegistration startup)
    {
        if (_settingsWindow is { } open)
        {
            open.Activate();
            return;
        }

        var window = new SettingsWindow(settings, startup);
        window.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // The hook goes first, so that no new commands arrive during shutdown.
        _hook?.Dispose();
        _windowEvents?.Dispose();
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
