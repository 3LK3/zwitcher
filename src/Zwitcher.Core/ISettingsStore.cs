namespace Zwitcher.Core;

public interface ISettingsStore
{
    /// <summary>Never fails: a missing or damaged store gives the defaults.</summary>
    Settings Load();

    void Save(Settings settings);
}
