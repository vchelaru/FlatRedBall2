using Avalonia.Media.Imaging;
using System.ComponentModel;

namespace AnimationEditor.App.Models;

/// <summary>One row of the sequence list: its name, and whether the row is being renamed in place.</summary>
internal sealed class SequenceListItem : INotifyPropertyChanged
{
    private bool _isEditing;
    private Bitmap? _thumbnail;

    public SequenceListItem(string name, string summary)
    {
        Name = name;
        Summary = summary;
    }

    public string Name { get; }

    /// <summary>The grey text after the name, for example "2 tracks · 0.60s" (<c>SequenceListSummary</c>).</summary>
    public string Summary { get; }

    /// <summary>
    /// The sequence's picture at the first instant where something shows, set asynchronously after the row appears. Null
    /// until it arrives or when nothing in the sequence resolves; the row reserves its slot either way, so rows do not jump.
    /// </summary>
    public Bitmap? Thumbnail
    {
        get => _thumbnail;
        set
        {
            if (ReferenceEquals(_thumbnail, value)) return;
            _thumbnail = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Thumbnail)));
        }
    }

    public bool IsEditing
    {
        get => _isEditing;
        set
        {
            if (_isEditing == value) return;
            _isEditing = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEditing)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
