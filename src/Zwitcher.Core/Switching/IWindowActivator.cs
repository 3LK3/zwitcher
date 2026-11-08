namespace Zwitcher.Core.Switching;

public interface IWindowActivator
{
    /// <summary>Called when the switcher opens, while Alt is still held.</summary>
    void Prepare();

    /// <summary>Brings the window to the foreground. Must tolerate a window that has closed.</summary>
    void Activate(nint handle);
}
