using System.Diagnostics;
using Zwitcher.Core;
using Zwitcher.Core.Windows;

namespace Zwitcher.App.Debugging;

/// <summary>Temporary stand-in that logs what the real overlay will render. M3 replaces it.</summary>
public sealed class LoggingOverlayView : IOverlayView
{
    // Never raised: this view has nothing to click.
    public event Action<int>? ItemClicked
    {
        add { }
        remove { }
    }

    public void Show(IReadOnlyList<WindowInfo> windows, int selectedIndex) =>
        Debug.WriteLine($"Show({windows.Count} windows, selected={selectedIndex})");

    public void UpdateSelection(int selectedIndex) =>
        Debug.WriteLine($"UpdateSelection({selectedIndex})");

    public void UpdateWindows(IReadOnlyList<WindowInfo> windows, int selectedIndex) =>
        Debug.WriteLine($"UpdateWindows({windows.Count} windows, selected={selectedIndex})");

    public void Hide() => Debug.WriteLine("Hide()");
}
