namespace Zwitcher.Core.Configuration;

public interface ISettingsFile
{
    /// <summary>Never fails: a missing or damaged store gives the defaults.</summary>
    Settings Load();

    void Save(Settings settings);
}
