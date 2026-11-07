using Zwitcher.Core.Windows;

namespace Zwitcher.Core;

/// <summary>The overlay as the controller sees it. The view only renders and reports clicks.</summary>
public interface IOverlayView
{
    event Action<int>? ItemClicked;

    void Show(IReadOnlyList<WindowInfo> windows, int selectedIndex);

    void UpdateSelection(int selectedIndex);

    void UpdateWindows(IReadOnlyList<WindowInfo> windows, int selectedIndex);

    void Hide();
}
