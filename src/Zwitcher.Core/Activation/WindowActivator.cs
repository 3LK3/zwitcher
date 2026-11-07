using System.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Zwitcher.Core.Activation;

/// <summary>Brings a window to the foreground. A window that has closed just fails quietly.</summary>
public sealed class WindowActivator : IWindowActivator
{
    public void Activate(nint handle)
    {
        var window = (HWND)handle;

        // The async variant only posts the request, so a hung window can't block the UI thread.
        if (PInvoke.IsIconic(window))
        {
            PInvoke.ShowWindowAsync(window, SHOW_WINDOW_CMD.SW_RESTORE);
        }

        // The list has one entry per owner group. If a dialog is open, its owner is disabled,
        // so focus has to go to the dialog.
        var popup = PInvoke.GetLastActivePopup(window);
        var target = PInvoke.IsWindowVisible(popup) ? popup : window;

        if (!PInvoke.SetForegroundWindow(target))
        {
            Debug.WriteLine($"SetForegroundWindow failed for {handle}");
        }
    }
}
