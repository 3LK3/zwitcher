namespace Zwitcher.Core.Windows;

public interface IWindowTracker
{
    /// <summary>Eligible windows, most recently used first.</summary>
    IReadOnlyList<WindowInfo> Snapshot();

    /// <summary>True if the window, or a window of its owner group, is in the foreground.</summary>
    bool IsForeground(nint window);
}
