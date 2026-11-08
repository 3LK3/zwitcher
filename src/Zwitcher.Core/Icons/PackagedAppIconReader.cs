using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.System.Variant;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.Shell.PropertiesSystem;

namespace Zwitcher.Core.Icons;

/// <summary>
/// Reads the icon of a packaged app. These windows have no icon of their own: the frame belongs
/// to ApplicationFrameHost, so the icon comes from the shell, through the AppUserModelID.
/// May take up to 150 ms and may be called on any thread, but not on the hook thread.
/// </summary>
public static class PackagedAppIconReader
{
    /// <summary>Returns null if the window has no packaged app behind it.</summary>
    public static PixelImage? TryRead(nint window, int size)
    {
        try
        {
            return ReadAppId((HWND)window) is { } appId ? ReadIcon(appId, size) : null;
        }
        // Windows and apps can disappear at any point during these calls, and a failed HRESULT
        // becomes one of many exception types: E_INVALIDARG is an ArgumentException, for one.
        // This runs on a pool thread, where anything uncaught would end the whole process, and
        // a missing icon is never worth that.
        catch (Exception)
        {
            return null;
        }
    }

    private static unsafe string? ReadAppId(HWND window)
    {
        var storeId = typeof(IPropertyStore).GUID;
        PInvoke.SHGetPropertyStoreForWindow(window, &storeId, out var store).ThrowOnFailure();

        ((IPropertyStore)store).GetValue(PInvoke.PKEY_AppUserModel_ID, out var value);
        try
        {
            var inner = value.Anonymous.Anonymous;
            return inner.vt == VARENUM.VT_LPWSTR ? inner.Anonymous.pwszVal.ToString() : null;
        }
        finally
        {
            PInvoke.PropVariantClear(ref value);
        }
    }

    private static unsafe PixelImage? ReadIcon(string appId, int size)
    {
        var appsFolder = PInvoke.FOLDERID_AppsFolder;
        var factoryId = typeof(IShellItemImageFactory).GUID;
        object item;
        fixed (char* name = appId)
        {
            PInvoke
                .SHCreateItemInKnownFolder(&appsFolder, 0, name, &factoryId, out item)
                .ThrowOnFailure();
        }

        var requested = new SIZE { cx = size, cy = size };
        ((IShellItemImageFactory)item).GetImage(requested, SIIGBF.SIIGBF_ICONONLY, out var bitmap);
        using (bitmap)
        {
            return ReadPixels((HBITMAP)bitmap.DangerousGetHandle(), size);
        }
    }

    /// <summary>
    /// GetDIBits gives a defined layout: a negative height asks for top-down rows, and alpha
    /// survives, unlike with Imaging.CreateBitmapSourceFromHBitmap.
    /// </summary>
    private static unsafe PixelImage? ReadPixels(HBITMAP bitmap, int size)
    {
        var info = new BITMAPINFO();
        info.bmiHeader.biSize = (uint)sizeof(BITMAPINFOHEADER);
        info.bmiHeader.biWidth = size;
        info.bmiHeader.biHeight = -size;
        info.bmiHeader.biPlanes = 1;
        info.bmiHeader.biBitCount = 32;

        var pixels = new byte[size * size * 4];
        var screen = PInvoke.GetDC(HWND.Null);
        try
        {
            fixed (byte* target = pixels)
            {
                var lines = PInvoke.GetDIBits(
                    screen,
                    bitmap,
                    0,
                    (uint)size,
                    target,
                    &info,
                    DIB_USAGE.DIB_RGB_COLORS
                );
                return lines == size ? new PixelImage(size, size, pixels) : null;
            }
        }
        finally
        {
            _ = PInvoke.ReleaseDC(HWND.Null, screen);
        }
    }
}
