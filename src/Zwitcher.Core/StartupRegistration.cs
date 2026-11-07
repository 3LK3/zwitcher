using System.Diagnostics;
using System.Security;
using Microsoft.Win32;

namespace Zwitcher.Core;

/// <summary>
/// Start with Windows, through a value in the current user's Run key. The registry is the only
/// source of truth, so that this can't disagree with what Windows will actually do.
/// </summary>
public sealed class StartupRegistration(string keyPath, string valueName, string executablePath)
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private string Command => $"\"{executablePath}\"";

    public bool IsEnabled => ReadValue() is not null;

    public static StartupRegistration ForCurrentUser(string executablePath) =>
        new(RunKey, "Zwitcher", executablePath);

    /// <summary>Returns false if the registry refused the change.</summary>
    public bool TrySetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(keyPath);
            if (enabled)
            {
                key.SetValue(valueName, Command);
            }
            else
            {
                key.DeleteValue(valueName, throwOnMissingValue: false);
            }

            return true;
        }
        catch (Exception e)
            when (e is IOException or UnauthorizedAccessException or SecurityException)
        {
            Trace.TraceWarning($"Changing the Run key failed: {e.Message}");
            return false;
        }
    }

    /// <summary>Follows the exe if it moved. Never creates an entry the user didn't ask for.</summary>
    public void RepairPath()
    {
        if (ReadValue() is { } current && current != Command)
        {
            TrySetEnabled(true);
        }
    }

    private string? ReadValue()
    {
        using var key = Registry.CurrentUser.OpenSubKey(keyPath);
        return key?.GetValue(valueName) as string;
    }
}
