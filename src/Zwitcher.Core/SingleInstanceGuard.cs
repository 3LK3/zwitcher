namespace Zwitcher.Core;

/// <summary>
/// Holds a named mutex for the lifetime of the app, so that a second launch can detect the
/// first. Windows releases the mutex if the process dies, so a crash can't block a restart.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private readonly Mutex _mutex;

    public SingleInstanceGuard(string name)
    {
        _mutex = new Mutex(initiallyOwned: true, name, out var createdNew);
        IsFirstInstance = createdNew;
    }

    public bool IsFirstInstance { get; }

    public void Dispose() => _mutex.Dispose();
}
