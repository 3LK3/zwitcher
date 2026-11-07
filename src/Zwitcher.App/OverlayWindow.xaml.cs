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

    // Of the work area. Longer lists scroll.
    private const double MaxHeightShare = 0.8;

    private readonly IconCache _icons = new();

    private OverlayItem[] _rows = [];
    private int _selected;
    private MonitorArea? _monitor;
    private bool _warmingUp;

    public OverlayWindow()
    {
        InitializeComponent();
    }

    public event Action<int>? ItemClicked;

    /// <summary>
    /// Shows the overlay once, invisibly, so that the first real open doesn't pay for JIT and
    /// the first render. Hidden again as soon as the dispatcher is idle.
    /// </summary>
    public void WarmUp()
    {
        Opacity = 0;
        Show([new WindowInfo(0, "Warm up", 0, null)], 0);
        _warmingUp = true;
        Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            () =>
            {
                if (_warmingUp)
                {
                    Hide();
                    EndWarmUp();
                }
            }
        );
    }

    public void Show(IReadOnlyList<WindowInfo> windows, int selectedIndex)
    {
#if DEBUG
        var stopwatch = Stopwatch.StartNew();
#endif
        EndWarmUp();
        _icons.KeepOnly(windows);
        SetRows(windows, selectedIndex);
        MoveToPrimaryMonitor();
        FitToRows();
        Scroller.ScrollToTop();
        Show();
        UpdateLayout();
        BringSelectionIntoView();
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
        BringSelectionIntoView();
    }

    public void UpdateWindows(IReadOnlyList<WindowInfo> windows, int selectedIndex)
    {
        Debug.WriteLine($"Overlay updated: {windows.Count} rows, selected={selectedIndex}");
        SetRows(windows, selectedIndex);
        FitToRows();
        UpdateLayout();
        BringSelectionIntoView();
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

    private void EndWarmUp()
    {
        if (_warmingUp)
        {
            _warmingUp = false;
            Opacity = 1;
        }
    }

    private void BringSelectionIntoView()
    {
        if (Rows.ItemContainerGenerator.ContainerFromIndex(_selected) is FrameworkElement row)
        {
            row.BringIntoView();
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
    private void MoveToPrimaryMonitor()
    {
        var monitor = OverlayNative.GetPrimaryMonitor();
        OverlayNative.Move(
            new WindowInteropHelper(this).Handle,
            monitor.Left + (monitor.Width / 2),
            monitor.Top + (monitor.Height / 2)
        );
        _monitor = monitor;
    }

    /// <summary>Sizes the window to the rows and centers it on the current monitor.</summary>
    private void FitToRows()
    {
        if (_monitor is not { } monitor)
        {
            return;
        }

        // Measure alone would return the old size: new rows only mark the items panel as dirty,
        // not its ancestors, so the call exits early. UpdateLayout processes them first.
        Panel.UpdateLayout();
        Panel.Measure(new Size(OverlayWidth, double.PositiveInfinity));
        var scale = monitor.Dpi / DefaultDpi;
        Width = OverlayWidth;
        Height = Math.Min(Panel.DesiredSize.Height, monitor.Height * MaxHeightShare / scale);

        var width = (int)Math.Ceiling(Width * scale);
        var height = (int)Math.Ceiling(Height * scale);
        var (x, y) = monitor.Center(width, height);
        OverlayNative.Place(new WindowInteropHelper(this).Handle, x, y, width, height);
    }

    /// <summary>A window without an icon gets the fallback, and maybe its package icon later.</summary>
    private void LoadIcon(OverlayItem row)
    {
        row.Icon = _icons.Load(row.Handle);
        if (row.Icon is null)
        {
            row.Icon = IconCache.Fallback;
            _icons.LoadPackaged(row.Handle, icon => row.Icon = icon);
        }
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
                Dispatcher.BeginInvoke(DispatcherPriority.Background, () => LoadIcon(row));
            }
        }
    }
}
