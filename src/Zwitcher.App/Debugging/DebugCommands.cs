#if DEBUG
using System.Diagnostics;
using System.Windows.Threading;
using Zwitcher.App.Tray;
using Zwitcher.Core.Input;
using Zwitcher.Core.Switching;
using Zwitcher.Core.WindowList;

namespace Zwitcher.App.Debugging;

/// <summary>Debug-build helpers: tray items for manual tests and a crash test switch.</summary>
public static class DebugCommands
{
    /// <summary>Wires the tray's debug menu items.</summary>
    public static void Attach(
        TrayIcon tray,
        CommandPoster poster,
        SwitcherActiveFlag activeFlag,
        IWindowTracker tracker
    )
    {
        tray.PostTestCommandsRequested += (_, _) => PostTestCommands(poster, activeFlag);
        tray.DumpWindowsRequested += (_, _) => DumpWindows(tracker);
    }

    /// <summary>`--crash-test=ui` or `--crash-test=pool` throws shortly after startup.</summary>
    public static void StartCrashTest(string[] args)
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

    private static void PostTestCommands(CommandPoster poster, SwitcherActiveFlag activeFlag)
    {
        // The hook sets the flag itself when it posts an activate command.
        activeFlag.IsSet = true;
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
}
#endif
