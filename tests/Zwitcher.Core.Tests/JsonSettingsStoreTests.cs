namespace Zwitcher.Core.Tests;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"Zwitcher-Tests-{Guid.NewGuid()}"
    );

    private string SettingsPath => Path.Combine(_directory, "nested", "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private void WriteFile(string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, content);
    }

    [Fact]
    public void Saved_settings_come_back()
    {
        var store = new JsonSettingsStore(SettingsPath);
        var settings = new Settings
        {
            Enabled = false,
            ShowOn = ShowOn.CursorMonitor,
            Layout = OverlayLayout.Grid,
        };

        store.Save(settings);

        Assert.Equal(settings, store.Load());
    }

    [Fact]
    public void Save_leaves_no_temporary_file()
    {
        new JsonSettingsStore(SettingsPath).Save(new Settings());

        Assert.Equal([SettingsPath], Directory.GetFiles(Path.GetDirectoryName(SettingsPath)!));
    }

    [Fact]
    public void Save_replaces_earlier_settings()
    {
        var store = new JsonSettingsStore(SettingsPath);
        store.Save(new Settings { Layout = OverlayLayout.Grid });

        store.Save(new Settings { Layout = OverlayLayout.List });

        Assert.Equal(OverlayLayout.List, store.Load().Layout);
    }

    [Fact]
    public void Enums_are_stored_as_names()
    {
        new JsonSettingsStore(SettingsPath).Save(new Settings { Layout = OverlayLayout.Grid });

        Assert.Contains("\"Grid\"", File.ReadAllText(SettingsPath));
    }

    [Fact]
    public void Missing_file_gives_the_defaults()
    {
        Assert.Equal(new Settings(), new JsonSettingsStore(SettingsPath).Load());
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("""{ "Layout": "Spiral" }""")]
    [InlineData("""{ "Layout": 1 }""")]
    public void Damaged_file_gives_the_defaults(string content)
    {
        WriteFile(content);

        Assert.Equal(new Settings(), new JsonSettingsStore(SettingsPath).Load());
    }

    [Fact]
    public void Missing_property_keeps_its_default()
    {
        WriteFile("""{ "Layout": "Grid" }""");

        var settings = new JsonSettingsStore(SettingsPath).Load();

        Assert.Equal(new Settings { Layout = OverlayLayout.Grid }, settings);
    }

    [Fact]
    public void Unknown_property_is_ignored()
    {
        WriteFile("""{ "Layout": "Grid", "FromTheFuture": true }""");

        Assert.Equal(OverlayLayout.Grid, new JsonSettingsStore(SettingsPath).Load().Layout);
    }
}
