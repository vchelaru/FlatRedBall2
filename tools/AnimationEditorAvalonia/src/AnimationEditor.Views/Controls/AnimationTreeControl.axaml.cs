using System.Collections.ObjectModel;
using System.ComponentModel;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using FlatRedBall2.Animation.Content;

namespace AnimationEditor.Views.Controls;

/// <summary>
/// Phase 1 (#603) read-only chain/frame/shape browser: renders the tree built by
/// <see cref="TreeBuilder.BuildTree"/> and routes row selection through the already-portable,
/// already-tested <see cref="TreeBuilder.RouteNodeSelection"/>. Adds no new selection logic --
/// only the Avalonia wiring MainWindow's desktop tree already has, minus multi-select/
/// drag-reorder (see docs/BROWSER_TREE_INSPECTOR_DECISION.md for why this is a new, smaller
/// control rather than a port of MainWindow's). Phase 2 (#610) adds <see cref="Refresh"/> so
/// mutation commands can keep the tree in sync, and chain inline-rename (double-tap a chain's
/// label), mirroring MainWindow's own double-tap-to-rename. Phase 2 of #754 adds
/// <see cref="EnableContextMenu"/>: the same right-click plan desktop's MainWindow builds from
/// <see cref="TreeMenuPlanBuilder"/>, translated into real Avalonia menu items here (see
/// docs/BROWSER_TREE_CONTEXT_MENU_DECISION.md).
/// </summary>
public partial class AnimationTreeControl : UserControl
{
    private ISelectedState? _selectedState;
    private AnimationChainListSave? _acls;
    private ObservableCollection<TreeNodeVm>? _roots;
    private IAppCommands? _appCommands;
    private IObjectFinder? _objectFinder;
    private IProjectManager? _projectManager;
    private IPendingCutState? _pendingCutState;

    public AnimationTreeControl()
    {
        InitializeComponent();
        Tree.SelectionChanged += OnTreeSelectionChanged;
    }

    /// <summary>
    /// Enables double-tap-to-rename on chain nodes, routed through <see cref="IAppCommands.RenameChain"/>.
    /// Frame/rectangle/circle rename is not wired yet -- deferred to a follow-up (frames have no
    /// user-editable name in the data model; shapes need the same treatment desktop gives them
    /// via <c>SetRectProps</c>/<c>SetCircleProps</c>).
    /// </summary>
    public void EnableRename(IAppCommands appCommands) => _appCommands = appCommands;

