using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Zwitcher.Core.Interop;
using Zwitcher.Core.Windows;

namespace Zwitcher.App;

/// <summary>Window icons by HWND, converted once so that showing the overlay stays cheap.</summary>
public sealed class IconCache
{
    // Device pixels. The rows show 24 DIP, so this stays sharp up to 200% scaling.
    private const int PackagedIconSize = 48;

    private readonly Dictionary<nint, ImageSource> _icons = [];

    /// <summary>For windows without an icon of their own. Not cached per window, so that one
    /// that gets an icon later picks it up on the next open.</summary>
    public static ImageSource Fallback { get; } = CreateFallback();

    /// <summary>
    /// Asks the shell for the icon of a packaged app. The first lookup takes up to 150 ms, so it
    /// runs off the UI thread, as ARCHITECTURE.md allows. The result comes back on the UI thread.
    /// </summary>
    public void LoadPackaged(nint window, Action<ImageSource> loaded)
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        ThreadPool.QueueUserWorkItem(_ =>
        {
            if (PackagedIcons.TryRead(window, PackagedIconSize) is not { } image)
            {
                return;
            }

            var icon = BitmapSource.Create(
                image.Width,
                image.Height,
                96,
                96,
                PixelFormats.Pbgra32,
                null,
                image.Pixels,
                image.Width * 4
            );
            icon.Freeze();
            dispatcher.BeginInvoke(() =>
            {
                _icons[window] = icon;
                loaded(icon);
            });
        });
    }

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

    private static ImageSource CreateFallback()
    {
        var icon = Imaging.CreateBitmapSourceFromHIcon(
            System.Drawing.SystemIcons.Application.Handle,
            Int32Rect.Empty,
            BitmapSizeOptions.FromEmptyOptions()
        );
        icon.Freeze();
        return icon;
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
