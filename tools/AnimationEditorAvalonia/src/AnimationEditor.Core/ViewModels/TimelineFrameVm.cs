using System.Globalization;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AnimationEditor.Core.ViewModels;

public sealed class TimelineFrameVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName] string p = "") =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

    public int Index { get; }
    public double Width { get; }

    /// <summary>Width of the playhead bar in pixels. Used to compute travel width so the bar's right edge aligns with the cell edge at end-of-frame.</summary>
    public const double PlayheadWidth = 2.0;

    public string IndexLabel => (Index + 1).ToString(CultureInfo.InvariantCulture);

    private bool _isCurrent;
    public bool IsCurrent
    {
        get => _isCurrent;
        set
        {
            if (_isCurrent != value)
            {
                _isCurrent = value;
                Notify();
            }
        }
    }

    private object? _thumbnail;
    /// <summary>
    /// Thumbnail image for this frame cell, set by the App layer after the texture is resolved.
    /// Holds an <c>Avalonia.Media.Imaging.Bitmap</c> at runtime; null until populated.
    /// </summary>
    public object? Thumbnail
    {
        get => _thumbnail;
        set
        {
            if (!ReferenceEquals(_thumbnail, value))
            {
                _thumbnail = value;
                Notify();
            }
        }
    }

    private double _scrubberOffset;
    /// <summary>
    /// X offset of the playhead within this frame cell (0..Width-PlayheadWidth).
    /// Updated every tick by MainWindow while this frame is current.
    /// </summary>
    public double ScrubberOffset
    {
        get => _scrubberOffset;
        set
        {
            if (_scrubberOffset != value)
            {
                _scrubberOffset = value;
                Notify();
            }
        }
    }

    public TimelineFrameVm(int index, double width)
    {
        Index = index;
        Width = width;
    }
}
