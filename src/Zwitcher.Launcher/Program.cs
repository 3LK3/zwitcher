using System.ComponentModel;
using System.Diagnostics;
using Velopack;

namespace Zwitcher.Launcher;

/// <summary>
/// Handles Velopack's hooks, then starts Zwitcher.App.exe through ShellExecute and exits.
/// ShellExecute goes through the AppInfo service, which is the only way a normal process can
/// start a uiAccess exe. CreateProcess fails with ERROR_ELEVATION_REQUIRED.
/// https://learn.microsoft.com/windows/security/application-security/application-control/user-account-control/how-it-works
/// </summary>
internal static class Program
{
    private const string AppExeName = "Zwitcher.App.exe";

    private static int Main(string[] args)
    {
        // Velopack starts the main exe with --veloapp-* arguments for install, update and
        // uninstall hooks. Run() handles those and exits the process; otherwise it returns.
        VelopackApp.Build().Run();

        var startInfo = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, AppExeName))
        {
            UseShellExecute = true,
            WorkingDirectory = AppContext.BaseDirectory,
        };
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        try
        {
            Process.Start(startInfo)?.Dispose();
            return 0;
        }
        catch (Win32Exception e)
        {
            // No UI here on purpose. The exit code is what Velopack and a terminal see.
            return e.NativeErrorCode;
        }
    }
}
