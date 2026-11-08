using Zwitcher.Core.WindowList;

namespace Zwitcher.Core.Tests;

public sealed class WindowFilterTests
{
    private const nint Owner = 1;
    private const nint Owned = 2;
    private const nint Other = 3;

    private static bool IsEligible(nint window, params FakeWindow[] windows) =>
        new WindowFilter(new FakeWindowQuery(windows)).IsEligible(window);

    [Fact]
    public void Normal_window_is_included()
    {
        Assert.True(IsEligible(Owner, new FakeWindow(Owner)));
    }

    [Fact]
    public void Hidden_window_is_excluded()
    {
        Assert.False(IsEligible(Owner, new FakeWindow(Owner) { Visible = false }));
    }

    [Fact]
    public void Cloaked_window_is_excluded()
    {
        Assert.False(IsEligible(Owner, new FakeWindow(Owner) { Cloaked = true }));
    }

    [Fact]
    public void Cloaked_app_window_is_excluded()
    {
        var window = new FakeWindow(Owner) { Cloaked = true, ExStyle = WindowExStyle.AppWindow };

        Assert.False(IsEligible(Owner, window));
    }

    [Fact]
    public void Window_without_a_title_is_excluded()
    {
        Assert.False(IsEligible(Owner, new FakeWindow(Owner) { Title = "" }));
    }

    [Fact]
    public void Shell_window_is_excluded()
    {
        Assert.False(IsEligible(Owner, new FakeWindow(Owner) { Shell = true }));
    }

    [Theory]
    [InlineData(WindowExStyle.ToolWindow)]
    [InlineData(WindowExStyle.ToolWindow | WindowExStyle.AppWindow)]
    public void Tool_window_is_excluded(WindowExStyle exStyle)
    {
        Assert.False(IsEligible(Owner, new FakeWindow(Owner) { ExStyle = exStyle }));
    }

    [Fact]
    public void Visible_owner_represents_its_owned_window()
    {
        FakeWindow[] windows =
        [
            new(Owner) { LastActivePopup = Owned },
            new(Owned) { RootOwner = Owner },
        ];

        Assert.True(IsEligible(Owner, windows));
        Assert.False(IsEligible(Owned, windows));
    }

    [Fact]
    public void Hidden_owner_is_represented_by_its_visible_owned_window()
    {
        FakeWindow[] windows =
        [
            new(Owner) { Visible = false, LastActivePopup = Owned },
            new(Owned) { RootOwner = Owner },
        ];

        Assert.True(IsEligible(Owned, windows));
        Assert.False(IsEligible(Owner, windows));
    }

    [Fact]
    public void Owned_tool_window_behind_a_hidden_owner_is_excluded()
    {
        FakeWindow[] windows =
        [
            new(Owner) { Visible = false, LastActivePopup = Owned },
            new(Owned) { RootOwner = Owner, ExStyle = WindowExStyle.ToolWindow },
        ];

        Assert.False(IsEligible(Owned, windows));
    }

    [Fact]
    public void Tool_window_owner_counts_as_invisible()
    {
        FakeWindow[] windows =
        [
            new(Owner) { ExStyle = WindowExStyle.ToolWindow, LastActivePopup = Owned },
            new(Owned) { RootOwner = Owner },
        ];

        Assert.True(IsEligible(Owned, windows));
    }

    [Fact]
    public void Owned_app_window_is_included_alongside_its_owner()
    {
        FakeWindow[] windows =
        [
            new(Owner) { LastActivePopup = Owned },
            new(Owned) { RootOwner = Owner, ExStyle = WindowExStyle.AppWindow },
        ];

        Assert.True(IsEligible(Owner, windows));
        Assert.True(IsEligible(Owned, windows));
    }

    [Fact]
    public void Walk_ending_on_a_hidden_window_excludes_the_window()
    {
        FakeWindow[] windows = [new(Owner) { Visible = false }, new(Owned) { RootOwner = Owner }];

        Assert.False(IsEligible(Owned, windows));
    }

    [Fact]
    public void Popup_cycle_terminates_and_excludes_the_window()
    {
        FakeWindow[] windows =
        [
            new(Owner) { Visible = false, LastActivePopup = Other },
            new(Other) { Visible = false, LastActivePopup = Owner },
            new(Owned) { RootOwner = Owner },
        ];

        Assert.False(IsEligible(Owned, windows));
    }
}
