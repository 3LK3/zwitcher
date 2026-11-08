namespace Zwitcher.Core.WindowList;

public sealed record WindowInfo(nint Handle, string Title, int ProcessId);
