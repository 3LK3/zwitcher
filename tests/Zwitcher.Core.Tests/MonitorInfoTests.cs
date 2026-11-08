using Zwitcher.Core.Screen;

namespace Zwitcher.Core.Tests;

public sealed class MonitorInfoTests
{
    private static MonitorInfo WithWorkArea(int left, int top, int width, int height) =>
        new(
            new PixelRect(left, top, left + width, top + height),
            new PixelRect(left, top, left + width, top + height),
            96
        );

    [Fact]
    public void Center_on_the_primary_monitor()
    {
        var monitor = WithWorkArea(0, 0, 1920, 1040);

        Assert.Equal((720, 420), monitor.Center(480, 200));
    }

    [Fact]
    public void Center_on_a_monitor_with_negative_coordinates()
    {
        var monitor = WithWorkArea(-1920, -200, 1920, 1080);

        Assert.Equal((-1200, 240), monitor.Center(480, 200));
    }

    [Fact]
    public void Size_larger_than_the_work_area_starts_at_the_edge()
    {
        var monitor = WithWorkArea(100, 50, 800, 600);

        Assert.Equal((100, 50), monitor.Center(1000, 700));
    }

    [Fact]
    public void Center_uses_the_work_area_not_the_monitor_bounds()
    {
        var monitor = new MonitorInfo(
            new PixelRect(0, 0, 1920, 1080),
            new PixelRect(0, 0, 1920, 1040),
            96
        );

        Assert.Equal((720, 420), monitor.Center(480, 200));
    }
}
