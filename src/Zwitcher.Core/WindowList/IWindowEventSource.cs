namespace Zwitcher.Core.WindowList;

/// <summary>Reports what happens to windows, so that the tracker can be tested with fake events.</summary>
public interface IWindowEventSource
{
    event Action<WindowEventKind, nint>? WindowEvent;
}
