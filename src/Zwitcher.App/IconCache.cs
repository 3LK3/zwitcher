using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Zwitcher.Core.Interop;
using Zwitcher.Core.Windows;

namespace Zwitcher.App;

/// <summary>Window icons by HWND, converted once so that showing the overlay stays cheap.</summary>
public sealed class IconCache
{
    private readonly Dictionary<nint, ImageSource> _icons = [];

    public ImageSource? Find(nint window) => _icons.GetValueOrDefault(window);

    /// <summary>Reads the icon from the window. Returns null if it has none.</summary>
    public ImageSource? Load(nint window)
    {
        var handle = WindowIcons.GetIconHandle(window);
        if (handle == 0)
        {
            return null;
        }

        try
        {
            var icon = Imaging.CreateBitmapSourceFromHIcon(
                handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions()
            );
            icon.Freeze();
            _icons[window] = icon;
            return icon;
        }
        // The window owns the handle and may destroy the icon while we read it.
        catch (Exception e) when (e is COMException or Win32Exception or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>Bounds the cache and drops entries whose handle Windows could reuse.</summary>
    public void KeepOnly(IReadOnlyList<WindowInfo> windows)
    {
        var current = windows.Select(w => w.Handle).ToHashSet();
        foreach (var window in _icons.Keys.Where(w => !current.Contains(w)).ToList())
        {
            _icons.Remove(window);
        }
    }
}
