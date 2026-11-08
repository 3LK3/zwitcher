using Zwitcher.Core.WindowList;

namespace Zwitcher.Core.Tests;

internal sealed class FakeWindowEventSource : IWindowEventSource
{
    public event Action<WindowEventKind, nint>? WindowEvent;

    public void Raise(WindowEventKind kind, nint window) => WindowEvent?.Invoke(kind, window);
}
