using System.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Zwitcher.Core.Input;

namespace Zwitcher.Core.Switching;

/// <summary>Brings a window to the foreground. A window that has closed just fails quietly.</summary>
public sealed class Win32WindowActivator : IWindowActivator
{
    public void Prepare() => MaskKey.Tap();

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

        // Releasing Alt was input to another process, which ended the right from Prepare.
        MaskKey.Tap();
        if (!PInvoke.SetForegroundWindow(target))
        {
            Trace.TraceWarning($"SetForegroundWindow failed for {handle}");
        }
    }
}
