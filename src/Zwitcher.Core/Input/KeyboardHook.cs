using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;
using Zwitcher.Core.Interop;

namespace Zwitcher.Core.Input;

/// <summary>
/// The low-level keyboard hook, on its own thread with its own message loop. The callback runs
/// on every keystroke system-wide: if it's slow, typing lags for the whole OS and Windows
/// silently removes the hook. So it only applies HookDecisions and posts the result.
/// https://learn.microsoft.com/windows/win32/winmsg/lowlevelkeyboardproc
/// </summary>
public sealed class KeyboardHook : IDisposable
{
    private const int JoinTimeoutMilliseconds = 1000;
    private const uint LlkhfAltDown = 0x20;
    private const uint LlkhfUp = 0x80;

    private readonly CommandPoster _poster;
    private readonly ISwitcherState _state;

    // Held in a field, or the GC would collect the delegate while native code still calls it.
    private readonly HOOKPROC _callback;
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _started = new();

    private uint _threadId;
    private volatile bool _enabled = true;

    public KeyboardHook(CommandPoster poster, ISwitcherState state)
    {
        _poster = poster;
        _state = state;
        _callback = Callback;

        // Above normal, so that a busy machine doesn't delay every keystroke.
        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "Keyboard hook",
            Priority = ThreadPriority.AboveNormal,
        };
    }

    /// <summary>False if Windows refused the hook. The native Alt+Tab then keeps working.</summary>
    public bool IsInstalled { get; private set; }

    /// <summary>When false, every key passes through, so the native Alt+Tab works.</summary>
    public bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    public void Start()
    {
        _thread.Start();
        _started.Wait();
    }

    public void Dispose()
    {
        if (_thread.IsAlive)
        {
            PInvoke.PostThreadMessage(_threadId, PInvoke.WM_QUIT, 0, 0);

            // A crash handler can run on the hook thread itself, which can't wait for itself.
            if (Thread.CurrentThread != _thread)
            {
                _thread.Join(JoinTimeoutMilliseconds);
            }
        }

        _started.Dispose();
    }

    private void Run()
    {
        _threadId = PInvoke.GetCurrentThreadId();

        // Compiles the decision code now, so that the first real key isn't delayed by the JIT.
        HookDecisions.Decide(0, false, false, false, false);

        using var module = PInvoke.GetModuleHandle((string?)null);
        using var hook = PInvoke.SetWindowsHookEx(
            WINDOWS_HOOK_ID.WH_KEYBOARD_LL,
            _callback,
            module,
            0
        );
        IsInstalled = !hook.IsInvalid;
        _started.Set();

        if (IsInstalled)
        {
            while (PInvoke.GetMessage(out _, HWND.Null, 0, 0) > 0) { }
        }
    }

    private unsafe LRESULT Callback(int code, WPARAM wParam, LPARAM lParam)
    {
        if (code >= 0 && _enabled)
        {
            var key = (KBDLLHOOKSTRUCT*)lParam.Value;
            var flags = (uint)key->flags;

            // Alt comes from the event and Shift from the system, rather than from tracked
            // state. We miss keys while an elevated window has focus, and a stale "Alt is down"
            // would turn a plain Tab into a switcher command.
            var decision = HookDecisions.Decide(
                key->vkCode,
                (flags & LlkhfUp) != 0,
                (flags & LlkhfAltDown) != 0,
                PInvoke.GetAsyncKeyState((int)VIRTUAL_KEY.VK_SHIFT) < 0,
                _state.IsActive
            );

            if (decision.Command is { } command)
            {
                if (command is SwitcherCommand.Activate or SwitcherCommand.ActivatePrev)
                {
                    _state.IsActive = true;
                }

                _poster.Post(command);
            }

            if (decision.Swallow)
            {
                return (LRESULT)1;
            }
        }

        return PInvoke.CallNextHookEx(null, code, wParam, lParam);
    }
}
