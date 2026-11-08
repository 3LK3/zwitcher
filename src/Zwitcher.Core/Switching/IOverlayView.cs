using Zwitcher.Core.WindowList;

namespace Zwitcher.Core.Switching;

/// <summary>The overlay as the controller sees it. The view only renders and reports clicks.</summary>
public interface IOverlayView
{
    event Action<int>? ItemClicked;

    void Show(IReadOnlyList<WindowInfo> windows, int selectedIndex);

    void UpdateSelection(int selectedIndex);

    void UpdateWindows(IReadOnlyList<WindowInfo> windows, int selectedIndex);

    void Hide();
}
