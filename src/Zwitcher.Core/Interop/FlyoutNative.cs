using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.HiDpi;
using Zwitcher.Core.Windows;

namespace Zwitcher.Core.Interop;

/// <summary>A monitor's full bounds, its work area (without the taskbar) and its DPI.</summary>
public sealed record MonitorBounds(PixelRect Monitor, PixelRect Work, uint Dpi);

/// <summary>The Win32 calls the settings flyout needs, so that the App project stays free of them.</summary>
public static class FlyoutNative
{
    /// <summary>The monitor at a screen point, or the nearest one if the point is off-screen.</summary>
    public static unsafe MonitorBounds MonitorAt(int x, int y)
    {
        var monitor = PInvoke.MonitorFromPoint(
            new System.Drawing.Point(x, y),
            MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST
        );
        var info = new MONITORINFO { cbSize = (uint)sizeof(MONITORINFO) };
        PInvoke.GetMonitorInfo(monitor, ref info);
        // The analyzer assumes Windows 7, the default of net10.0-windows. We need Windows 10.
#pragma warning disable CA1416
        PInvoke.GetDpiForMonitor(monitor, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out var dpi, out _);
#pragma warning restore CA1416

        return new MonitorBounds(ToPixelRect(info.rcMonitor), ToPixelRect(info.rcWork), dpi);
    }

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

    private static PixelRect ToPixelRect(RECT rect) =>
        new(rect.left, rect.top, rect.right, rect.bottom);
}
