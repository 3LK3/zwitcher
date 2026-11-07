using System.Diagnostics;
using Zwitcher.Core.Activation;

namespace Zwitcher.App.Debugging;

/// <summary>Temporary stand-in that logs instead of switching windows. M4 replaces it.</summary>
public sealed class LoggingWindowActivator : IWindowActivator
{
    public void Activate(nint handle) => Debug.WriteLine($"Activate({handle})");
}
