using Velopack;
using Velopack.Sources;

namespace Zwitcher.App.Updates;

/// <summary>Checks GitHub releases for a newer version and installs it on request.</summary>
public sealed class UpdateService
{
    private readonly UpdateManager _manager = new(
        new GithubSource("https://github.com/3LK3/zwitcher", null, false)
    );

    /// <summary>Returns the available update, or null (also when not installed via Velopack).</summary>
    public async Task<UpdateInfo?> CheckAsync()
    {
        // Portable/dev builds have no Velopack install to update.
        if (!_manager.IsInstalled)
        {
            return null;
        }

        return await _manager.CheckForUpdatesAsync();
    }

    /// <summary>Downloads the update, then restarts the app into the new version.</summary>
    public async Task DownloadAndApplyAsync(UpdateInfo update)
    {
        await _manager.DownloadUpdatesAsync(update);
        _manager.ApplyUpdatesAndRestart(update);
    }
}
