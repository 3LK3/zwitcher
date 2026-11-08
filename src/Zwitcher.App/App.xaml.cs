using System.IO;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Extensions.Logging;
using Serilog;
using Velopack;
using Zwitcher.App.Overlay;
using Zwitcher.App.Settings;
using Zwitcher.App.Tray;
using Zwitcher.Core;
using Zwitcher.Core.Configuration;
using Zwitcher.Core.Input;
using Zwitcher.Core.Switching;
using Zwitcher.Core.WindowList;
#if DEBUG
using Zwitcher.App.Debugging;
#else
using Zwitcher.App.Updates;
#endif

namespace Zwitcher.App;

/// <summary>
/// The composition root: creates and wires the components in the order from ARCHITECTURE.md,
/// and tears them down in reverse.
/// </summary>
public sealed partial class App : Application
{
    // Local\ scopes the mutex to the session, like the keyboard hook: another signed-in user
    // can run their own instance.
    private const string InstanceMutexName = @"Local\Zwitcher-7f3c1a52-8e4b-4d06-9b1e-2c5d6a90f3e7";

    private ILoggerFactory? _loggerFactory;
    private ILogger<App>? _logger;

    private SingleInstanceGuard? _instanceGuard;
    private Win32WindowEventSource? _windowEvents;
    private MessageWindow? _messageWindow;
    private TrayIcon? _tray;
    private KeyboardHook? _hook;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _instanceGuard = new SingleInstanceGuard(InstanceMutexName);
        if (!_instanceGuard.IsFirstInstance)
        {
            Shutdown();
            return;
        }

        StartLogging();
        _logger = _loggerFactory?.CreateLogger<App>();
#pragma warning disable CA1873 // Avoid potentially expensive logging
        _logger?.LogInformation(
            "Zwitcher [{Version}] started on Windows {OsVersion}",
            AppInfo.Version(),
            Environment.OSVersion.Version
        );
#pragma warning restore CA1873 // Avoid potentially expensive logging

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += (_, args) =>
            _logger?.LogError(args.Exception, "Unobserved task exception");

        var settings = new SettingsStore(
            new JsonSettingsFile(JsonSettingsFile.DefaultPath),
            _loggerFactory!.CreateLogger<SettingsStore>()
        );

        var startup = StartupRegistration.ForCurrentUser(
            Environment.ProcessPath!,
            _loggerFactory!.CreateLogger<StartupRegistration>()
        );
        startup.RepairPath();

        _windowEvents = new Win32WindowEventSource();
        var tracker = new WindowTracker(
            new Win32WindowQuery(),
            _windowEvents,
            _loggerFactory!.CreateLogger<WindowTracker>()
        );
        tracker.Start();

        var overlay = CreateOverlay(settings.Current);

        var activeFlag = new SwitcherActiveFlag();
        var controller = new SwitcherController(
            tracker,
            overlay,
            new Win32WindowActivator(_loggerFactory!.CreateLogger<Win32WindowActivator>()),
            activeFlag
        );
        _messageWindow = new MessageWindow(controller.Handle);
        var poster = new CommandPoster(_messageWindow.Handle);

        // Posted, not called: WinEvents can arrive during any message pumping, so the controller
        // could be in the middle of something. Only while active, so idle costs nothing.
        tracker.WindowsChanged += () =>
        {
            if (activeFlag.IsSet)
            {
                poster.Post(SwitcherCommand.WindowsChanged);
            }
        };

        var settingsFlyout = new SettingsFlyoutOpener(settings, startup, _loggerFactory!);
        var tray = new TrayIcon();
        tray.SetEnabled(settings.Current.Enabled);
        tray.EnabledChanged += (_, enabled) => settings.Update(s => s with { Enabled = enabled });
        tray.SettingsRequested += (_, request) => settingsFlyout.Open(request);
        tray.ExitRequested += (_, _) => Shutdown();
        _tray = tray;

#if DEBUG
        DebugCommands.Attach(
            tray,
            poster,
            activeFlag,
            tracker,
            _loggerFactory!.CreateLogger("DebugCommands")
        );
#else
        _ = new UpdateFlow(tray, _loggerFactory!.CreateLogger<UpdateFlow>()).RunAsync();
#endif

