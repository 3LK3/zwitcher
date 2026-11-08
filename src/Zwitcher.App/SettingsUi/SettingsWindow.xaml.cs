using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Extensions.Logging;
using Zwitcher.Core.Configuration;
using Zwitcher.Core.Screen;

namespace Zwitcher.App.SettingsUi;

/// <summary>
/// Settings and About in one flyout that opens next to the tray icon and closes when it loses
/// focus, like the Windows volume and network flyouts. It's created for each opening and
/// closed (not hidden) afterwards, so no state outlives it.
/// </summary>
public sealed partial class SettingsWindow : Window
{
    private readonly GeneralPage _general;
    private readonly AboutPage _about;
    private bool _closing;

    public SettingsWindow(
        SettingsStore settings,
        StartupRegistration startup,
        ILoggerFactory loggers
    )
    {
        // Fluent controls that follow the Windows light/dark setting. Only this window opts in,
        // the overlay keeps its own look. The API is marked experimental in .NET 9+.
#pragma warning disable WPF0001
        ThemeMode = ThemeMode.System;
#pragma warning restore WPF0001

        InitializeComponent();

        _general = new GeneralPage(settings, startup);
        _about = new AboutPage(loggers.CreateLogger<AboutPage>());

        SourceInitialized += (_, _) =>
            Win32Window.RoundCorners(new WindowInteropHelper(this).Handle);
        Deactivated += (_, _) => CloseOnce();
        Closing += (_, _) => _closing = true;
        Closed += (_, _) => _general.Detach();
    }

    public void ShowPage(SettingsPage page) =>
        Nav.SelectedItem = page == SettingsPage.About ? AboutItem : GeneralItem;

    /// <summary>
    /// Places the flyout in the corner above the taskbar on the monitor of the anchor (the
    /// tray icon, in physical pixels), then shows and activates it. Placing before showing
    /// avoids a visible jump.
    /// </summary>
    public void ShowAt(PixelPoint anchor)
    {
        var handle = new WindowInteropHelper(this).EnsureHandle();
        var monitor = Monitors.AtPoint(anchor);
        var scale = monitor.Scale;
        var width = (int)Math.Round(Width * scale);
        var height = (int)Math.Round(Height * scale);
        var (x, y) = FlyoutPlacement.Compute(
            anchor,
            monitor.Bounds,
            monitor.WorkArea,
            width,
            height
        );
        Win32Window.Place(handle, x, y, width, height);

        Show();
        Activate();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CloseOnce();
            e.Handled = true;
            return;
        }
        base.OnPreviewKeyDown(e);
    }

    private void OnNavChanged(object sender, SelectionChangedEventArgs e) =>
        PageHost.Content = Nav.SelectedItem == AboutItem ? _about : _general;

    private void OnCloseClicked(object sender, RoutedEventArgs e) => CloseOnce();

    /// <summary>
    /// Closing deactivates the window, and Deactivated closes it. A second Close() during
    /// closing throws, so this guards against the echo.
    /// </summary>
    private void CloseOnce()
    {
        if (!_closing)
        {
            Close();
        }
    }
}
