namespace Zwitcher.Core.Activation;

public interface IWindowActivator
{
    /// <summary>Brings the window to the foreground. Must tolerate a window that has closed.</summary>
    void Activate(nint handle);
}
