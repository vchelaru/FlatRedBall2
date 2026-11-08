using System.ComponentModel;

namespace AnimationEditor.App.Models;

/// <summary>One row of the sequence list: its name, and whether the row is being renamed in place.</summary>
internal sealed class SequenceListItem : INotifyPropertyChanged
{
    private bool _isEditing;

    public SequenceListItem(string name, string summary)
    {
        Name = name;
        Summary = summary;
    }

    public string Name { get; }

    /// <summary>The grey text after the name, for example "2 tracks · 0.60s" (<c>SequenceListSummary</c>).</summary>
    public string Summary { get; }

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
