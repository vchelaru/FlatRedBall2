using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AnimationEditor.Core.ViewModels;

/// <summary>
/// Lightweight view-model for a single node in the animation tree.
/// The <see cref="Data"/> field holds the underlying data object
/// (AnimationChainSave, AnimationFrameSave, AxisAlignedRectangleSave, CircleSave).
/// </summary>
public class TreeNodeVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName] string p = "") =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

    private string _header = string.Empty;
    public string Header
    {
        get => _header;
        set { if (_header != value) { _header = value; Notify(); } }
    }

    private bool _isExpanded;
    /// <summary>
    /// Whether this node is expanded in the tree view.
    /// Persisted in AESettingsSave.ExpandedNodes for chain-level nodes.
    /// </summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set { if (_isExpanded != value) { _isExpanded = value; Notify(); } }
    }

    private bool _isEditing;
    /// <summary>
    /// When <c>true</c> the tree item shows an editable TextBox instead of a TextBlock.
    /// Set via <see cref="BeginEdit"/>; cleared by <see cref="CancelEdit"/> or a successful commit.
    /// </summary>
    public bool IsEditing
    {
        get => _isEditing;
        set { if (_isEditing != value) { _isEditing = value; Notify(); } }
    }

    private string _editingText = string.Empty;
    /// <summary>Current text in the inline rename TextBox.</summary>
    public string EditingText
    {
        get => _editingText;
        set { if (_editingText != value) { _editingText = value; Notify(); } }
    }

    /// <summary>Enter inline-edit mode, seeding <see cref="EditingText"/> from <see cref="Header"/>.</summary>
    public void BeginEdit()
    {
        EditingText = Header;
        IsEditing = true;
    }

    /// <summary>Exit inline-edit mode without applying any change.</summary>
    public void CancelEdit() => IsEditing = false;

    /// <summary>Underlying data object — AnimationChainSave, AnimationFrameSave, etc.</summary>
    public object? Data { get; set; }

    public ObservableCollection<TreeNodeVm> Children { get; } = new();
}
