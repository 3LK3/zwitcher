namespace Zwitcher.Core.Windows;

/// <summary>The extended window styles the filter cares about, with their Win32 values.</summary>
[Flags]
public enum WindowExStyle : uint
{
    None = 0,

    /// <summary>WS_EX_TOOLWINDOW.</summary>
    ToolWindow = 0x00000080,

    /// <summary>WS_EX_APPWINDOW.</summary>
    AppWindow = 0x00040000,
}
