using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Zwitcher.Core.Diagnostics;

/// <summary>
/// Writes <see cref="Trace"/> and <see cref="Debug"/> output to a file and rolls it over when
/// it gets too big, so that the log can't fill the disk. Logging must never take the app down,
/// so write errors are swallowed.
/// Trace serializes calls into listeners (<see cref="Trace.UseGlobalLock"/> is on by default),
/// so the UI thread and the icon thread can both log. The hook callback never logs.
/// </summary>
public sealed class RollingFileTraceListener : TraceListener
{
    public const long DefaultMaxBytes = 1024 * 1024;
    public const int DefaultKeptFiles = 3;

    private readonly string _directory;
    private readonly string _baseName;
    private readonly long _maxBytes;
    private readonly int _keptFiles;

    private StreamWriter? _writer;

    /// <param name="keptFiles">How many rolled-over files to keep next to the current one.</param>
    public RollingFileTraceListener(
        string directory,
        string fileName = "zwitcher.log",
        long maxBytes = DefaultMaxBytes,
        int keptFiles = DefaultKeptFiles
    )
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(keptFiles, 1);
        _directory = directory;
        _baseName = Path.GetFileNameWithoutExtension(fileName);
        _maxBytes = maxBytes;
        _keptFiles = keptFiles;
        FilePath = Path.Combine(directory, fileName);
    }

    /// <summary>%LocalAppData%\Zwitcher\logs: logs are per machine, unlike roaming settings.</summary>
    public static string DefaultDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Zwitcher",
            "logs"
        );

    public string FilePath { get; }

    public override void Write(string? message) => WriteEntry("Debug", message);

    public override void WriteLine(string? message) => WriteEntry("Debug", message);

    public override void TraceEvent(
        TraceEventCache? eventCache,
        string source,
        TraceEventType eventType,
        int id,
        string? message
    ) => WriteEntry(eventType.ToString(), message);

    public override void TraceEvent(
        TraceEventCache? eventCache,
        string source,
        TraceEventType eventType,
        int id,
        string? format,
        params object?[]? args
    ) =>
        WriteEntry(
            eventType.ToString(),
            args is { Length: > 0 } && format is not null
                ? string.Format(CultureInfo.InvariantCulture, format, args)
                : format
        );

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Close();
        }

        base.Dispose(disposing);
    }

    public override void Close()
    {
        _writer?.Dispose();
        _writer = null;
    }

    /// <summary>Path of a rolled-over file: 1 is the newest.</summary>
    public string RolledPath(int index) =>
        Path.Combine(_directory, $"{_baseName}.{index}{Path.GetExtension(FilePath)}");

    private void WriteEntry(string level, string? message)
    {
        try
        {
            var writer = _writer ?? Open();
            var time = DateTime.Now.ToString(
                "yyyy-MM-dd HH:mm:ss.fff",
                CultureInfo.InvariantCulture
            );
            writer.WriteLine($"{time} [{level}] {message}");
            writer.Flush();

            if (writer.BaseStream.Length >= _maxBytes)
            {
                Close();
                Roll();
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Try again with the next entry, e.g. once a locked file is free again.
            Close();
        }
    }

    private StreamWriter Open()
    {
        Directory.CreateDirectory(_directory);
        if (File.Exists(FilePath) && new FileInfo(FilePath).Length >= _maxBytes)
        {
            Roll();
        }

        // Read sharing, so that the log can be opened while Zwitcher runs.
        var stream = new FileStream(
            FilePath,
            FileMode.Append,
            FileAccess.Write,
            FileShare.ReadWrite | FileShare.Delete
        );
        _writer = new StreamWriter(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
        );
        return _writer;
    }

    private void Roll()
    {
        File.Delete(RolledPath(_keptFiles));
        for (var index = _keptFiles - 1; index >= 1; index--)
        {
            if (File.Exists(RolledPath(index)))
            {
                File.Move(RolledPath(index), RolledPath(index + 1));
            }
        }

        File.Move(FilePath, RolledPath(1));
    }
}
