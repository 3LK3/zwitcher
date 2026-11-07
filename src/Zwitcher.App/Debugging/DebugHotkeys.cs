using System.Diagnostics;
using Zwitcher.Core;
using Zwitcher.Core.Input;
using Zwitcher.Core.Interop;

namespace Zwitcher.App.Debugging;

/// <summary>
/// Temporary stand-in for the keyboard hook, so the overlay can be tried without touching
/// Alt+Tab. Ctrl+Alt+Space opens it and moves the selection, Ctrl+Alt+Shift+Space cancels.
/// M5 removes this.
/// </summary>
public sealed class DebugHotkeys : IDisposable
{
    private const int NextId = 1;
    private const int CancelId = 2;
    private const uint VkSpace = 0x20;

    private readonly CommandPoster _poster;
    private readonly ISwitcherState _state;
    private readonly GlobalHotkey _next;
    private readonly GlobalHotkey _cancel;

    public DebugHotkeys(MessageWindow window, CommandPoster poster, ISwitcherState state)
    {
        _poster = poster;
        _state = state;

        const HotkeyModifiers modifiers =
            HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.NoRepeat;
        _next = new GlobalHotkey(window.Handle, NextId, modifiers, VkSpace);
        _cancel = new GlobalHotkey(
            window.Handle,
            CancelId,
            modifiers | HotkeyModifiers.Shift,
            VkSpace
        );
        Debug.WriteLine(
            $"Hotkeys registered: next={_next.IsRegistered} cancel={_cancel.IsRegistered}"
        );

        window.HotkeyPressed += OnHotkeyPressed;
    }

    public void Dispose()
    {
        _next.Dispose();
        _cancel.Dispose();
    }

    private void OnHotkeyPressed(int id)
    {
        if (id == CancelId)
        {
            _poster.Post(SwitcherCommand.Cancel);
        }
        else if (_state.IsActive)
        {
            _poster.Post(SwitcherCommand.Next);
        }
        else
        {
            // The same order as the hook: set the flag, then post.
            _state.IsActive = true;
            _poster.Post(SwitcherCommand.Activate);
        }
    }
}
