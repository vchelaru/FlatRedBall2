using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.ViewModels;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FlatRedBall.Content.AnimationChain;
using FlatRedBall.Content.Math.Geometry;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Headless Avalonia tests for TV05 (multi-select TreeView) and TV06 (context menu items).
///
/// Each test creates a fresh MainWindow headlessly, resets singleton data state,
/// and exercises the tree view in isolation.
/// </summary>
public class HeadlessTreeViewTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a headless <see cref="MainWindow"/>, shows it, and returns it.
    /// Callers must call <see cref="Window.Close"/> when done.
    /// </summary>
    private static (MainWindow Window, TestServices Ctx) CreateWindow()
    {
        // Reset data state so each test starts clean.
        // Note: we deliberately do NOT override DoOnUiThread — the window sets it
        // to InvokeAsync which is correct when running on the Avalonia UI thread.
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName = null;
        ctx.SelectedState.SelectedChain = null;
        ctx.AppCommands.ConfirmAsync = (_, _) => Task.FromResult(true);
        ctx.AppCommands.FileDialogService = NullFileDialogService.Instance;

        var window = ctx.CreateMainWindow();
        window.Show();
        return (window, ctx);
    }

    /// <summary>Returns the AnimTree control from a window.</summary>
    private static TreeView GetTree(MainWindow w)
        => w.FindControl<TreeView>("AnimTree")
           ?? throw new InvalidOperationException("AnimTree control not found");

    /// <summary>
    /// Gets the <c>_treeRoots</c> ObservableCollection via the tree's ItemsSource.
    /// </summary>
    private static ObservableCollection<TreeNodeVm> GetRoots(TreeView tree)
        => (ObservableCollection<TreeNodeVm>)(tree.ItemsSource
           ?? throw new InvalidOperationException("AnimTree has no ItemsSource"));

    /// <summary>
    /// Invokes the private <c>OnTreeContextMenuOpening</c> method to populate
    /// the context menu without needing a real pointer event.
    /// </summary>
    private static void TriggerContextMenuOpening(MainWindow window)
    {
        var method = typeof(MainWindow).GetMethod(
            "OnTreeContextMenuOpening",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("OnTreeContextMenuOpening not found via reflection");

        method.Invoke(window, [null, new CancelEventArgs()]);
    }

    /// <summary>Invokes the private <c>RefreshTreeView</c> method directly.</summary>
    private static void TriggerRefreshTreeView(MainWindow window)
    {
        var method = typeof(MainWindow).GetMethod(
            "RefreshTreeView",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("RefreshTreeView not found via reflection");

        method.Invoke(window, null);
    }

    private static List<string?> ContextMenuHeaders(MainWindow window)
    {
        var tree = GetTree(window);
        return tree.ContextMenu!.Items
            .OfType<MenuItem>()
            .Select(m => m.Header?.ToString())
            .ToList();
    }

    // ── TV05: Multi-select ────────────────────────────────────────────────────

    [AvaloniaFact]
    public void TreeView_SelectionMode_IsMultiple()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var tree = GetTree(window);
            Assert.Equal(SelectionMode.Multiple, tree.SelectionMode);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void TreeView_SelectTwoNodes_BothAppearInSelectedItems()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var tree  = GetTree(window);
            var roots = GetRoots(tree);

            var vm1 = new TreeNodeVm { Header = "Walk", Data = new AnimationChainSave { Name = "Walk" } };
            var vm2 = new TreeNodeVm { Header = "Run",  Data = new AnimationChainSave { Name = "Run"  } };
            roots.Add(vm1);
            roots.Add(vm2);

            tree.SelectedItems!.Add(vm1);
            tree.SelectedItems.Add(vm2);

            Assert.Equal(2, tree.SelectedItems.Count);
            Assert.Contains(vm1, tree.SelectedItems!.Cast<TreeNodeVm>());
            Assert.Contains(vm2, tree.SelectedItems.Cast<TreeNodeVm>());
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void TreeView_DeselectOne_LeavesSingleItemSelected()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var tree  = GetTree(window);
            var roots = GetRoots(tree);

            var vm1 = new TreeNodeVm { Header = "A", Data = new AnimationChainSave { Name = "A" } };
            var vm2 = new TreeNodeVm { Header = "B", Data = new AnimationChainSave { Name = "B" } };
            roots.Add(vm1);
            roots.Add(vm2);

            tree.SelectedItems!.Add(vm1);
            tree.SelectedItems.Add(vm2);
            tree.SelectedItems.Remove(vm1);

            Assert.Single(tree.SelectedItems!.Cast<TreeNodeVm>());
            Assert.Contains(vm2, tree.SelectedItems.Cast<TreeNodeVm>());
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void TreeView_ClearSelectedItems_ResultsInEmptySelection()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var tree  = GetTree(window);
            var roots = GetRoots(tree);

            var vm1 = new TreeNodeVm { Header = "A", Data = new AnimationChainSave { Name = "A" } };
            roots.Add(vm1);
            tree.SelectedItems!.Add(vm1);
            Assert.Single(tree.SelectedItems!.Cast<TreeNodeVm>());

            tree.SelectedItems.Clear();
            Assert.Empty(tree.SelectedItems.Cast<TreeNodeVm>());
        }
        finally { window.Close(); }
    }

    // ── TV06: Context menu items ──────────────────────────────────────────────

    [AvaloniaFact]
    public void ContextMenu_NoSelection_ContainsAddAnimationChain()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var tree = GetTree(window);
            tree.SelectedItem = null;  // ensure nothing selected

            TriggerContextMenuOpening(window);

            Assert.Contains("Add AnimationChain", ContextMenuHeaders(window));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ContextMenu_ChainSelected_ContainsDeleteChain()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var tree  = GetTree(window);
            var roots = GetRoots(tree);

            var chain = new AnimationChainSave { Name = "Walk" };
            var vm    = new TreeNodeVm { Header = "Walk", Data = chain };
            roots.Add(vm);
            tree.SelectedItem = vm;

            TriggerContextMenuOpening(window);

            Assert.Contains("Delete AnimationChain", ContextMenuHeaders(window));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ContextMenu_ChainSelected_ContainsAddFrame()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var tree  = GetTree(window);
            var roots = GetRoots(tree);

            var chain = new AnimationChainSave { Name = "Run" };
            var vm    = new TreeNodeVm { Header = "Run", Data = chain };
            roots.Add(vm);
            tree.SelectedItem = vm;

            TriggerContextMenuOpening(window);

            Assert.Contains("Add Frame", ContextMenuHeaders(window));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ContextMenu_ChainSelected_ContainsMoveAndFlipItems()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var tree  = GetTree(window);
            var roots = GetRoots(tree);

            var chain = new AnimationChainSave { Name = "Idle" };
            var vm    = new TreeNodeVm { Header = "Idle", Data = chain };
            roots.Add(vm);
            tree.SelectedItem = vm;

            TriggerContextMenuOpening(window);

            var headers = ContextMenuHeaders(window);
            Assert.Contains("Flip Horizontally", headers);
            Assert.Contains("Flip Vertically",   headers);
            Assert.Contains("^  Move Up",         headers);
            Assert.Contains("v  Move Down",        headers);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ContextMenu_FrameSelected_ContainsDeleteFrame()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var tree  = GetTree(window);
            var roots = GetRoots(tree);

            var frame = new AnimationFrameSave { TextureName = "Tex.png", ShapeCollectionSave = new ShapeCollectionSave() };
            var vm    = new TreeNodeVm { Header = "Frame 0", Data = frame };
            roots.Add(vm);
            tree.SelectedItem = vm;

            TriggerContextMenuOpening(window);

            Assert.Contains("Delete Frame", ContextMenuHeaders(window));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ContextMenu_FrameSelected_ContainsShapeItems()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var tree  = GetTree(window);
            var roots = GetRoots(tree);

            var frame = new AnimationFrameSave { TextureName = "Tex.png", ShapeCollectionSave = new ShapeCollectionSave() };
            var vm    = new TreeNodeVm { Header = "Frame 0", Data = frame };
            roots.Add(vm);
            tree.SelectedItem = vm;

            TriggerContextMenuOpening(window);

            var headers = ContextMenuHeaders(window);
            Assert.Contains("Add AxisAlignedRectangle", headers);
            Assert.Contains("Add Circle",               headers);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ContextMenu_RectSelected_ContainsDeleteRectangle()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var tree  = GetTree(window);
            var roots = GetRoots(tree);

            var rect = new AxisAlignedRectangleSave();
            var vm   = new TreeNodeVm { Header = "Rect", Data = rect };
            roots.Add(vm);
            tree.SelectedItem = vm;

            TriggerContextMenuOpening(window);

            Assert.Contains("Delete Rectangle", ContextMenuHeaders(window));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ContextMenu_CircleSelected_ContainsDeleteCircle()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var tree  = GetTree(window);
            var roots = GetRoots(tree);

            var circle = new CircleSave();
            var vm     = new TreeNodeVm { Header = "Circle", Data = circle };
            roots.Add(vm);
            tree.SelectedItem = vm;

            TriggerContextMenuOpening(window);

            Assert.Contains("Delete Circle", ContextMenuHeaders(window));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ContextMenu_AlwaysContainsSortAlphabetically()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            // Test both: no selection case
            var tree = GetTree(window);
            tree.SelectedItem = null;
            TriggerContextMenuOpening(window);
            Assert.Contains("Sort Animations Alphabetically", ContextMenuHeaders(window));
        }
        finally { window.Close(); }
    }

    /// <summary>
    /// Regression test for Bug #2: when the selection is changed to a chain (simulating what
    /// OnTreePointerPressed does on right-click) the context menu must show chain items,
    /// NOT the items for the previously selected frame.
    /// </summary>
    [AvaloniaFact]
    public void ContextMenu_WhenSelectionChangedToChainBeforeOpen_ShowsChainNotFrameItems()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var tree  = GetTree(window);
            var roots = GetRoots(tree);

            var chain   = new AnimationChainSave { Name = "Walk" };
            var frame   = new AnimationFrameSave { TextureName = "Tex.png", ShapeCollectionSave = new ShapeCollectionSave() };
            var chainVm = new TreeNodeVm { Header = "Walk",    Data = chain };
            var frameVm = new TreeNodeVm { Header = "Frame 0", Data = frame };
            roots.Add(chainVm);
            roots.Add(frameVm);

            // Simulate a frame being selected first (the "old" selection).
            tree.SelectedItem = frameVm;

            // Simulate OnTreePointerPressed firing: right-click on chain changes selection.
            tree.SelectedItem = chainVm;

            // Context menu should reflect the chain, not the old frame.
            TriggerContextMenuOpening(window);

            var headers = ContextMenuHeaders(window);
            Assert.Contains("Delete AnimationChain", headers);
            Assert.DoesNotContain("Delete Frame", headers);
        }
        finally { window.Close(); }
    }

    // ── TV08: Inline "Add Frame" button on chain nodes ────────────────────────

    [AvaloniaFact]
    public void ChainNode_AfterRefresh_HasIsChainNodeTrue()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            TriggerRefreshTreeView(window);
            Dispatcher.UIThread.RunJobs();

            var roots = GetRoots(GetTree(window));
            Assert.Single(roots);
            Assert.True(roots[0].IsChainNode);
        }
        finally { window.Close(); }
    }

    // ── Tree selection sync with shapes ───────────────────────────────────────

    [AvaloniaFact]
    public void SelectCircle_TreeViewHighlightsCircleNode_NotFrameNode()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            // Arrange: chain → frame → circle in ProjectManager
            var circle = new CircleSave { Name = "CircleInstance", Radius = 8f };
            var frame  = new AnimationFrameSave
            {
                TextureName         = "Tex.png",
                ShapeCollectionSave = new ShapeCollectionSave()
            };
            frame.ShapeCollectionSave.CircleSaves.Add(circle);
            var chain  = new AnimationChainSave { Name = "Run" };
            chain.Frames.Add(frame);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            ctx.ProjectManager.FileName = "test.achx";

            TriggerRefreshTreeView(window);
            Dispatcher.UIThread.RunJobs();

            // First select the frame (simulates the normal tree-click flow)
            ctx.SelectedState.SelectedFrame = frame;
            Dispatcher.UIThread.RunJobs();

            var tree = GetTree(window);
            var roots = GetRoots(tree);
            var frameNode  = roots[0].Children[0];          // chain → frame
            var circleNode = frameNode.Children[0];          // frame → circle

            Assert.Same(frameNode,  tree.SelectedItem);

            // Act: select the circle (e.g. from tree click or preview click)
            ctx.SelectedState.SelectedCircle = circle;
            Dispatcher.UIThread.RunJobs();

            // Assert: tree must highlight the circle node, not the frame node
            Assert.Same(circleNode, tree.SelectedItem);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void SelectRect_TreeViewHighlightsRectNode_NotFrameNode()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var rect  = new AxisAlignedRectangleSave { Name = "HitBox" };
            var frame = new AnimationFrameSave
            {
                TextureName         = "Tex.png",
                ShapeCollectionSave = new ShapeCollectionSave()
            };
            frame.ShapeCollectionSave.AxisAlignedRectangleSaves.Add(rect);
            var chain = new AnimationChainSave { Name = "Idle" };
            chain.Frames.Add(frame);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            ctx.ProjectManager.FileName = "test.achx";

            TriggerRefreshTreeView(window);
            Dispatcher.UIThread.RunJobs();

            ctx.SelectedState.SelectedFrame = frame;
            Dispatcher.UIThread.RunJobs();

            var tree     = GetTree(window);
            var frameNode = GetRoots(tree)[0].Children[0];
            var rectNode  = frameNode.Children[0];

            Assert.Same(frameNode, tree.SelectedItem);

            ctx.SelectedState.SelectedRectangle = rect;
            Dispatcher.UIThread.RunJobs();

            Assert.Same(rectNode, tree.SelectedItem);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void InlineAddFrameBtn_Click_AddsFrameToChain_AndSelectsIt()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Run" };
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            // Wire SaveCurrentAnimationChainList to a no-op so no file I/O happens
            ctx.AppCommands.DoOnUiThread = a => a();

            TriggerRefreshTreeView(window);
            Dispatcher.UIThread.RunJobs();

            // Find the add-frame button rendered inside the tree visual tree
            var tree = GetTree(window);
            var addBtn = tree.GetVisualDescendants()
                .OfType<Button>()
                .FirstOrDefault(b => b.Classes.Contains("add-frame-btn") && b.IsVisible);

            Assert.NotNull(addBtn);

            // Simulate a click
            addBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            Assert.Single(chain.Frames);
            Assert.Same(chain.Frames[0], ctx.SelectedState.SelectedFrame);
        }
        finally { window.Close(); }
    }
}
