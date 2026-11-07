namespace Zwitcher.Core.Windows;

public sealed record WindowInfo(nint Handle, string Title, int ProcessId, string? AppId);
