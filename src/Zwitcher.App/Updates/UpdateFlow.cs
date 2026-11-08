using Microsoft.Extensions.Logging;
using Velopack;
using Velopack.Sources;
using Zwitcher.App.Tray;

namespace Zwitcher.App.Updates;

/// <summary>
/// Checks GitHub releases for an update once and offers it in the tray. Nothing is installed
/// until the user clicks the offer.
/// </summary>
public sealed class UpdateFlow(TrayIcon tray, ILogger<UpdateFlow> logger)
{
    private readonly UpdateManager _manager = new(
        new GithubSource(AppInfo.RepositoryUrl, null, false)
    );

    /// <summary>
    /// Meant to be fire-and-forget, so that a slow network never delays the hook going live.
    /// Resumes on the UI thread (WPF sync context), so touching the tray is safe. Never throws.
    /// </summary>
    public async Task RunAsync()
    {
        try
        {
            // Portable and dev builds have no Velopack install to update.
            var update = _manager.IsInstalled ? await _manager.CheckForUpdatesAsync() : null;

            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    "Update check: {Version}",
                    update?.TargetFullRelease.Version.ToString() ?? "none"
                );
            }

            if (update is null)
            {
                return;
            }

            tray.UpdateRequested += async (_, _) =>
            {
                tray.SetUpdateState("Downloading update...", enabled: false);
                try
                {
                    await _manager.DownloadUpdatesAsync(update);
                    _manager.ApplyUpdatesAndRestart(update);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Update failed");
                    tray.SetUpdateState("Update failed, retry", enabled: true);
                }
            };
            tray.SetUpdateAvailable(update.TargetFullRelease.Version.ToString());
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Update check failed");
        }
    }
}
