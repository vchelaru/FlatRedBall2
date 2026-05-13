using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AnimationEditor.Core.ViewModels;

/// <summary>
/// Lightweight view-model for a single node in the animation tree.
/// The <see cref="Data"/> field holds the underlying data object
/// (AnimationChainSave, AnimationFrameSave, AARectSave, CircleSave).
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

    /// <summary>
    /// True when this node represents an <see cref="FlatRedBall2.Animation.Content.AnimationChainSave"/>.
    /// Used by the tree item template to show the inline Add Frame button only on chain nodes.
    /// </summary>
    public bool IsChainNode { get; set; }

    private string _meta = string.Empty;
    /// <summary>Short metadata string displayed beside the node header (e.g. frame count or duration).</summary>
    public string Meta
    {
        get => _meta;
        set { if (_meta != value) { _meta = value; Notify(); } }
    }

    /// <summary>Discriminator for the icon shown in the tree item template.</summary>
    public NodeKind Kind { get; set; } = NodeKind.Frame;

    /// <summary>True when this node represents an animation frame. Set once at construction time.</summary>
    public bool IsFrameNode  { get; set; }
    /// <summary>True when this node represents an AxisAlignedRectangleSave shape. Set once at construction time.</summary>
    public bool IsRectNode   { get; set; }
    /// <summary>True when this node represents a CircleSave shape. Set once at construction time.</summary>
    public bool IsCircleNode { get; set; }

    public ObservableCollection<TreeNodeVm> Children { get; } = new();

    /// <summary>
    /// Sets <see cref="IsExpanded"/> on <paramref name="node"/> and every descendant.
    /// </summary>
    public static void SetExpandedRecursive(TreeNodeVm node, bool expanded)
    {
        node.IsExpanded = expanded;
        foreach (var child in node.Children)
            SetExpandedRecursive(child, expanded);
    }
}

/// <summary>Identifies the type of data a <see cref="TreeNodeVm"/> represents, for icon selection.</summary>
public enum NodeKind
{
    Chain,
    Frame,
    RectShape,
    CircleShape,
}
