using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Zwitcher.Core.Diagnostics;

namespace Zwitcher.App;

public sealed partial class AboutPage : UserControl
{
    private const string IconResourceName = "Zwitcher.App.Assets.zwitcher.ico";

    public AboutPage()
    {
        InitializeComponent();
        VersionText.Text = $"Version {AppInfo.Version()}";
        RepositoryText.Text = AppInfo.RepositoryUrl;
        AppIcon.Source = LoadIcon();
    }

    private void OnRepositoryClicked(object sender, RoutedEventArgs e) =>
        Open(AppInfo.RepositoryUrl);

    private void OnOpenLogsClicked(object sender, RoutedEventArgs e)
    {
        // The folder only exists after the first log line, which is written at startup, but
        // a failed create there is swallowed by the listener.
        Directory.CreateDirectory(RollingFileTraceListener.DefaultDirectory);
        Open(RollingFileTraceListener.DefaultDirectory);
    }

    private static void Open(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException)
        {
            Trace.TraceWarning($"Could not open {target}: {ex.Message}");
        }
    }

    /// <summary>The largest frame of the embedded .ico, so the icon stays sharp at any DPI.</summary>
    private static BitmapSource? LoadIcon()
    {
        using var stream = typeof(AboutPage).Assembly.GetManifestResourceStream(IconResourceName);
        if (stream is null)
        {
            return null;
        }

        var decoder = new IconBitmapDecoder(
            stream,
            BitmapCreateOptions.None,
            BitmapCacheOption.OnLoad
        );
        return decoder.Frames.MaxBy(f => f.PixelWidth);
    }
}
