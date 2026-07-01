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

    private bool _pinnedVisible = true;
    /// <summary>
    /// Whether this row is shown by the ANIMATIONS search filter. Defaults to
    /// <c>true</c> (no filter). Only chain (root) nodes are toggled — frame/shape
    /// nodes stay <c>true</c> and are hidden only when their parent chain is.
    /// The tree row binds <c>IsVisible</c> to <c>PinnedVisible || IsSelected</c>, so
    /// the currently-selected chain stays visible even when filtered out.
    /// </summary>
    public bool PinnedVisible
    {
        get => _pinnedVisible;
        set { if (_pinnedVisible != value) { _pinnedVisible = value; Notify(); } }
    }

    private bool _isPendingCut;
    /// <summary>True while this item is part of a pending cut (Ctrl+X, not yet pasted).</summary>
    public bool IsPendingCut
    {
        get => _isPendingCut;
        set { if (_isPendingCut != value) { _isPendingCut = value; Notify(); } }
    }

    /// <summary>
    /// True when this is a frame node that currently has no shape children.
    /// Used by the tree template to show a spacer that matches the chevron width,
    /// keeping frame icons horizontally aligned regardless of whether they have shapes.
    /// </summary>
    public bool IsLeafFrameNode => IsFrameNode && Children.Count == 0;

    private object? _thumbnail;
    /// <summary>
    /// First-frame thumbnail for a chain node, set by the App layer once the texture is
    /// resolved. Holds an <c>Avalonia.Media.Imaging.Bitmap</c> at runtime; <c>null</c> for
    /// chains with no frames (or an unresolvable first-frame texture), in which case the
    /// tree shows the generic chain icon. Always <c>null</c> on non-chain nodes.
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
                Notify(nameof(HasThumbnail));
                Notify(nameof(ShowGenericChainIcon));
            }
        }
    }

    /// <summary>True when a first-frame <see cref="Thumbnail"/> is available to show.</summary>
    public bool HasThumbnail => _thumbnail is not null;

    /// <summary>
    /// True when the tree should show the generic chain icon — a chain node that has no
    /// first-frame thumbnail. Drives the SVG-vs-thumbnail swap in the tree item template.
    /// </summary>
    public bool ShowGenericChainIcon => IsChainNode && _thumbnail is null;

    /// <summary>
    /// Signature of the first frame the current <see cref="Thumbnail"/> was rendered from.
    /// The App layer compares this against <see cref="ThumbnailSource.FromChain"/> to skip
    /// regenerating a chain icon whose first-frame visual is unchanged.
    /// </summary>
    public ThumbnailSource? ThumbnailSource { get; set; }

    public ObservableCollection<TreeNodeVm> Children { get; }

    public TreeNodeVm()
    {
        Children = new ObservableCollection<TreeNodeVm>();
        Children.CollectionChanged += (_, _) => Notify(nameof(IsLeafFrameNode));
    }

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
