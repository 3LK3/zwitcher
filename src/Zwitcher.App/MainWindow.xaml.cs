using System.Windows;

namespace Zwitcher.App;

/// <summary>The Alt+Tab overlay. Created at startup and kept hidden until needed.</summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
