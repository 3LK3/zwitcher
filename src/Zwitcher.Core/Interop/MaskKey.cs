using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Zwitcher.Core.Interop;

/// <summary>
/// Injects a tap of an unassigned key, for two effects we measured but Windows doesn't document:
/// - While Alt is held, the foreground app no longer sees a lone Alt, which opens its menu.
/// - Our process counts as having input, which a keyboard hook alone doesn't achieve. Without
///   that, SetForegroundWindow fails. See IWindowActivator in ARCHITECTURE.md.
/// Must not be called from the hook callback.
/// </summary>
public static class MaskKey
{
    private const VIRTUAL_KEY Unassigned = (VIRTUAL_KEY)0xE8;

    public static unsafe void Tap()
    {
        Span<INPUT> inputs = stackalloc INPUT[2];
        inputs[0] = Key(0);
        inputs[1] = Key(KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP);
        PInvoke.SendInput(inputs, sizeof(INPUT));
    }

    private static INPUT Key(KEYBD_EVENT_FLAGS flags) =>
        new()
        {
            type = INPUT_TYPE.INPUT_KEYBOARD,
            Anonymous = new INPUT._Anonymous_e__Union
            {
                ki = new KEYBDINPUT { wVk = Unassigned, dwFlags = flags },
            },
        };
}
