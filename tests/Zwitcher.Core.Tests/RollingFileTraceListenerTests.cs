using System.Diagnostics;
using Zwitcher.Core.Diagnostics;

namespace Zwitcher.Core.Tests;

public sealed class RollingFileTraceListenerTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"Zwitcher-Tests-{Guid.NewGuid()}"
    );

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static string Read(string path)
    {
        // The listener keeps the file open, with read sharing.
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete
        );
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [Fact]
    public void Writes_a_timestamped_line_with_the_level()
    {
        using var listener = new RollingFileTraceListener(Path.Combine(_directory, "logs"));

        listener.TraceEvent(null, "test", TraceEventType.Warning, 0, "Saving failed");
        listener.WriteLine("debug line");

        var lines = Read(listener.FilePath)
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Matches(
            @"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} \[Warning\] Saving failed$",
            lines[0]
        );
        Assert.EndsWith("[Debug] debug line", lines[1]);
    }

    [Fact]
    public void Formats_arguments()
    {
        using var listener = new RollingFileTraceListener(_directory);

        listener.TraceEvent(null, "test", TraceEventType.Information, 0, "{0} windows", 3);

        Assert.Contains("[Information] 3 windows", Read(listener.FilePath));
    }

    [Fact]
    public void Appends_to_an_existing_log()
    {
        using (var first = new RollingFileTraceListener(_directory))
        {
            first.WriteLine("one");
        }

        using var second = new RollingFileTraceListener(_directory);
        second.WriteLine("two");

        var text = Read(second.FilePath);
        Assert.Contains("one", text);
        Assert.Contains("two", text);
    }

    [Fact]
    public void Rolls_over_at_the_size_limit()
    {
        using var listener = new RollingFileTraceListener(_directory, maxBytes: 100);

        listener.WriteLine(new string('a', 120));
        listener.WriteLine("after");

        Assert.Contains(new string('a', 120), Read(listener.RolledPath(1)));
        var current = Read(listener.FilePath);
        Assert.Contains("after", current);
        Assert.DoesNotContain("aaa", current);
    }

    [Fact]
    public void Keeps_only_the_newest_rolled_files()
    {
        using var listener = new RollingFileTraceListener(_directory, maxBytes: 10, keptFiles: 2);

        listener.WriteLine("first entry");
        listener.WriteLine("second entry");
        listener.WriteLine("third entry");

        Assert.Contains("third", Read(listener.RolledPath(1)));
        Assert.Contains("second", Read(listener.RolledPath(2)));
        Assert.False(File.Exists(listener.RolledPath(3)));
    }

    [Fact]
    public void Rolls_an_oversized_file_left_from_a_previous_run()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "zwitcher.log"), new string('a', 200));

        using var listener = new RollingFileTraceListener(_directory, maxBytes: 100);
        listener.WriteLine("new run");

        Assert.Contains("aaa", Read(listener.RolledPath(1)));
        Assert.DoesNotContain("aaa", Read(listener.FilePath));
    }

    [Fact]
    public void A_folder_that_cannot_be_created_does_not_throw()
    {
        Directory.CreateDirectory(_directory);
        var file = Path.Combine(_directory, "not-a-folder");
        File.WriteAllText(file, "");

        using var listener = new RollingFileTraceListener(Path.Combine(file, "logs"));

        listener.WriteLine("lost");
        listener.TraceEvent(null, "test", TraceEventType.Error, 0, "also lost");
    }
}
