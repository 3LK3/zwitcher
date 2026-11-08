using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.HiDpi;
using Zwitcher.Core.Configuration;

namespace Zwitcher.Core.Screen;

/// <summary>Finds monitors and reads their bounds, work area and DPI.</summary>
public static class Monitors
{
    /// <summary>The monitor at a screen point, or the nearest one if the point is off-screen.</summary>
    public static MonitorInfo AtPoint(int x, int y) =>
        Describe(
            PInvoke.MonitorFromPoint(
                new System.Drawing.Point(x, y),
                MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST
            )
        );

    /// <summary>The monitor for the overlay. Falls back to the primary one.</summary>
    public static MonitorInfo For(ShowOnMonitor showOn) =>
        Describe(
            showOn switch
            {
                ShowOnMonitor.Cursor => AtCursor(),
                ShowOnMonitor.Foreground => PInvoke.MonitorFromWindow(
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

    private static HMONITOR AtCursor() =>
        PInvoke.GetCursorPos(out var cursor)
            ? PInvoke.MonitorFromPoint(cursor, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTOPRIMARY)
            : Primary();

    private static unsafe MonitorInfo Describe(HMONITOR monitor)
    {
        var info = new MONITORINFO { cbSize = (uint)sizeof(MONITORINFO) };
        PInvoke.GetMonitorInfo(monitor, ref info);
        // The analyzer assumes Windows 7, the default of net10.0-windows. We need Windows 10.
#pragma warning disable CA1416
        PInvoke.GetDpiForMonitor(monitor, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out var dpi, out _);
#pragma warning restore CA1416

        return new MonitorInfo(ToPixelRect(info.rcMonitor), ToPixelRect(info.rcWork), dpi);
    }

    private static PixelRect ToPixelRect(RECT rect) =>
        new(rect.left, rect.top, rect.right, rect.bottom);
}
