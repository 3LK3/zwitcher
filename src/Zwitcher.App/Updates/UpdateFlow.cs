using System.Diagnostics;
using Zwitcher.App.Tray;

namespace Zwitcher.App.Updates;

/// <summary>
/// Checks for an update once and offers it in the tray. Nothing is installed until the user
/// clicks the offer.
/// </summary>
public sealed class UpdateFlow(TrayIcon tray)
{
    private readonly AppUpdater _updater = new();

    /// <summary>
    /// Meant to be fire-and-forget, so that a slow network never delays the hook going live.
    /// Resumes on the UI thread (WPF sync context), so touching the tray is safe. Never throws.
    /// </summary>
    public async Task RunAsync()
    {
        try
        {
            var update = await _updater.CheckAsync();
            Trace.TraceInformation(
                $"Update check: {update?.TargetFullRelease.Version.ToString() ?? "none"}"
            );
            if (update is null)
            {
                return;
            }

            tray.UpdateRequested += async (_, _) =>
            {
                tray.SetUpdateBusy("Downloading update...");
                try
                {
                    await _updater.DownloadAndApplyAsync(update);
                }
                catch (Exception ex)
                {
                    Trace.TraceError($"Update failed: {ex}");
                    tray.SetUpdateRetry("Update failed, retry");
                }
            };
            tray.SetUpdateAvailable(update.TargetFullRelease.Version.ToString());
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Update check failed: {ex}");
        }
    }
}
