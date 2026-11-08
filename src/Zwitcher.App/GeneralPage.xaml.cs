using System.Windows;
using System.Windows.Controls;
using Zwitcher.Core;

namespace Zwitcher.App;

/// <summary>Every change applies and saves immediately, so there's nothing to confirm.</summary>
public sealed partial class GeneralPage : UserControl
{
    private readonly SettingsService _settings;
    private readonly StartupRegistration _startup;

    public GeneralPage(SettingsService settings, StartupRegistration startup)
    {
        InitializeComponent();
        _settings = settings;
        _startup = startup;

        ShowOnBox.ItemsSource = new[]
        {
            new Choice<ShowOn>("Primary monitor", ShowOn.PrimaryMonitor),
            new Choice<ShowOn>("Monitor with the cursor", ShowOn.CursorMonitor),
            new Choice<ShowOn>("Monitor with the active window", ShowOn.ForegroundMonitor),
        };
        LayoutBox.ItemsSource = new[]
        {
            new Choice<OverlayLayout>("List", OverlayLayout.List),
            new Choice<OverlayLayout>("Grid", OverlayLayout.Grid),
        };

        Display(_settings.Current);
        StartupBox.IsChecked = _startup.IsEnabled;

        // The tray can change settings too, while this page is open.
        _settings.Changed += Display;
    }

    /// <summary>Unsubscribes from the settings. Called by the window when it closes.</summary>
    public void Detach() => _settings.Changed -= Display;

    private void Display(Settings settings)
    {
        EnabledBox.IsChecked = settings.Enabled;
        ShowOnBox.SelectedValue = settings.ShowOn;
        LayoutBox.SelectedValue = settings.Layout;
    }

    private void OnEnabledClicked(object sender, RoutedEventArgs e) =>
        _settings.Update(s => s with { Enabled = EnabledBox.IsChecked == true });

    private void OnStartupClicked(object sender, RoutedEventArgs e)
    {
        _startup.TrySetEnabled(StartupBox.IsChecked == true);

        // What the registry says, in case it refused the change.
        StartupBox.IsChecked = _startup.IsEnabled;
    }

    private void OnShowOnChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ShowOnBox.SelectedValue is ShowOn showOn)
        {
            _settings.Update(s => s with { ShowOn = showOn });
        }
    }

    private void OnLayoutChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LayoutBox.SelectedValue is OverlayLayout layout)
        {
            _settings.Update(s => s with { Layout = layout });
        }
    }

    // Public, because WPF bindings can't read properties of a non-public type.
    public sealed record Choice<T>(string Label, T Value);
}
