using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using Zwitcher.Core;

namespace Zwitcher.App.SettingsUi;

public sealed partial class AboutPage : UserControl
{
    private readonly ILogger<AboutPage> _logger;

    public AboutPage(ILogger<AboutPage> logger)
    {
        InitializeComponent();

        _logger = logger;

        VersionText.Text = $"Version {AppInfo.Version()}";
        RepositoryText.Text = AppInfo.RepositoryUrl;
        AppIcon.Source = LoadIcon();
    }

    private void OnRepositoryClicked(object sender, RoutedEventArgs e) =>
        Open(AppInfo.RepositoryUrl);

    private void OnOpenLogsClicked(object sender, RoutedEventArgs e)
    {
        // The folder only exists after the first log line, which is written at startup, but
        // Serilog swallows a failed create there. Explorer can't open a missing folder.
        try
        {
            Directory.CreateDirectory(ZwitcherLog.DefaultDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not create {Directory}", ZwitcherLog.DefaultDirectory);
            return;
        }

        Open(ZwitcherLog.DefaultDirectory);
    }

    private void Open(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException)
        {
            _logger.LogWarning(ex, "Could not open {Target}", target);
        }
    }

    /// <summary>The largest frame of the embedded .ico, so the icon stays sharp at any DPI.</summary>
    private static BitmapSource? LoadIcon()
    {
        using var stream = AppInfo.OpenIconStream();
        var decoder = new IconBitmapDecoder(
            stream,
            BitmapCreateOptions.None,
            BitmapCacheOption.OnLoad
        );
        return decoder.Frames.MaxBy(f => f.PixelWidth);
    }
}
