using Zwitcher.Core.Windows;

namespace Zwitcher.Core.Tests;

public sealed class MonitorAreaTests
{
    [Fact]
    public void Center_on_the_primary_monitor()
    {
        var area = new MonitorArea(0, 0, 1920, 1040, 96);

        Assert.Equal((720, 420), area.Center(480, 200));
    }

    [Fact]
    public void Center_on_a_monitor_with_negative_coordinates()
    {
        var area = new MonitorArea(-1920, -200, 1920, 1080, 96);

        Assert.Equal((-1200, 240), area.Center(480, 200));
    }

    [Fact]
    public void Size_larger_than_the_area_starts_at_the_edge()
    {
        var area = new MonitorArea(100, 50, 800, 600, 96);

        Assert.Equal((100, 50), area.Center(1000, 700));
    }
}
