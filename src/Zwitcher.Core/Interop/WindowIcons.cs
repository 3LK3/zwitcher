using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Zwitcher.Core.Interop;

/// <summary>Finds a window's icon. The handle belongs to that window: copy it, never destroy it.</summary>
public static class WindowIcons
{
    // https://learn.microsoft.com/windows/win32/winmsg/wm-geticon
    private const nuint IconSmall = 0;
    private const nuint IconBig = 1;
    private const nuint IconSmall2 = 2;

    // WM_GETICON runs in the other process. Long enough for a healthy app, short enough that
    // a busy one can't stall key handling on the UI thread.
    private const uint TimeoutMilliseconds = 20;

    /// <summary>Returns the icon handle, or 0 if the window has none.</summary>
    public static nint GetIconHandle(nint window)
    {
        var hwnd = (HWND)window;

        var icon = FromMessage(hwnd, IconBig);
        if (icon == 0)
        {
            icon = FromMessage(hwnd, IconSmall2);
        }

        if (icon == 0)
        {
            icon = FromMessage(hwnd, IconSmall);
        }

        if (icon == 0)
        {
            icon = FromClass(hwnd, GET_CLASS_LONG_INDEX.GCL_HICON);
        }

        if (icon == 0)
        {
            icon = FromClass(hwnd, GET_CLASS_LONG_INDEX.GCL_HICONSM);
        }

        return icon;
    }

    // GetClassLongPtr isn't available to an AnyCPU build. The 32-bit call is enough, because
    // USER handles only use the low 32 bits, sign-extended on 64-bit Windows.
    // https://learn.microsoft.com/windows/win32/winprog64/interprocess-communication
    private static nint FromClass(HWND window, GET_CLASS_LONG_INDEX index) =>
        unchecked((int)PInvoke.GetClassLong(window, index));

    private static unsafe nint FromMessage(HWND window, nuint iconType)
    {
        nuint icon = 0;
        var sent = PInvoke.SendMessageTimeout(
            window,
            PInvoke.WM_GETICON,
            iconType,
            0,
            SEND_MESSAGE_TIMEOUT_FLAGS.SMTO_ABORTIFHUNG,
            TimeoutMilliseconds,
            &icon
        );
        return sent == 0 ? 0 : (nint)icon;
    }
}
