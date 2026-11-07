using System.ComponentModel;

namespace AnimationEditor.App.Models;

/// <summary>One row of the sequence list: its name, and whether the row is being renamed in place.</summary>
internal sealed class SequenceListItem : INotifyPropertyChanged
{
    private bool _isEditing;

    public SequenceListItem(string name) => Name = name;

    public string Name { get; }

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