    /// <summary>
    /// Wires a right-click context menu built from <see cref="TreeMenuPlanBuilder"/> -- the same
    /// plan desktop's MainWindow consumes. Copy/Cut/Paste route through the hosting
    /// <see cref="TopLevel"/>'s clipboard (works under both the desktop and browser Avalonia
    /// backends); Rename reuses the inline-edit mechanism <see cref="EnableRename"/> wires for
    /// chains, extended here to also cover AARectSave/CircleSave. Safe to call alongside (or
    /// instead of) <see cref="EnableRename"/> -- both may share the same
    /// <paramref name="appCommands"/> instance. Idempotent: only the first call wires the
    /// <see cref="ContextMenu"/> and pointer handler.
    /// </summary>
    public void EnableContextMenu(
        IAppCommands appCommands,
        IObjectFinder objectFinder,
        IProjectManager projectManager,
        IPendingCutState pendingCutState)
    {
        _appCommands = appCommands;
        _objectFinder = objectFinder;
        _projectManager = projectManager;
        _pendingCutState = pendingCutState;

        if (Tree.ContextMenu is not null) return;

        var contextMenu = new ContextMenu();
        contextMenu.Opening += OnTreeContextMenuOpening;
        Tree.ContextMenu = contextMenu;

        // Right-click alone doesn't move TreeView.SelectedItem (only left-click does), so
        // without this the menu would act on whatever was last left-clicked instead of the row
        // under the pointer -- mirrors desktop MainWindow's OnTreePointerPressed right-click branch.
        Tree.AddHandler(InputElement.PointerPressedEvent, OnTreeRightClick, RoutingStrategies.Tunnel);
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

    /// <summary>
    /// Snapshots every node's expand state (see <see cref="TreeBuilder.CaptureExpandState"/>) so it
    /// can be restored after a tab switch calls <see cref="InitializeServices"/> again, which
    /// otherwise rebuilds the tree from scratch and collapses everything (#687) — including frame
    /// nodes with shape children, whose expand state has no other persistence.
    /// </summary>
    public Dictionary<object, bool> CaptureExpandState() =>
        _roots is null ? new Dictionary<object, bool>() : TreeBuilder.CaptureExpandState(_roots);

    /// <summary>Restores expand state captured by <see cref="CaptureExpandState"/> onto the current tree.</summary>
    public void ApplyExpandState(IReadOnlyDictionary<object, bool>? state)
    {
        if (_roots is not null && state is not null)
            TreeBuilder.ApplyExpandState(_roots, state);
    }

    private void OnTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_selectedState is null) return;
        if (Tree.SelectedItem is not TreeNodeVm vm) return;
        TreeBuilder.RouteNodeSelection(vm.Data, _selectedState, _acls);
    }

    private void OnHeaderDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control src) return;
        var tvi = src.FindAncestorOfType<TreeViewItem>(includeSelf: true);
        if (tvi?.DataContext is not TreeNodeVm vm) return;
        if (vm.Data is not AnimationChainSave chain) return;
        e.Handled = true;
        vm.BeginEdit();
    }

    private void OnAddFrameBtnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (btn.DataContext is not TreeNodeVm vm) return;
        if (vm.Data is not AnimationChainSave chain) return;
        _appCommands?.AddFrame(chain);
        e.Handled = true;
    }

    private void OnAddFrameBtnDoubleTapped(object? sender, TappedEventArgs e) => e.Handled = true;

    /// <summary>Test seam for the inline add-frame button without simulating hover/pointer.</summary>
    internal void RaiseAddFrameForTest(TreeNodeVm chainNode)
    {
        if (chainNode.Data is not AnimationChainSave chain) return;
        _appCommands?.AddFrame(chain);
    }

    private void OnRenameKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb) return;
        if (tb.DataContext is not TreeNodeVm vm) return;

        if (e.Key == Key.Return)
        {
            e.Handled = true;
            CommitRename(vm, tb.Text ?? string.Empty);
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            vm.CancelEdit();
        }
    }

    private void OnRenameLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not TextBox tb) return;
        if (tb.DataContext is not TreeNodeVm vm) return;
        if (!vm.IsEditing) return;
        CommitRename(vm, tb.Text ?? string.Empty);
    }

    /// <summary>
    /// Applies (or discards) an in-progress rename. Exits edit mode unconditionally; calls
    /// <see cref="IAppCommands.RenameChain"/>/<see cref="IAppCommands.SetRectProps"/>/
    /// <see cref="IAppCommands.SetCircleProps"/> only when the trimmed name is non-empty and
    /// actually different, matching <c>MainWindow.CommitInlineRename</c>'s desktop behavior.
    /// Rectangle/circle rename additionally requires <see cref="EnableContextMenu"/> to have run
    /// (it needs <see cref="IObjectFinder"/> to locate the shape's containing frame).
    /// </summary>
    public void CommitRename(TreeNodeVm vm, string newName)
    {
        vm.IsEditing = false;
        if (_appCommands is null) return;

        var trimmed = newName.Trim();
        if (string.IsNullOrEmpty(trimmed)) return;

        switch (vm.Data)
        {
            case AnimationChainSave chain when trimmed != chain.Name:
                _appCommands.RenameChain(chain, trimmed);
                break;
            case AARectSave rect when trimmed != rect.Name && _objectFinder is not null:
                _appCommands.SetRectProps(
                    _objectFinder.GetAnimationFrameContaining(rect), rect, trimmed,
                    rect.X, rect.Y, rect.ScaleX, rect.ScaleY);
                break;
            case CircleSave circle when trimmed != circle.Name && _objectFinder is not null:
                _appCommands.SetCircleProps(
                    _objectFinder.GetAnimationFrameContaining(circle), circle, trimmed,
                    circle.X, circle.Y, circle.Radius);
                break;
        }
    }

    // ── Context menu ──────────────────────────────────────────────────────────

    private void OnTreeRightClick(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(Tree).Properties.IsRightButtonPressed) return;
        if (e.Source is not Control src) return;
        var tvi = src.FindAncestorOfType<TreeViewItem>(includeSelf: true);
        if (tvi?.DataContext is TreeNodeVm vm)
            Tree.SelectedItem = vm;
    }

    private void OnTreeContextMenuOpening(object? sender, CancelEventArgs e)
    {
        if (Tree.ContextMenu is null || _appCommands is null || _objectFinder is null || _projectManager is null)
            return;
        Tree.ContextMenu.Items.Clear();

        var data = (Tree.SelectedItem as TreeNodeVm)?.Data;

        Action? rename = data switch
        {
            AARectSave or CircleSave or AnimationChainSave => () => BeginRenameForNode(data),
            _ => null,
        };
        Action<bool, bool>? duplicateChainFlip = data is AnimationChainSave flipChain
            ? (flipH, flipV) => _appCommands.DuplicateChains(new[] { flipChain }, flipH, flipV)
            : null;

        var actions = new TreeMenuActions(
            Copy: () => _ = HandleCopyAsync(),
            Cut: () => _ = HandleCutAsync(),
            Paste: () => _ = HandlePasteAsync(data),
            Duplicate: HandleDuplicate,
            Delete: () => HandleDelete(data),
            Rename: rename,
            AddAnimation: AddAnimationChainAndBeginInlineRename,
            DuplicateChainFlip: duplicateChainFlip);

        var plan = TreeMenuPlanBuilder.Build(data, _appCommands, _selectedState!, _objectFinder, _projectManager, actions);
        RenderMenuPlan(plan, data);
    }

    // Thin walk over the shared plan built by TreeMenuPlanBuilder -- mirrors MainWindow's own
    // RenderMenuPlan, substituting a real menu item at the one host-slot this control's plan can
    // ever contain (see AddHostSlotItem).
    private void RenderMenuPlan(IReadOnlyList<TreeMenuItem> plan, object? nodeData)
    {
        foreach (var entry in plan)
        {
            if (entry.IsSeparator)
                AddSeparator();
            else if (entry.HostSlot is { } slot)
                AddHostSlotItem(slot, nodeData);
            else if (entry.Children is { } children)
                AddSubMenu(entry.Header!, children.Select(c => (c.Header!, c.OnClick!)).ToArray());
            else
                AddMenuItem(entry.Header!, entry.OnClick!);
        }
    }

    // Only ViewTextureInExplorer ever reaches this control's plan for a frame node -- the other
    // three host slots are chain-only dialogs with no browser dialog pattern yet (issue #756) and
    // are silently omitted here, same as MainWindow would omit them for a host that didn't
    // implement dialogs. See docs/BROWSER_TREE_CONTEXT_MENU_DECISION.md for the "Copy Texture
    // Path" remap (no filesystem to open an Explorer window onto in the browser).
    private void AddHostSlotItem(TreeMenuHostSlot slot, object? nodeData)
    {
        if (slot == TreeMenuHostSlot.ViewTextureInExplorer && nodeData is AnimationFrameSave frame)
            AddMenuItem("Copy Texture Path", () => _ = CopyTexturePathAsync(frame));
    }

    private async Task CopyTexturePathAsync(AnimationFrameSave frame)
    {
        if (string.IsNullOrEmpty(frame.TextureName)) return;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null) return;
        await clipboard.SetTextAsync(frame.TextureName);
    }

    private void AddMenuItem(string header, Action onClick)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => onClick();
        Tree.ContextMenu!.Items.Add(item);
    }

    private void AddSeparator() => Tree.ContextMenu!.Items.Add(new Separator());

    private void AddSubMenu(string header, params (string Header, Action OnClick)[] children)
    {
        var parent = new MenuItem { Header = header };
        foreach (var (childHeader, onClick) in children)
        {
            var child = new MenuItem { Header = childHeader };
            child.Click += (_, _) => onClick();
            parent.Items.Add(child);
        }
        Tree.ContextMenu!.Items.Add(parent);
    }

    // ── Copy / Cut / Paste / Duplicate / Delete ──────────────────────────────
    // Mirrors MainWindow's HandleCopyCoreAsync/HandleCutCoreAsync/HandlePasteCoreAsync/
    // HandleDuplicate/HandleDelete, minus the desktop-only bits: IsTextInputFocused (this is a
    // menu click, not a global hotkey, so no focus gate is needed), status-message error
    // surfacing, and tree-selection/expand bookkeeping (AnimationChainsChanged already drives
    // Refresh() for every mutation here -- see App.axaml.cs's wiring). SelectedChains/
    // SelectedFrames/etc. already fall back to the singular Selected* property when empty (see
    // SelectedState), so this works unchanged even though Tree is SelectionMode="Single" today.

    private async Task HandleCopyAsync()
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null) return;
        if (!SelectionCopyContext.TryGet(_selectedState!, _objectFinder!, _projectManager!.AnimationChainListSave, out var payload, out _))
            return;

        await clipboard.SetTextAsync(ClipboardPayload.SerializeFromPayload(payload));
        _pendingCutState!.Clear();
    }

    private async Task HandleCutAsync()
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null) return;
        if (!SelectionCopyContext.TryGet(_selectedState!, _objectFinder!, _projectManager!.AnimationChainListSave, out var payload, out _))
            return;

        await clipboard.SetTextAsync(ClipboardPayload.SerializeFromPayload(payload));
        _pendingCutState!.Set(payload);
    }

    private async Task HandlePasteAsync(object? nodeData)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null) return;
        var text = await clipboard.TryGetTextAsync();
        if (string.IsNullOrWhiteSpace(text)) return;

        if (!ClipboardPayload.TryDeserialize(text, out var chains, out var frames, out var rectangles, out var circles))
            return;

        var acls = _projectManager!.AnimationChainListSave;
        if (acls is null) return;

        var pendingCut = _pendingCutState!;
        bool completingCut = pendingCut.IsActive;
        if (completingCut && !pendingCut.SourcesBelongToProject(acls, _objectFinder!))
        {
            pendingCut.Clear();
            completingCut = false;
        }

        if (chains is { Count: > 0 })
        {
            if (completingCut && pendingCut.Kind != CopySelectionKind.Chain) return;
            if (completingCut) _appCommands!.PasteChainsCut(chains, pendingCut.Chains);
            else _appCommands!.PasteChains(chains);
        }
        else if (frames is { Count: > 0 })
        {
            if (completingCut && pendingCut.Kind != CopySelectionKind.Frame) return;
            var (targetChain, insertIndex) =
                PastePlacementLogic.ResolveFramePasteTarget(acls, nodeData, _objectFinder!, _selectedState);
            if (targetChain is null) return;

            foreach (var pasted in frames)
                pasted.ShapesSave ??= new ShapesSave();

            if (completingCut) _appCommands!.PasteFramesCut(targetChain, frames, insertIndex, pendingCut.Frames);
            else _appCommands!.PasteFrames(targetChain, frames, insertIndex);
        }
        else if (rectangles is { Count: > 0 } || circles is { Count: > 0 })
        {
            if (completingCut && pendingCut.Kind != CopySelectionKind.Shape) return;
            var frame = _selectedState!.SelectedFrame;
            if (frame is null) return;

            if (completingCut)
            {
                var sourceFrame = pendingCut.Shapes[0] switch
                {
                    AARectSave r => _objectFinder!.GetAnimationFrameContaining(r),
                    CircleSave c => _objectFinder!.GetAnimationFrameContaining(c),
                    _ => null,
                };
                if (sourceFrame is null) return;
                _appCommands!.PasteShapesCut(
                    frame, rectangles ?? new List<AARectSave>(), circles ?? new List<CircleSave>(),
                    pendingCut.Shapes, sourceFrame);
            }
            else
            {
                _appCommands!.PasteShapes(frame, rectangles ?? new List<AARectSave>(), circles ?? new List<CircleSave>());
            }
        }

        if (completingCut) pendingCut.Clear();
    }

    private void HandleDuplicate()
    {
        if (!SelectionCopyContext.TryGet(_selectedState!, _objectFinder!, _projectManager!.AnimationChainListSave, out var payload, out _))
            return;
        _appCommands!.DuplicateSelection(payload);
    }

    private void HandleDelete(object? nodeData)
    {
        switch (nodeData)
        {
            case AnimationChainSave chain:
                _appCommands!.DeleteAnimationChains(new List<AnimationChainSave> { chain });
                break;
            case AnimationFrameSave frame:
                _appCommands!.DeleteFrames(new List<AnimationFrameSave> { frame });
                break;
            case AARectSave rect:
                _appCommands!.DeleteShapes(_selectedState!.SelectedFrame!, new List<AARectSave> { rect }, new List<CircleSave>());
                break;
            case CircleSave circle:
                _appCommands!.DeleteShapes(_selectedState!.SelectedFrame!, new List<AARectSave>(), new List<CircleSave> { circle });
                break;
        }
    }

    private void BeginRenameForNode(object? data)
    {
        if (data is null || _roots is null) return;
        TreeBuilder.FindNodeForData(_roots, data)?.BeginEdit();
    }

    private void AddAnimationChainAndBeginInlineRename()
    {
        if (_appCommands is null || _projectManager is null) return;
        if (_projectManager.AnimationChainListSave is null)
            _projectManager.AnimationChainListSave = new AnimationChainListSave();

        var chain = _appCommands.AddNewAnimationChain();
        if (chain is null || _roots is null) return;
        TreeBuilder.FindNodeForData(_roots, chain)?.BeginEdit();
    }
}
