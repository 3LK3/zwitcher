using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.HiDpi;
using Windows.Win32.UI.WindowsAndMessaging;
using Zwitcher.Core.Windows;

namespace Zwitcher.Core.Interop;

/// <summary>The Win32 calls the overlay needs, so that the App project stays free of them.</summary>
public static class OverlayNative
{
    /// <summary>Keeps a window out of Alt+Tab, including our own list.</summary>
    public static void MakeToolWindow(nint window)
    {
        var style = PInvoke.GetWindowLong((HWND)window, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        PInvoke.SetWindowLong(
            (HWND)window,
            WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE,
            style | (int)WindowExStyle.ToolWindow
        );
    }

    /// <summary>The monitor for the overlay. Falls back to the primary one.</summary>
    public static MonitorArea GetMonitor(ShowOn showOn) =>
        Describe(
            showOn switch
            {
                ShowOn.CursorMonitor => FromCursor(),
                ShowOn.ForegroundMonitor => PInvoke.MonitorFromWindow(
                    PInvoke.GetForegroundWindow(),
                    MONITOR_FROM_FLAGS.MONITOR_DEFAULTTOPRIMARY
                ),
                _ => Primary(),
            }
        );

    /// <summary>The primary monitor by definition has its origin at (0, 0).</summary>
    private static HMONITOR Primary() =>
        PInvoke.MonitorFromPoint(
            new System.Drawing.Point(0, 0),
            MONITOR_FROM_FLAGS.MONITOR_DEFAULTTOPRIMARY
        );

    private static HMONITOR FromCursor() =>
        PInvoke.GetCursorPos(out var cursor)
            ? PInvoke.MonitorFromPoint(cursor, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTOPRIMARY)
            : Primary();

    private static unsafe MonitorArea Describe(HMONITOR monitor)
    {
        var info = new MONITORINFO { cbSize = (uint)sizeof(MONITORINFO) };
        PInvoke.GetMonitorInfo(monitor, ref info);
        // The analyzer assumes Windows 7, the default of net10.0-windows. We need Windows 10.
#pragma warning disable CA1416
        PInvoke.GetDpiForMonitor(monitor, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out var dpi, out _);
#pragma warning restore CA1416

        var area = info.rcWork;
        return new MonitorArea(
            area.left,
            area.top,
            area.right - area.left,
            area.bottom - area.top,
            dpi
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
}
