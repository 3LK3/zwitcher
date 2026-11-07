using Zwitcher.Core.Windows;

namespace Zwitcher.Core.Tests;

internal sealed class FakeWindowEvents : IWindowEventSource
{
    public event Action<WindowEventKind, nint>? WindowEvent;

    public void Raise(WindowEventKind kind, nint window) => WindowEvent?.Invoke(kind, window);
}
