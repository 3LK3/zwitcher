using Windows.Win32;
using Windows.Win32.Foundation;
using Zwitcher.Core.Switching;

namespace Zwitcher.Core.Input;

/// <summary>
/// Posts commands to the message window. Safe to call from the hook thread: PostMessage
/// doesn't block, allocates nothing and keeps the order of the commands.
/// </summary>
public sealed class CommandPoster(nint messageWindow)
{
    public const int WmSwitcherCommand = (int)PInvoke.WM_APP + 1;

    private readonly HWND _messageWindow = (HWND)messageWindow;

    public void Post(SwitcherCommand command)
    {
        // A failed post (full queue, window gone) is ignored on purpose: this runs in the
        // keyboard hook, which must never throw. The worst case is one lost key press.
        PInvoke.PostMessage(_messageWindow, WmSwitcherCommand, (nuint)command, 0);
    }
}
