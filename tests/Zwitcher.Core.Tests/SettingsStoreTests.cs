using Microsoft.Extensions.Logging.Abstractions;
using Zwitcher.Core.Configuration;

namespace Zwitcher.Core.Tests;

public sealed class SettingsStoreTests
{
    private readonly FakeSettingsFile _file = new();

    [Fact]
    public void Current_starts_with_what_the_file_loads()
    {
        _file.Stored = new Settings { Layout = OverlayLayout.Grid };

        Assert.Equal(
            OverlayLayout.Grid,
            new SettingsStore(_file, NullLogger<SettingsStore>.Instance).Current.Layout
        );
    }

    [Fact]
    public void Update_saves_and_raises_Changed()
    {
        var store = new SettingsStore(_file, NullLogger<SettingsStore>.Instance);
        Settings? raised = null;
        store.Changed += s => raised = s;

        store.Update(s => s with { Enabled = false });

        var expected = new Settings { Enabled = false };
        Assert.Equal(expected, store.Current);
        Assert.Equal(expected, _file.Stored);
        Assert.Equal(expected, raised);
    }

    [Fact]
    public void Update_without_a_change_does_nothing()
    {
        var store = new SettingsStore(_file, NullLogger<SettingsStore>.Instance);
        var raised = 0;
        store.Changed += _ => raised++;

        store.Update(s => s with { Enabled = true });

        Assert.Equal(0, raised);
        Assert.Equal(0, _file.SaveCount);
    }

    [Fact]
    public void Failed_save_keeps_the_change_and_still_raises_Changed()
    {
        _file.FailOnSave = true;
        var store = new SettingsStore(_file, NullLogger<SettingsStore>.Instance);
        var raised = 0;
        store.Changed += _ => raised++;

        store.Update(s => s with { Layout = OverlayLayout.Grid });

        Assert.Equal(OverlayLayout.Grid, store.Current.Layout);
        Assert.Equal(1, raised);
    }

    private sealed class FakeSettingsFile : ISettingsFile
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
