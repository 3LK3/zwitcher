using Zwitcher.Core.Windows;

namespace Zwitcher.Core.Tests;

public sealed class FlyoutPlacementTests
{
    private const int Width = 400;
    private const int Height = 300;

    private static readonly PixelRect Monitor = new(0, 0, 1920, 1080);

    [Theory]
    [InlineData(1000)]
    [InlineData(1900)]
    public void Taskbar_at_the_bottom_places_the_flyout_bottom_right_above_it(int iconX)
    {
        var work = Monitor with { Bottom = 1032 };

        var position = FlyoutPlacement.Compute(new(iconX, 1050), Monitor, work, Width, Height);

        Assert.Equal((1920 - Width, 1032 - Height), position);
    }

    [Fact]
    public void Taskbar_at_the_top_places_the_flyout_top_right_below_it()
    {
        var work = Monitor with { Top = 48 };

        var position = FlyoutPlacement.Compute(new(1000, 20), Monitor, work, Width, Height);

        Assert.Equal((1920 - Width, 48), position);
    }

    [Fact]
    public void Taskbar_on_the_left_places_the_flyout_bottom_left_next_to_it()
    {
        var work = Monitor with { Left = 60 };

        var position = FlyoutPlacement.Compute(new(30, 1000), Monitor, work, Width, Height);

        Assert.Equal((60, 1080 - Height), position);
    }

    [Fact]
    public void Taskbar_on_the_right_places_the_flyout_bottom_right_next_to_it()
    {
        var work = Monitor with { Right = 1860 };

        var position = FlyoutPlacement.Compute(new(1890, 1000), Monitor, work, Width, Height);

        Assert.Equal((1860 - Width, 1080 - Height), position);
    }

    [Fact]
    public void Auto_hidden_taskbar_uses_the_edge_nearest_to_the_icon()
    {
        var position = FlyoutPlacement.Compute(new(30, 700), Monitor, Monitor, Width, Height);

        Assert.Equal((0, 1080 - Height), position);
    }

    [Fact]
    public void Auto_hidden_taskbar_at_the_bottom()
    {
        var position = FlyoutPlacement.Compute(new(1700, 1075), Monitor, Monitor, Width, Height);

        Assert.Equal((1920 - Width, 1080 - Height), position);
    }

    [Fact]
    public void Monitor_with_negative_coordinates()
    {
        var monitor = new PixelRect(-1920, -200, 0, 880);
        var work = monitor with { Bottom = 832 };

        var position = FlyoutPlacement.Compute(new(-500, 850), monitor, work, Width, Height);

        Assert.Equal((-Width, 832 - Height), position);
    }

    [Fact]
    public void Flyout_larger_than_the_work_area_starts_at_its_top_left()
    {
        var work = new PixelRect(0, 0, 300, 200);
        var monitor = work with { Bottom = 240 };

        var position = FlyoutPlacement.Compute(new(150, 220), monitor, work, Width, Height);

        Assert.Equal((0, 0), position);
    }
}
