using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.WindowsAndMessaging;
using Zwitcher.Core.WindowList;

namespace Zwitcher.Core.Screen;

/// <summary>
/// Win32 calls on our own windows (placement and styling), so that the App project stays free
/// of them.
/// </summary>
public static class Win32Window
{
    /// <summary>Keeps a window out of Alt+Tab, including our own list.</summary>
    public static void HideFromAltTab(nint window)
    {
        var style = PInvoke.GetWindowLong((HWND)window, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        PInvoke.SetWindowLong(
            (HWND)window,
            WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE,
            style | (int)WindowExStyle.ToolWindow
        );
    }

    /// <summary>Moves without resizing, activating or changing the z-order.</summary>
    public static void Move(nint window, int x, int y) =>
        PInvoke.SetWindowPos(
            (HWND)window,
            HWND.Null,
            x,
            y,
            0,
            0,
            SET_WINDOW_POS_FLAGS.SWP_NOSIZE
                | SET_WINDOW_POS_FLAGS.SWP_NOZORDER
                | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE
        );

    /// <summary>Sets position and size in physical pixels, without activating.</summary>
    public static void Place(nint window, int x, int y, int width, int height) =>
        PInvoke.SetWindowPos(
            (HWND)window,
            HWND.Null,
            x,
            y,
            width,
            height,
            SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE
        );

    /// <summary>
    /// Asks DWM for rounded corners, like system flyouts. Windows 11 only; Windows 10 rejects
    /// the attribute and keeps square corners, which is fine.
    /// https://learn.microsoft.com/windows/apps/desktop/modernize/ui/apply-rounded-corners
    /// </summary>
    public static unsafe void RoundCorners(nint window)
    {
        var preference = DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND;
#pragma warning disable CA1416
        PInvoke.DwmSetWindowAttribute(
            (HWND)window,
            DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE,
            &preference,
            (uint)sizeof(DWM_WINDOW_CORNER_PREFERENCE)
        );
#pragma warning restore CA1416
    }
}
