using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Zwitcher.Core;
using Zwitcher.Core.Activation;
using Zwitcher.Core.Diagnostics;
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
    private RollingFileTraceListener? _log;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _instanceGuard = new SingleInstanceGuard(InstanceMutexName);
        if (!_instanceGuard.IsFirstInstance)
        {
            Shutdown();
            return;
        }

        StartLog();
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += (_, args) =>
            Trace.TraceWarning($"Unobserved task exception: {args.Exception}");

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
        if (hook.IsInstalled)
        {
            Trace.TraceInformation("Keyboard hook installed: True");
        }
        else
        {
            Trace.TraceError("Keyboard hook installed: False, the native Alt+Tab stays active");
        }
        _hook = hook;

        settings.Changed += current =>
        {
            hook.Enabled = current.Enabled;
            overlay.ApplySettings(current.ShowOn, current.Layout);
            tray.SetEnabled(current.Enabled);
        };

#if DEBUG
        StartCrashTest(e.Args);
#endif
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
        if (_log is not null)
        {
            Trace.TraceInformation("Exited");
            Trace.Listeners.Remove(_log);
            _log.Dispose();
        }

        base.OnExit(e);
    }

    private void StartLog()
    {
        // After the single-instance check, so that a second launch can't write to the same file.
        _log = new RollingFileTraceListener(RollingFileTraceListener.DefaultDirectory);
        Trace.Listeners.Add(_log);
        Trace.TraceInformation(
            $"Zwitcher {Version()} started on Windows {Environment.OSVersion.Version}, exe {Environment.ProcessPath}"
        );
    }

    /// <summary>
    /// Runs for an unhandled exception on any thread; WPF passes unhandled UI-thread exceptions
    /// on to here. Zwitcher still exits: carrying on in an unknown state is worse for a
    /// switcher than falling back to the native Alt+Tab. Windows would remove the hook with
    /// the process anyway, but unhooking first means no keystroke waits on a dying process.
    /// </summary>
    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Trace.TraceError($"Unhandled exception, Zwitcher exits: {e.ExceptionObject}");
        _hook?.Dispose();

        // NotifyIcon belongs to the UI thread. From another thread, the icon stays as a ghost
        // until the mouse moves over it.
        if (Dispatcher.CheckAccess())
        {
            _tray?.Dispose();
        }
    }

#if DEBUG
    /// <summary>`--crash-test=ui` or `--crash-test=pool` throws shortly after startup.</summary>
    private void StartCrashTest(string[] args)
    {
        var target = args.FirstOrDefault(a =>
                a.StartsWith("--crash-test=", StringComparison.Ordinal)
            )
            ?.Split('=')[1];
        if (target is null)
        {
            return;
        }

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (target == "pool")
            {
                ThreadPool.QueueUserWorkItem(_ =>
                    throw new InvalidOperationException("Crash test on a pool thread")
                );
            }
            else
            {
                throw new InvalidOperationException("Crash test on the UI thread");
            }
        };
        timer.Start();
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
        MessageBox.Show(
            $"Zwitcher {Version()}\n\nLogs: {RollingFileTraceListener.DefaultDirectory}",
            "About Zwitcher"
        );
    }

    /// <summary>SemVer plus the short commit, e.g. 1.0.0+abc1234. The SDK appends the commit.</summary>
    private static string Version()
    {
        var version =
            typeof(App)
                .Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion
            ?? "unknown";
        var plus = version.IndexOf('+');
        return plus >= 0 && version.Length > plus + 8 ? version[..(plus + 8)] : version;
    }
}
