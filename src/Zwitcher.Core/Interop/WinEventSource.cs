using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;
using Zwitcher.Core.Windows;

namespace Zwitcher.Core.Interop;

/// <summary>
/// Reports window events through SetWinEventHook. Create it on the UI thread: out-of-context
/// events arrive on the calling thread, through its message loop.
/// https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setwineventhook
/// </summary>
public sealed class WinEventSource : IWindowEventSource, IDisposable
{
    // https://learn.microsoft.com/windows/win32/winauto/event-constants
    private const uint EventForeground = 0x0003;
    private const uint EventMinimizeStart = 0x0016;
    private const uint EventObjectDestroy = 0x8001;
    private const uint EventObjectShow = 0x8002;
    private const uint EventObjectHide = 0x8003;
    private const uint EventObjectNameChange = 0x800C;
    private const uint EventObjectCloaked = 0x8017;
    private const uint EventObjectUncloaked = 0x8018;

    private const int ObjectIdWindow = 0;
    private const int ChildIdSelf = 0;

    // Without skipping our own process, showing the overlay would itself report a change.
    private const uint Flags = 0x0000 | 0x0002; // WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS

    // Held in a field, or the GC would collect the delegate while native code still calls it.
    private readonly WINEVENTPROC _callback;
    private readonly List<IDisposable> _hooks = [];

    public WinEventSource()
    {
        _callback = Callback;

        // Separate ranges, because one wide range would include EVENT_OBJECT_LOCATIONCHANGE,
        // which fires constantly.
        Hook(EventForeground, EventForeground);
        Hook(EventMinimizeStart, EventMinimizeStart);
        Hook(EventObjectDestroy, EventObjectHide);
        Hook(EventObjectNameChange, EventObjectNameChange);
        Hook(EventObjectCloaked, EventObjectUncloaked);
    }

    public event Action<WindowEventKind, nint>? WindowEvent;

    public void Dispose()
    {
        foreach (var hook in _hooks)
        {
            hook.Dispose();
        }

        _hooks.Clear();
    }

    private void Hook(uint first, uint last) =>
        _hooks.Add(PInvoke.SetWinEventHook(first, last, null, _callback, 0, 0, Flags));

    private void Callback(
        HWINEVENTHOOK hook,
        uint eventId,
        HWND window,
        int objectId,
        int childId,
        uint eventThread,
        uint eventTime
    )
    {
        // These events also fire for carets, cursors and controls inside a window.
        if (window.IsNull || objectId != ObjectIdWindow || childId != ChildIdSelf)
        {
            return;
        }

        var kind = eventId switch
        {
            EventForeground => WindowEventKind.Foreground,
            EventMinimizeStart => WindowEventKind.MinimizeStart,
            EventObjectDestroy => WindowEventKind.Destroyed,
            _ => WindowEventKind.Changed,
        };
        WindowEvent?.Invoke(kind, window);
    }
}
