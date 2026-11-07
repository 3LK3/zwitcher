using Zwitcher.Core.Activation;
using Zwitcher.Core.Input;
using Zwitcher.Core.Windows;

namespace Zwitcher.Core;

/// <summary>
/// The state machine from ARCHITECTURE.md and the only component that makes decisions.
/// Runs on the UI thread only.
/// </summary>
public sealed class SwitcherController
{
    private readonly IWindowTracker _tracker;
    private readonly IOverlayView _view;
    private readonly IWindowActivator _activator;
    private readonly ISwitcherState _state;

    private IReadOnlyList<WindowInfo> _windows = [];
    private int _selected;
    private bool _active;

    public SwitcherController(
        IWindowTracker tracker,
        IOverlayView view,
        IWindowActivator activator,
        ISwitcherState state
    )
    {
        _tracker = tracker;
        _view = view;
        _activator = activator;
        _state = state;

        _view.ItemClicked += OnItemClicked;
    }

    public void Handle(SwitcherCommand command)
    {
        // Anything without a case here is ignored, including unknown values from a message.
        switch (command)
        {
            case SwitcherCommand.Activate when !_active:
                Open(reverse: false);
                break;
            case SwitcherCommand.ActivatePrev when !_active:
                Open(reverse: true);
                break;
            case SwitcherCommand.Next when _active:
                Move(1);
                break;
            case SwitcherCommand.Prev when _active:
                Move(-1);
                break;
            case SwitcherCommand.Confirm when _active:
                Close(_selected);
                break;
            case SwitcherCommand.Cancel when _active:
                Close(null);
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
            _state.IsActive = false;
            return;
        }

        _windows = windows;
        _selected = reverse ? windows.Count - 1 : Math.Min(1, windows.Count - 1);
        _active = true;
        _view.Show(_windows, _selected);
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

        _active = false;
        _windows = [];
        _view.Hide();
        if (target is not null)
        {
            _activator.Activate(target.Handle);
        }

        _state.IsActive = false;
    }

    private void OnItemClicked(int index)
    {
        if (_active && index >= 0 && index < _windows.Count)
        {
            Close(index);
        }
    }
}
