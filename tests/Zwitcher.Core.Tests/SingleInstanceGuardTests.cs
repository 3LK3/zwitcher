namespace Zwitcher.Core.Tests;

public sealed class SingleInstanceGuardTests
{
    private static string UniqueName() => $@"Local\Zwitcher-Test-{Guid.NewGuid()}";

    [Fact]
    public void First_holder_is_the_first_instance()
    {
        using var guard = new SingleInstanceGuard(UniqueName());

        Assert.True(guard.IsFirstInstance);
    }

    [Fact]
    public void Second_holder_is_not_the_first_instance()
    {
        var name = UniqueName();
        using var first = new SingleInstanceGuard(name);

        using var second = new SingleInstanceGuard(name);

        Assert.False(second.IsFirstInstance);
    }

    [Fact]
    public void Name_is_free_again_after_dispose()
    {
        var name = UniqueName();
        new SingleInstanceGuard(name).Dispose();

        using var next = new SingleInstanceGuard(name);

        Assert.True(next.IsFirstInstance);
    }
}
