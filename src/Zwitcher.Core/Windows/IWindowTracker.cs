namespace Zwitcher.Core.Windows;

public interface IWindowTracker
{
    /// <summary>Eligible windows, most recently used first.</summary>
    IReadOnlyList<WindowInfo> Snapshot();
}
