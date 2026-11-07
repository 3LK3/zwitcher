using System.Windows.Interop;
using Zwitcher.Core.Input;
using Zwitcher.Core.Interop;

namespace Zwitcher.App;

/// <summary>
/// Hidden message-only window on the UI thread. It turns posted messages into commands, so the
/// Win32 message queue is the only queue between the hook thread and the controller.
/// </summary>
public sealed class MessageWindow : IDisposable
{
    // https://learn.microsoft.com/windows/win32/winmsg/window-features#message-only-windows
    private static readonly nint HwndMessage = -3;

    private readonly Action<SwitcherCommand> _onCommand;
    private readonly HwndSource _source;

    public MessageWindow(Action<SwitcherCommand> onCommand)
    {
        _onCommand = onCommand;

        var parameters = new HwndSourceParameters("Zwitcher.MessageWindow")
        {
            ParentWindow = HwndMessage,
            WindowStyle = 0,
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    /// <summary>Raised with the hotkey id. Temporary: goes away with the debug hotkeys in M5.</summary>
    public event Action<int>? HotkeyPressed;

    public nint Handle => _source.Handle;

    public void Dispose()
    {
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }

    private nint WndProc(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == CommandPoster.WmSwitcherCommand)
        {
            _onCommand((SwitcherCommand)wParam);
            handled = true;
        }
        else if (message == GlobalHotkey.WmHotkey)
        {
            HotkeyPressed?.Invoke((int)wParam);
            handled = true;
        }

        return 0;
    }
}
