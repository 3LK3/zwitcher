using System.Diagnostics;

namespace Zwitcher.Core;

/// <summary>The current settings. UI thread only, like all mutable state.</summary>
public sealed class SettingsService(ISettingsStore store)
{
    public Settings Current { get; private set; } = store.Load();

    public event Action<Settings>? Changed;

    /// <summary>
    /// Applies and saves a change. Does nothing if the settings stay the same, so that controls
    /// showing the same setting don't echo each other.
    /// </summary>
    public void Update(Func<Settings, Settings> change)
    {
        var updated = change(Current);
        if (updated == Current)
        {
            return;
        }

        Current = updated;
        try
        {
            store.Save(updated);
        }
        // The setting still applies until the next start, which beats losing the change.
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning($"Saving settings failed: {e.Message}");
        }

        Changed?.Invoke(updated);
    }
}
