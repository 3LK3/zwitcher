using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.WindowsAndMessaging;
using Zwitcher.Core.Windows;

namespace Zwitcher.Core.Interop;

/// <summary>Reads window properties from Win32. A window that has closed reads as empty.</summary>
public sealed class Win32WindowQuery : IWindowQuery
{
    public IReadOnlyList<nint> EnumerateTopLevel()
    {
        var windows = new List<nint>();
        PInvoke.EnumWindows(
            (window, _) =>
            {
                windows.Add(window);
                return true;
            },
            0
        );
        return windows;
    }

    public nint GetForegroundWindow() => PInvoke.GetForegroundWindow();

    public bool IsTopLevel(nint window) =>
        PInvoke.GetAncestor((HWND)window, GET_ANCESTOR_FLAGS.GA_ROOT) == (HWND)window;

    public bool IsVisible(nint window) => PInvoke.IsWindowVisible((HWND)window);

    public unsafe bool IsCloaked(nint window)
    {
        uint cloaked;
        var result = PInvoke.DwmGetWindowAttribute(
            (HWND)window,
            DWMWINDOWATTRIBUTE.DWMWA_CLOAKED,
            &cloaked,
            sizeof(uint)
        );
        return result.Succeeded && cloaked != 0;
    }

    public bool IsShellWindow(nint window) => PInvoke.GetShellWindow() == (HWND)window;

    public WindowExStyle GetExStyle(nint window) =>
        (WindowExStyle)PInvoke.GetWindowLong((HWND)window, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);

    public nint GetRootOwner(nint window) =>
        PInvoke.GetAncestor((HWND)window, GET_ANCESTOR_FLAGS.GA_ROOTOWNER);

    public nint GetLastActivePopup(nint window) => PInvoke.GetLastActivePopup((HWND)window);

    public string GetTitle(nint window)
    {
        // For a top-level window of another process these read the cached title without
        // sending a message, so a hung app can't block us.
        // https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-getwindowtextw
        var length = PInvoke.GetWindowTextLength((HWND)window);
        if (length == 0)
        {
            return "";
        }

        Span<char> buffer = new char[length + 1];
        var copied = PInvoke.GetWindowText((HWND)window, buffer);
        return new string(buffer[..copied]);
    }

    public unsafe int GetProcessId(nint window)
    {
        uint processId;
        PInvoke.GetWindowThreadProcessId((HWND)window, &processId);
        return (int)processId;
    }
}
