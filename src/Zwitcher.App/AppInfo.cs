using System.Reflection;

namespace Zwitcher.App;

/// <summary>Facts about this build, shared by the log and the About page.</summary>
public static class AppInfo
{
    public const string RepositoryUrl = "https://github.com/3LK3/zwitcher";

    /// <summary>SemVer plus the short commit, e.g. 1.0.0+abc1234. The SDK appends the commit.</summary>
    public static string Version()
    {
        var version =
            typeof(AppInfo)
                .Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion
            ?? "unknown";
        var plus = version.IndexOf('+');
        return plus >= 0 && version.Length > plus + 8 ? version[..(plus + 8)] : version;
    }
}