        // Last: commands may arrive from here on, so everything else must be ready.
        var hook = StartKeyboardHook(poster, activeFlag, settings.Current.Enabled);
        _hook = hook;

        settings.Changed += current =>
        {
            hook.Enabled = current.Enabled;
            overlay.ApplySettings(current.ShowOn, current.Layout);
            tray.SetEnabled(current.Enabled);
        };

#if DEBUG
        DebugCommands.StartCrashTest(e.Args);
#endif
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // The hook goes first, so that no new commands arrive during shutdown.
        _hook?.Dispose();
        _windowEvents?.Dispose();
        _tray?.Dispose();
        _messageWindow?.Dispose();
        _instanceGuard?.Dispose();
        _logger?.LogInformation("Exited");
        _loggerFactory?.Dispose();

        base.OnExit(e);
    }

    private void StartLogging(bool useConsole = false)
    {
        var config = new LoggerConfiguration()
#if DEBUG
            .MinimumLevel.Verbose() // Everything for development
            .WriteTo.Debug() // Also write to debug output
#else
            .MinimumLevel.Information() // Information during normal operation.
#endif
            .Enrich.WithProperty("Version", AppInfo.Version())
            .WriteTo.File(
                ZwitcherLog.FilePath,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
                fileSizeLimitBytes: 1024 * 1024, // 1 MB
                rollOnFileSizeLimit: true,
                retainedFileCountLimit: 5,
                shared: false
            );

        if (useConsole)
        {
            config.WriteTo.Console(
                outputTemplate: "{Timestamp:HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}"
            );
        }

        var serilog = config.CreateLogger();
        _loggerFactory = new LoggerFactory().AddSerilog(serilog, dispose: true);
    }

    /// <summary>
    /// Builds the HWND now but never shows it for real, so later show/hide is fast.
    /// EnsureHandle creates the native window without displaying it.
    /// </summary>
    private OverlayWindow CreateOverlay(Core.Configuration.Settings settings)
    {
        var overlay = new OverlayWindow(_loggerFactory!.CreateLogger<OverlayWindow>());
        overlay.ApplySettings(settings.ShowOn, settings.Layout);
        new WindowInteropHelper(overlay).EnsureHandle();
        overlay.WarmUp();
        return overlay;
    }

    private KeyboardHook StartKeyboardHook(
        CommandPoster poster,
        SwitcherActiveFlag activeFlag,
        bool enabled
    )
    {
        var hook = new KeyboardHook(poster, activeFlag) { Enabled = enabled };
        hook.Start();
        if (hook.IsInstalled)
        {
            _logger?.LogInformation("Keyboard hook installed");
        }
        else
        {
            _logger?.LogError("Unable to install keyboard hook: the native Alt+Tab stays active");
        }
        return hook;
    }

    /// <summary>
    /// Runs for an unhandled exception on any thread; WPF passes unhandled UI-thread exceptions
    /// on to here. Zwitcher still exits: carrying on in an unknown state is worse for a
    /// switcher than falling back to the native Alt+Tab. Windows would remove the hook with
    /// the process anyway, but unhooking first means no keystroke waits on a dying process.
    /// </summary>
    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        _logger?.LogCritical(e.ExceptionObject as Exception, "Unhandled exception");
        _hook?.Dispose();

        // NotifyIcon belongs to the UI thread. From another thread, the icon stays as a ghost
        // until the mouse moves over it.
        if (Dispatcher.CheckAccess())
        {
            _tray?.Dispose();
        }
    }

    [STAThread]
    private static void Main(string[] args)
    {
        // Velopack sends its install/update hooks to Zwitcher.Launcher, the main exe. This call
        // stays because the app is also started directly (Run key, dev): it applies an update
        // that was downloaded but not installed yet (SetAutoApplyOnStartup is on by default).
        VelopackApp.Build().Run();

        App app = new();
        app.InitializeComponent();
        app.Run();
    }
}
