using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zwitcher.Core;

/// <summary>Keeps the settings in a JSON file.</summary>
public sealed class JsonSettingsStore(string path) : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
    };

    public static string DefaultPath { get; } =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Zwitcher",
            "settings.json"
        );

    public Settings Load()
    {
        // A switcher must not refuse to start over a settings file.
        try
        {
            return JsonSerializer.Deserialize<Settings>(File.ReadAllText(path), Options)
                ?? new Settings();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return new Settings();
        }
    }

    public void Save(Settings settings)
    {
        if (Path.GetDirectoryName(path) is { Length: > 0 } directory)
        {
            Directory.CreateDirectory(directory);
        }

        // Replacing in one step means a crash can't leave half a file behind.
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings, Options));
        File.Move(temporary, path, overwrite: true);
    }
}
