namespace Zwitcher.Core.Tests;

public sealed class SettingsServiceTests
{
    private readonly FakeStore _store = new();

    [Fact]
    public void Current_starts_with_what_the_store_loads()
    {
        _store.Stored = new Settings { Layout = OverlayLayout.Grid };

        Assert.Equal(OverlayLayout.Grid, new SettingsService(_store).Current.Layout);
    }

    [Fact]
    public void Update_saves_and_raises_Changed()
    {
        var service = new SettingsService(_store);
        Settings? raised = null;
        service.Changed += s => raised = s;

        service.Update(s => s with { Enabled = false });

        var expected = new Settings { Enabled = false };
        Assert.Equal(expected, service.Current);
        Assert.Equal(expected, _store.Stored);
        Assert.Equal(expected, raised);
    }

    [Fact]
    public void Update_without_a_change_does_nothing()
    {
        var service = new SettingsService(_store);
        var raised = 0;
        service.Changed += _ => raised++;

        service.Update(s => s with { Enabled = true });

        Assert.Equal(0, raised);
        Assert.Equal(0, _store.SaveCount);
    }

    [Fact]
    public void Failed_save_keeps_the_change_and_still_raises_Changed()
    {
        _store.FailOnSave = true;
        var service = new SettingsService(_store);
        var raised = 0;
        service.Changed += _ => raised++;

        service.Update(s => s with { Layout = OverlayLayout.Grid });

        Assert.Equal(OverlayLayout.Grid, service.Current.Layout);
        Assert.Equal(1, raised);
    }

    private sealed class FakeStore : ISettingsStore
    {
        public Settings Stored { get; set; } = new();

        public bool FailOnSave { get; set; }

        public int SaveCount { get; private set; }

        public Settings Load() => Stored;

        public void Save(Settings settings)
        {
            if (FailOnSave)
            {
                throw new IOException("Disk full");
            }

            SaveCount++;
            Stored = settings;
        }
    }
}
