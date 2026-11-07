using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Zwitcher.Core;
using Zwitcher.Core.Interop;
using Zwitcher.Core.Windows;

namespace Zwitcher.App;

/// <summary>
/// The Alt+Tab overlay. Created at startup and kept hidden until needed. It renders what the
/// controller passes in and makes no decisions. Hide() comes from Window.
/// </summary>
public sealed partial class OverlayWindow : Window, IOverlayView
{
    private const double OverlayWidth = 480;
    private const double DefaultDpi = 96;

    private readonly IconCache _icons = new();

    private OverlayItem[] _rows = [];
    private int _selected;

    public OverlayWindow()
    {
        InitializeComponent();
    }

    public event Action<int>? ItemClicked;

    public void Show(IReadOnlyList<WindowInfo> windows, int selectedIndex)
    {
#if DEBUG
        var stopwatch = Stopwatch.StartNew();
#endif
        _icons.KeepOnly(windows);
        SetRows(windows, selectedIndex);
        PlaceOnForegroundMonitor();
        Show();
        LoadMissingIcons();
#if DEBUG
        // Loaded runs right after Render, so this includes layout and the first frame.
        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            () => Debug.WriteLine($"Overlay shown in {stopwatch.ElapsedMilliseconds} ms")
        );
#endif
    }

    public void UpdateSelection(int selectedIndex)
    {
        _rows[_selected].IsSelected = false;
        _rows[selectedIndex].IsSelected = true;
        _selected = selectedIndex;
    }

    public void UpdateWindows(IReadOnlyList<WindowInfo> windows, int selectedIndex)
    {
        SetRows(windows, selectedIndex);
        LoadMissingIcons();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        OverlayNative.MakeToolWindow(new WindowInteropHelper(this).Handle);
    }

    private void OnRowClicked(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: OverlayItem row })
        {
            ItemClicked?.Invoke(Array.IndexOf(_rows, row));
        }
    }

    private void SetRows(IReadOnlyList<WindowInfo> windows, int selectedIndex)
    {
        var rows = new OverlayItem[windows.Count];
        for (var i = 0; i < rows.Length; i++)
        {
            var handle = windows[i].Handle;
            rows[i] = new OverlayItem(handle, windows[i].Title, _icons.Find(handle));
        }

        rows[selectedIndex].IsSelected = true;
        _rows = rows;
        _selected = selectedIndex;
        Rows.ItemsSource = rows;
    }

    /// <summary>
    /// Works in physical pixels, because WPF's Left and Top are unreliable across monitors
    /// with different DPI. The first move lets WPF pick up the DPI of the target monitor.
    /// </summary>
    private void PlaceOnForegroundMonitor()
    {
        var handle = new WindowInteropHelper(this).Handle;
        var monitor = OverlayNative.GetForegroundMonitor();
        OverlayNative.Move(
            handle,
            monitor.Left + (monitor.Width / 2),
            monitor.Top + (monitor.Height / 2)
        );

        Panel.Measure(new Size(OverlayWidth, double.PositiveInfinity));
        Width = OverlayWidth;
        Height = Panel.DesiredSize.Height;

        var scale = monitor.Dpi / DefaultDpi;
        var width = (int)Math.Ceiling(Width * scale);
        var height = (int)Math.Ceiling(Height * scale);
        var (x, y) = monitor.Center(width, height);
        OverlayNative.Place(handle, x, y, width, height);
    }

    /// <summary>
    /// WM_GETICON is a cross-process call, so it stays off the show path. One dispatcher item
    /// per icon lets key presses run in between.
    /// </summary>
    private void LoadMissingIcons()
    {
        foreach (var row in _rows)
        {
            if (row.Icon is null)
            {
                Dispatcher.BeginInvoke(
                    DispatcherPriority.Background,
                    () => row.Icon = _icons.Load(row.Handle)
                );
            }
        }
    }
}
