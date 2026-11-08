using Zwitcher.Core.WindowList;

namespace Zwitcher.Core.Switching;

/// <summary>
/// The state machine from ARCHITECTURE.md and the only component that makes decisions.
/// Runs on the UI thread only.
/// </summary>
public sealed class SwitcherController
{
    private readonly IWindowTracker _tracker;
    private readonly IOverlayView _view;
    private readonly IWindowActivator _activator;
    private readonly SwitcherActiveFlag _activeFlag;

    private IReadOnlyList<WindowInfo> _windows = [];
    private int _selected;
    private SwitcherMode _mode = SwitcherMode.Idle;

    public SwitcherController(
        IWindowTracker tracker,
        IOverlayView view,
        IWindowActivator activator,
        SwitcherActiveFlag activeFlag
    )
    {
        _tracker = tracker;
        _view = view;
        _activator = activator;
        _activeFlag = activeFlag;

        _view.ItemClicked += OnItemClicked;
    }

    public void Handle(SwitcherCommand command)
    {
        // Anything without a case here is ignored, including unknown values from a message.
        switch (command)
        {
            case SwitcherCommand.Activate when _mode == SwitcherMode.Idle:
                Open(reverse: false);
                break;
            case SwitcherCommand.ActivatePrev when _mode == SwitcherMode.Idle:
                Open(reverse: true);
                break;
            case SwitcherCommand.Next when _mode == SwitcherMode.Active:
                Move(1);
                break;
            case SwitcherCommand.Prev when _mode == SwitcherMode.Active:
                Move(-1);
                break;
            case SwitcherCommand.Confirm when _mode == SwitcherMode.Active:
                Close(_selected);
                break;
            case SwitcherCommand.Cancel when _mode == SwitcherMode.Active:
                Close(null);
                break;
            case SwitcherCommand.WindowsChanged when _mode == SwitcherMode.Active:
                Refresh();
                break;
        }
    }

    private void Open(bool reverse)
    {
        // First, because Alt may be released any moment. Also needed for an empty list.
        _activator.Prepare();

        var windows = _tracker.Snapshot();
        if (windows.Count == 0)
        {
            _activeFlag.IsSet = false;
            return;
        }

        _windows = windows;
        _selected = reverse ? windows.Count - 1 : FirstSelection(windows);
        _mode = SwitcherMode.Active;
        _view.Show(_windows, _selected);
    }

    /// <summary>
    /// The previously used window: the second one, unless the first isn't current. That happens
    /// when the desktop or the taskbar has focus, and then the first one is what was used last.
    /// </summary>
    private int FirstSelection(IReadOnlyList<WindowInfo> windows) =>
        _tracker.IsForeground(windows[0].Handle) ? Math.Min(1, windows.Count - 1) : 0;

    private void Refresh()
    {
        var windows = _tracker.Snapshot();

        // Title updates of unrelated windows land here too. Rebuilding rows would flicker.
        if (windows.SequenceEqual(_windows))
        {
            return;
        }

        if (windows.Count == 0)
        {
            Close(null);
            return;
        }

        _selected = IndexAfterRefresh(windows);
        _windows = windows;
        _view.UpdateWindows(_windows, _selected);
    }

    /// <summary>The same window if it still exists, else the nearest index.</summary>
    private int IndexAfterRefresh(IReadOnlyList<WindowInfo> windows)
    {
        var selected = _windows[_selected].Handle;
        for (var i = 0; i < windows.Count; i++)
        {
            if (windows[i].Handle == selected)
            {
                return i;
            }
        }

        return Math.Min(_selected, windows.Count - 1);
    }

    private void Move(int delta)
    {
        var count = _windows.Count;
        _selected = (_selected + delta + count) % count;
        _view.UpdateSelection(_selected);
    }

    private void Close(int? activateIndex)
    {
        var target = activateIndex is { } index ? _windows[index] : null;

        _mode = SwitcherMode.Idle;
        _windows = [];
        _view.Hide();
        if (target is not null)
        {
            _activator.Activate(target.Handle);
        }

        _activeFlag.IsSet = false;
    }

    private void OnItemClicked(int index)
    {
        if (_mode == SwitcherMode.Active && index >= 0 && index < _windows.Count)
        {
            Close(index);
        }
    }
}
