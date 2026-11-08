namespace Zwitcher.Core;

/// <summary>Where the log lives. The logger itself is composed in the App.</summary>
public static class ZwitcherLog
{
    /// <summary>%LocalAppData%\Zwitcher\logs: logs are per machine, unlike roaming settings.</summary>
    public static string DefaultDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Zwitcher",
            "logs"
        );

    public static string FilePath => Path.Combine(DefaultDirectory, "zwitcher.log");
}
