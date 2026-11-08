using Zwitcher.Core.WindowList;

namespace Zwitcher.Core.Tests;

internal sealed record FakeWindow(nint Handle)
{
    public bool Visible { get; init; } = true;
    public bool Cloaked { get; init; }
    public bool Shell { get; init; }
    public bool TopLevel { get; init; } = true;
    public WindowExStyle ExStyle { get; init; }
    public nint? Root { get; init; }
    public nint Owner { get; init; }
    public nint? RootOwner { get; init; }
    public nint? LastActivePopup { get; init; }
    public string Title { get; init; } = "Title";
    public int ProcessId { get; init; } = 1;
}

/// <summary>Serves windows in the order they were added, which stands for z-order.</summary>
internal sealed class FakeWindowQuery(params FakeWindow[] windows) : IWindowQuery
{
    public List<FakeWindow> Windows { get; } = [.. windows];

    public nint Foreground { get; set; }

    private FakeWindow Get(nint window) => Windows.Single(w => w.Handle == window);

    public IReadOnlyList<nint> EnumerateTopLevel() => [.. Windows.Select(w => w.Handle)];

    public nint GetForegroundWindow() => Foreground;

    public bool IsTopLevel(nint window) => Get(window).TopLevel;

    public nint GetRoot(nint window) => Get(window).Root ?? window;

    public nint GetOwner(nint window) => Get(window).Owner;

    public bool IsVisible(nint window) => Get(window).Visible;

    public bool IsCloaked(nint window) => Get(window).Cloaked;

    public bool IsShellWindow(nint window) => Get(window).Shell;

    public WindowExStyle GetExStyle(nint window) => Get(window).ExStyle;

    public nint GetRootOwner(nint window) => Get(window).RootOwner ?? window;

    public nint GetLastActivePopup(nint window) => Get(window).LastActivePopup ?? window;

    public string GetTitle(nint window) => Get(window).Title;

    public int GetProcessId(nint window) => Get(window).ProcessId;
}
