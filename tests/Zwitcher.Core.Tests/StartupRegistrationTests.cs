using Microsoft.Win32;

namespace Zwitcher.Core.Tests;

/// <summary>Uses a throwaway key, so that these tests can't touch the real Run key.</summary>
public sealed class StartupRegistrationTests : IDisposable
{
    private const string ValueName = "Test";
    private const string Executable = @"C:\Program Files\Zwitcher\Zwitcher.App.exe";

    private readonly string _key = $@"Software\Zwitcher-Tests\{Guid.NewGuid()}";

    public void Dispose()
    {
        Registry.CurrentUser.DeleteSubKeyTree(
            @"Software\Zwitcher-Tests",
            throwOnMissingSubKey: false
        );
    }

    private StartupRegistration Registration(string executable = Executable) =>
        new(_key, ValueName, executable);

    private string? Value()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_key);
        return key?.GetValue(ValueName) as string;
    }

    [Fact]
    public void Is_disabled_without_an_entry()
    {
        Assert.False(Registration().IsEnabled);
    }

    [Fact]
    public void Enabling_writes_the_quoted_path()
    {
        Assert.True(Registration().TrySetEnabled(true));

        Assert.True(Registration().IsEnabled);
        Assert.Equal($"\"{Executable}\"", Value());
    }

    [Fact]
    public void Disabling_removes_the_entry()
    {
        var registration = Registration();
        registration.TrySetEnabled(true);

        Assert.True(registration.TrySetEnabled(false));

        Assert.False(registration.IsEnabled);
    }

    [Fact]
    public void Disabling_without_an_entry_succeeds()
    {
        Assert.True(Registration().TrySetEnabled(false));
    }

    [Fact]
    public void RepairPath_follows_a_moved_exe()
    {
        Registration(@"C:\Old\Zwitcher.App.exe").TrySetEnabled(true);

        Registration().RepairPath();

        Assert.Equal($"\"{Executable}\"", Value());
    }

    [Fact]
    public void RepairPath_does_not_create_an_entry()
    {
        Registration().RepairPath();

        Assert.Null(Value());
    }
}
