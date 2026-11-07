using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Zwitcher.Core.Interop;

[Flags]
public enum HotkeyModifiers : uint
{
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,

    /// <summary>MOD_NOREPEAT: holding the key doesn't send repeated hotkey messages.</summary>
    NoRepeat = 0x4000,
}

/// <summary>
/// A system-wide hotkey that sends WM_HOTKEY to a window. Temporary: the keyboard hook replaces
/// it in M5. Must be created on the thread that owns the window.
/// </summary>
public sealed class GlobalHotkey : IDisposable
{
    public const int WmHotkey = (int)PInvoke.WM_HOTKEY;

    private readonly HWND _window;
    private readonly int _id;

    public GlobalHotkey(nint window, int id, HotkeyModifiers modifiers, uint virtualKey)
    {
        _window = (HWND)window;
        _id = id;
        IsRegistered = PInvoke.RegisterHotKey(
            _window,
            id,
            (HOT_KEY_MODIFIERS)modifiers,
            virtualKey
        );
    }

    /// <summary>False if another application already owns this combination.</summary>
    public bool IsRegistered { get; }

    public void Dispose()
    {
        if (IsRegistered)
        {
            PInvoke.UnregisterHotKey(_window, _id);
        }
    }
}
