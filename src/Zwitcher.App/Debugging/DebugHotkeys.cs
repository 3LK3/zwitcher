using System.Diagnostics;
using Zwitcher.Core;
using Zwitcher.Core.Input;
using Zwitcher.Core.Interop;

namespace Zwitcher.App.Debugging;

/// <summary>
/// Temporary stand-in for the keyboard hook, so the overlay can be tried without touching
/// Alt+Tab. Ctrl+Alt+Space opens it and moves the selection. While it's open, Enter confirms
/// and Esc cancels. M5 removes this.
/// </summary>
public sealed class DebugHotkeys : IDisposable
{
    private const int NextId = 1;
    private const int ConfirmId = 2;
    private const int CancelId = 3;
    private const uint VkReturn = 0x0D;
    private const uint VkEscape = 0x1B;
    private const uint VkSpace = 0x20;

    private readonly MessageWindow _window;
    private readonly CommandPoster _poster;
    private readonly ISwitcherState _state;
    private readonly GlobalHotkey _next;

    private GlobalHotkey? _confirm;
    private GlobalHotkey? _cancel;

    public DebugHotkeys(MessageWindow window, CommandPoster poster, ISwitcherState state)
    {
        _window = window;
        _poster = poster;
        _state = state;

        _next = new GlobalHotkey(
            window.Handle,
            NextId,
            HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.NoRepeat,
            VkSpace
        );
        Debug.WriteLine($"Hotkey registered: {_next.IsRegistered}");

        window.HotkeyPressed += OnHotkeyPressed;
    }

    /// <summary>
    /// Enter and Esc must only be held while the overlay is open. A hotkey takes the key away
    /// from every other application.
    /// </summary>
    public void SetOverlayOpen(bool isOpen)
    {
        ReleaseOverlayKeys();
        if (isOpen)
        {
            var handle = _window.Handle;
            _confirm = new GlobalHotkey(handle, ConfirmId, HotkeyModifiers.NoRepeat, VkReturn);
            _cancel = new GlobalHotkey(handle, CancelId, HotkeyModifiers.NoRepeat, VkEscape);
        }
    }

    public void Dispose()
    {
        ReleaseOverlayKeys();
        _next.Dispose();
    }

    private void ReleaseOverlayKeys()
    {
        _confirm?.Dispose();
        _cancel?.Dispose();
        _confirm = null;
        _cancel = null;
    }

    private void OnHotkeyPressed(int id)
    {
        switch (id)
        {
            case ConfirmId:
                _poster.Post(SwitcherCommand.Confirm);
                break;
            case CancelId:
                _poster.Post(SwitcherCommand.Cancel);
                break;
            case NextId when _state.IsActive:
                _poster.Post(SwitcherCommand.Next);
                break;
            case NextId:
                // The same order as the hook: set the flag, then post.
                _state.IsActive = true;
                _poster.Post(SwitcherCommand.Activate);
                break;
        }
    }
}
