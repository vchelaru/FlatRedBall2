using System.Collections.ObjectModel;
using AnimationEditor.Core;
using AnimationEditor.Core.ViewModels;
using Avalonia.Controls;
using FlatRedBall2.Animation.Content;

namespace AnimationEditor.Views.Controls;

/// <summary>
/// Phase 1 (#603) read-only chain/frame/shape browser: renders the tree built by
/// <see cref="TreeBuilder.BuildTree"/> and routes row selection through the already-portable,
/// already-tested <see cref="TreeBuilder.RouteNodeSelection"/>. Adds no new selection logic --
/// only the Avalonia wiring MainWindow's desktop tree already has, minus editing/multi-select/
/// drag-reorder (see docs/BROWSER_TREE_INSPECTOR_DECISION.md for why this is a new, smaller
/// control rather than a port of MainWindow's). Phase 2 (#610) adds <see cref="Refresh"/> so
/// mutation commands can keep the tree in sync.
/// </summary>
public partial class AnimationTreeControl : UserControl
{
    private ISelectedState? _selectedState;
    private AnimationChainListSave? _acls;
    private ObservableCollection<TreeNodeVm>? _roots;

    public AnimationTreeControl()
    {
        InitializeComponent();
        Tree.SelectionChanged += OnTreeSelectionChanged;
    }

    /// <summary>Exposes the underlying TreeView for host layout/styling and test inspection.</summary>
    public TreeView TreeView => Tree;

    /// <summary>
    /// Builds the tree from <paramref name="acls"/> (or clears it when <c>null</c>) and starts
    /// routing row selection into <paramref name="selectedState"/>. Call again after loading a
    /// different file -- the browser build has no persisted expand-state yet, so every chain
    /// defaults to expanded (matches <see cref="TreeBuilder.BuildTree"/>'s no-saved-state default).
    /// </summary>
    public void InitializeServices(ISelectedState selectedState, AnimationChainListSave? acls)
    {
        _selectedState = selectedState;
        _acls = acls;
        _roots = acls is null ? null : new ObservableCollection<TreeNodeVm>(TreeBuilder.BuildTree(acls));
        Tree.ItemsSource = _roots;
    }

    /// <summary>
    /// Diff-updates the tree to match the current <see cref="AnimationChainListSave"/> after a
    /// mutation (add/delete/rename/reorder chain or frame), preserving existing nodes' expand
    /// state and selection instead of rebuilding from scratch -- the same
    /// <see cref="TreeBuilder.SyncChainsInto"/> desktop's <c>MainWindow.RefreshTreeView</c> uses.
    /// No-op if no file is loaded ((<see cref="InitializeServices"/> was called with a null acls).
    /// </summary>
    public void Refresh()
    {
        if (_acls is null || _roots is null) return;
        TreeBuilder.SyncChainsInto(_roots, _acls.AnimationChains);
    }

    private void OnTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_selectedState is null) return;
        if (Tree.SelectedItem is not TreeNodeVm vm) return;
        TreeBuilder.RouteNodeSelection(vm.Data, _selectedState, _acls);
    }
}
