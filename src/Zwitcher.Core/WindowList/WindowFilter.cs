namespace Zwitcher.Core.WindowList;

/// <summary>
/// Decides which windows belong in the switcher, following native Alt+Tab.
/// https://devblogs.microsoft.com/oldnewthing/20071008-00/?p=24863
/// </summary>
public sealed class WindowFilter(IWindowQuery query)
{
    // Real owner chains are short. The cap only stops odd data from looping forever.
    private const int MaxPopupWalk = 32;

    public bool IsEligible(nint window)
    {
        if (!IsTreatedAsVisible(window))
        {
            return false;
        }

        // IsWindowVisible stays true for hidden UWP frames and for windows on other virtual
        // desktops. Only the cloaked state tells them apart.
        if (query.IsCloaked(window))
        {
            return false;
        }

        if (query.IsShellWindow(window) || query.GetTitle(window).Length == 0)
        {
            return false;
        }

        return IsRepresentativeOfOwnerGroup(window);
    }

    /// <summary>A tool window counts as invisible, even if it is visible.</summary>
    private bool IsTreatedAsVisible(nint window) =>
        query.IsVisible(window) && !query.GetExStyle(window).HasFlag(WindowExStyle.ToolWindow);

    /// <summary>
    /// Each group of windows related by ownership gets one entry. Walks from the root owner
    /// down the last active popups to the first visible window, which represents the group.
    /// The article's pseudo-code would drop a visible window with a hidden owner, but its text
    /// wouldn't. We follow the text.
    /// </summary>
    private bool IsRepresentativeOfOwnerGroup(nint window)
    {
        // An app window counts as having no owner, so it represents itself.
        if (query.GetExStyle(window).HasFlag(WindowExStyle.AppWindow))
        {
            return true;
        }

        var current = query.GetRootOwner(window);
        for (var i = 0; i < MaxPopupWalk; i++)
        {
            if (IsTreatedAsVisible(current))
            {
                return current == window;
            }

            var next = query.GetLastActivePopup(current);
            if (next == current)
            {
                return false;
            }

            current = next;
        }

        return false;
    }
}
