using System.ComponentModel;
using System.Windows.Media;

namespace Zwitcher.App.Overlay;

/// <summary>One row of the overlay.</summary>
public sealed class OverlayRow(nint handle, string title, ImageSource? icon)
    : INotifyPropertyChanged
{
    // Cached, so that moving the selection doesn't allocate.
    private static readonly PropertyChangedEventArgs IconChanged = new(nameof(Icon));
    private static readonly PropertyChangedEventArgs IsSelectedChanged = new(nameof(IsSelected));

    private ImageSource? _icon = icon;
    private bool _isSelected;

    public event PropertyChangedEventHandler? PropertyChanged;

    public nint Handle { get; } = handle;

    public string Title { get; } = title;

    public ImageSource? Icon
    {
        get => _icon;
        set
        {
            _icon = value;
            PropertyChanged?.Invoke(this, IconChanged);
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            PropertyChanged?.Invoke(this, IsSelectedChanged);
        }
    }
}
