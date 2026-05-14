using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.ViewModels;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FlatRedBall2.Animation.Content;
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

            Assert.Contains("Add Animation", ContextMenuHeaders(window));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void AddChainButton_Click_StartsInlineRenameOnCreatedChain()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            // Avoid dialog interaction if AddChain still routes through PromptStringAsync.
            ctx.AppCommands.PromptStringAsync = (_, _, initial) => Task.FromResult<string?>(initial);

            var addChainBtn = window.FindControl<Button>("AddChainBtn")
                ?? throw new InvalidOperationException("AddChainBtn not found");
            addChainBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            var roots = GetRoots(GetTree(window));
            Assert.Single(roots);
            Assert.True(roots[0].IsEditing);
            Assert.Equal(roots[0].Header, roots[0].EditingText);
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

            Assert.Contains("Delete Animation", ContextMenuHeaders(window));
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

            // Three chains so the middle one has all four move items available.
            var chain0 = new AnimationChainSave { Name = "Idle" };
            var chain1 = new AnimationChainSave { Name = "Walk" };
            var chain2 = new AnimationChainSave { Name = "Run"  };
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.AddRange([chain0, chain1, chain2]);

            var vm = new TreeNodeVm { Header = "Walk", Data = chain1 };
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
    public void ContextMenu_Chain_SingleChainList_HidesAllMoveItems()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var tree  = GetTree(window);
            var roots = GetRoots(tree);

            var chain = new AnimationChainSave { Name = "Idle" };
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            var vm = new TreeNodeVm { Header = "Idle", Data = chain };
            roots.Add(vm);
            tree.SelectedItem = vm;

            TriggerContextMenuOpening(window);

            var headers = ContextMenuHeaders(window);
            Assert.DoesNotContain("^^ Move To Top",   headers);
            Assert.DoesNotContain("^  Move Up",        headers);
            Assert.DoesNotContain("v  Move Down",      headers);
            Assert.DoesNotContain("vv Move To Bottom", headers);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ContextMenu_Chain_FirstChainInList_HidesUpAndTopItems()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var tree  = GetTree(window);
            var roots = GetRoots(tree);

            var chain0 = new AnimationChainSave { Name = "Idle" };
            var chain1 = new AnimationChainSave { Name = "Walk" };
            var chain2 = new AnimationChainSave { Name = "Run"  };
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.AddRange([chain0, chain1, chain2]);

            var vm = new TreeNodeVm { Header = "Idle", Data = chain0 };
            roots.Add(vm);
            tree.SelectedItem = vm;

            TriggerContextMenuOpening(window);

            var headers = ContextMenuHeaders(window);
            Assert.DoesNotContain("^^ Move To Top",   headers);
            Assert.DoesNotContain("^  Move Up",        headers);
            Assert.Contains("v  Move Down",            headers);
            Assert.Contains("vv Move To Bottom",       headers);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ContextMenu_Chain_LastChainInList_HidesDownAndBottomItems()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var tree  = GetTree(window);
            var roots = GetRoots(tree);

            var chain0 = new AnimationChainSave { Name = "Idle" };
            var chain1 = new AnimationChainSave { Name = "Walk" };
            var chain2 = new AnimationChainSave { Name = "Run"  };
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.AddRange([chain0, chain1, chain2]);

            var vm = new TreeNodeVm { Header = "Run", Data = chain2 };
            roots.Add(vm);
            tree.SelectedItem = vm;

            TriggerContextMenuOpening(window);

            var headers = ContextMenuHeaders(window);
            Assert.Contains("^^ Move To Top",          headers);
            Assert.Contains("^  Move Up",              headers);
            Assert.DoesNotContain("v  Move Down",      headers);
            Assert.DoesNotContain("vv Move To Bottom", headers);
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

            var frame = new AnimationFrameSave { TextureName = "Tex.png", ShapesSave = new ShapesSave() };
            var vm    = new TreeNodeVm { Header = "Frame 0", Data = frame };
            roots.Add(vm);
            tree.SelectedItem = vm;

            TriggerContextMenuOpening(window);

            Assert.Contains("Delete Frame", ContextMenuHeaders(window));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ContextMenu_Frame_SingleFrameChain_HidesAllMoveItems()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var tree  = GetTree(window);
            var roots = GetRoots(tree);

            var chain = new AnimationChainSave { Name = "OnlyOne" };
            var frame = new AnimationFrameSave { TextureName = "A.png", ShapesSave = new ShapesSave() };
            chain.Frames.Add(frame);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            var frameVm = new TreeNodeVm { Header = "Frame 0", Data = frame };
            roots.Add(frameVm);
            tree.SelectedItem = frameVm;

            TriggerContextMenuOpening(window);

            var headers = ContextMenuHeaders(window);
            Assert.DoesNotContain("^^ Move To Top",   headers);
            Assert.DoesNotContain("^  Move Up",        headers);
            Assert.DoesNotContain("v  Move Down",      headers);
            Assert.DoesNotContain("vv Move To Bottom", headers);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ContextMenu_Frame_FirstFrameOfMultiFrameChain_HidesUpAndTopItems()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var tree  = GetTree(window);
            var roots = GetRoots(tree);

            var chain  = new AnimationChainSave { Name = "Walk" };
            var frame0 = new AnimationFrameSave { TextureName = "A.png", ShapesSave = new ShapesSave() };
            var frame1 = new AnimationFrameSave { TextureName = "B.png", ShapesSave = new ShapesSave() };
            var frame2 = new AnimationFrameSave { TextureName = "C.png", ShapesSave = new ShapesSave() };
            chain.Frames.Add(frame0);
            chain.Frames.Add(frame1);
            chain.Frames.Add(frame2);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            var frameVm = new TreeNodeVm { Header = "Frame 0", Data = frame0 };
            roots.Add(frameVm);
            tree.SelectedItem = frameVm;

            TriggerContextMenuOpening(window);

            var headers = ContextMenuHeaders(window);
            Assert.DoesNotContain("^^ Move To Top",   headers);
            Assert.DoesNotContain("^  Move Up",        headers);
            Assert.Contains("v  Move Down",            headers);
            Assert.Contains("vv Move To Bottom",       headers);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ContextMenu_Frame_LastFrameOfMultiFrameChain_HidesDownAndBottomItems()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var tree  = GetTree(window);
            var roots = GetRoots(tree);

            var chain  = new AnimationChainSave { Name = "Walk" };
            var frame0 = new AnimationFrameSave { TextureName = "A.png", ShapesSave = new ShapesSave() };
            var frame1 = new AnimationFrameSave { TextureName = "B.png", ShapesSave = new ShapesSave() };
            var frame2 = new AnimationFrameSave { TextureName = "C.png", ShapesSave = new ShapesSave() };
            chain.Frames.Add(frame0);
            chain.Frames.Add(frame1);
            chain.Frames.Add(frame2);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            var frameVm = new TreeNodeVm { Header = "Frame 2", Data = frame2 };
            roots.Add(frameVm);
            tree.SelectedItem = frameVm;

            TriggerContextMenuOpening(window);

            var headers = ContextMenuHeaders(window);
            Assert.Contains("^^ Move To Top",          headers);
            Assert.Contains("^  Move Up",              headers);
            Assert.DoesNotContain("v  Move Down",      headers);
            Assert.DoesNotContain("vv Move To Bottom", headers);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ContextMenu_Frame_MiddleFrameOfChain_ShowsAllMoveItems()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var tree  = GetTree(window);
            var roots = GetRoots(tree);

            var chain  = new AnimationChainSave { Name = "Walk" };
            var frame0 = new AnimationFrameSave { TextureName = "A.png", ShapesSave = new ShapesSave() };
            var frame1 = new AnimationFrameSave { TextureName = "B.png", ShapesSave = new ShapesSave() };
            var frame2 = new AnimationFrameSave { TextureName = "C.png", ShapesSave = new ShapesSave() };
            chain.Frames.Add(frame0);
            chain.Frames.Add(frame1);
            chain.Frames.Add(frame2);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            var frameVm = new TreeNodeVm { Header = "Frame 1", Data = frame1 };
            roots.Add(frameVm);
            tree.SelectedItem = frameVm;

            TriggerContextMenuOpening(window);

            var headers = ContextMenuHeaders(window);
            Assert.Contains("^^ Move To Top",   headers);
            Assert.Contains("^  Move Up",        headers);
            Assert.Contains("v  Move Down",      headers);
            Assert.Contains("vv Move To Bottom", headers);
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

            var frame = new AnimationFrameSave { TextureName = "Tex.png", ShapesSave = new ShapesSave() };
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

            var rect = new AARectSave();
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
            var frame   = new AnimationFrameSave { TextureName = "Tex.png", ShapesSave = new ShapesSave() };
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
            Assert.Contains("Delete Animation", headers);
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
                ShapesSave = new ShapesSave()
            };
            frame.ShapesSave.CircleSaves.Add(circle);
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
            var rect  = new AARectSave { Name = "HitBox" };
            var frame = new AnimationFrameSave
            {
                TextureName         = "Tex.png",
                ShapesSave = new ShapesSave()
            };
            frame.ShapesSave.AARectSaves.Add(rect);
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

    [AvaloniaFact]
    public void RefreshChainNode_UpdatesChainMetaFrameCount()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Run" };
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            TriggerRefreshTreeView(window);
            Dispatcher.UIThread.RunJobs();

            var roots = GetRoots(GetTree(window));
            Assert.Equal("0 fr", roots[0].Meta);

            chain.Frames.Add(new AnimationFrameSave { TextureName = "Tex.png", ShapesSave = new ShapesSave() });
            ctx.AppCommands.RefreshTreeNode(chain);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("1 fr", roots[0].Meta);
        }
        finally { window.Close(); }
    }

    // ── Scrollbar gutter ─────────────────────────────────────────────────────

    /// <summary>
    /// The vertical scroll bar must always be shown (Visible) so the gutter is always
    /// reserved and the inline "+" add-frame button always has a stable position.
    /// See issue #183.
    /// </summary>
    [AvaloniaFact]
    public void AnimTree_VerticalScrollBarVisibility_IsVisible()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var tree = GetTree(window);
            Assert.Equal(ScrollBarVisibility.Visible,
                         ScrollViewer.GetVerticalScrollBarVisibility(tree));
        }
        finally { window.Close(); }
    }

    /// <summary>
    /// AllowAutoHide must be false so Avalonia uses gutter layout (scrollbar takes up
    /// real layout space) instead of overlay mode (scrollbar floats on top of content).
    /// In overlay mode the scrollbar expands on hover and covers the inline "+" button
    /// even when VerticalScrollBarVisibility is Visible. See issue #183.
    /// </summary>
    [AvaloniaFact]
    public void AnimTree_AllowAutoHide_IsFalse()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var tree = GetTree(window);
            Assert.False(ScrollViewer.GetAllowAutoHide(tree));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void MoveChain_PreservesChainCollapseState()
    {
        // Regression (#237): reordering chains rebuilt the whole tree and re-expanded
        // every chain. MoveChain must leave each chain's collapse state untouched.
        var (window, ctx) = CreateWindow();
        try
        {
            var chainA = new AnimationChainSave { Name = "Walk" };
            var chainB = new AnimationChainSave { Name = "Run" };
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chainA);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chainB);

            TriggerRefreshTreeView(window);
            Dispatcher.UIThread.RunJobs();

            var roots = GetRoots(GetTree(window));
            var chainAVm = roots[0];
            chainAVm.IsExpanded = false;  // user collapses "Walk"

            ctx.AppCommands.MoveChain(chainA, +1);
            Dispatcher.UIThread.RunJobs();

            // Same VM instance moved to index 1, still collapsed.
            Assert.Same(chainAVm, roots[1]);
            Assert.False(chainAVm.IsExpanded);
            // The other chain keeps its (default expanded) state.
            Assert.True(roots[0].IsExpanded);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void MoveFrame_PreservesFrameVmIdentityAndSelection()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain  = new AnimationChainSave { Name = "Walk" };
            var frameA = new AnimationFrameSave { TextureName = "a.png" };
            var frameB = new AnimationFrameSave { TextureName = "b.png" };
            chain.Frames.Add(frameA);
            chain.Frames.Add(frameB);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            TriggerRefreshTreeView(window);
            Dispatcher.UIThread.RunJobs();

            var tree      = GetTree(window);
            var chainNode = GetRoots(tree)[0];
            var frameAVm  = chainNode.Children[0];
            Assert.Same(frameA, frameAVm.Data);

            // Select frameA in the tree and mark it expanded
            tree.SelectedItems!.Add(frameAVm);
            frameAVm.IsExpanded = true;

            // Move frameA down one position
            ctx.AppCommands.MoveFrame(frameA, chain, +1);
            Dispatcher.UIThread.RunJobs();

            // Same VM instance should now be at index 1
            Assert.Same(frameAVm, chainNode.Children[1]);
            // Avalonia selection must survive (relies on object identity)
            Assert.Contains(frameAVm, tree.SelectedItems.Cast<TreeNodeVm>());
            // Expanded state must be preserved
            Assert.True(frameAVm.IsExpanded);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void PropFrameLen_ValueChanged_UpdatesFrameMetaImmediately()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var frame = new AnimationFrameSave
            {
                TextureName = "Tex.png",
                FrameLength = 0.1f,
                ShapesSave = new ShapesSave()
            };
            var chain = new AnimationChainSave { Name = "Run" };
            chain.Frames.Add(frame);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            ctx.ProjectManager.FileName = "test.achx";

            TriggerRefreshTreeView(window);
            Dispatcher.UIThread.RunJobs();

            ctx.SelectedState.SelectedFrame = frame;
            Dispatcher.UIThread.RunJobs();

            var frameLen = window.FindControl<NumericUpDown>("PropFrameLen")
                ?? throw new InvalidOperationException("PropFrameLen not found");
            frameLen.Value = 0.55m;
            Dispatcher.UIThread.RunJobs();

            var roots = GetRoots(GetTree(window));
            Assert.Equal("0.55s", roots[0].Children[0].Meta);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RefreshTreeView_AfterChainAdded_PreservesExistingCollapseState()
    {
        // Regression (#237): copy/paste appends a chain then calls RefreshTreeView.
        // The full rebuild re-expanded every pre-existing chain. RefreshTreeView
        // must preserve the collapse state of chains that already had a tree node.
        var (window, ctx) = CreateWindow();
        try
        {
            var existing = new AnimationChainSave { Name = "Walk" };
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(existing);

            TriggerRefreshTreeView(window);
            Dispatcher.UIThread.RunJobs();

            var roots = GetRoots(GetTree(window));
            roots[0].IsExpanded = false;  // user collapses "Walk"

            // Simulate the paste path: a new chain is appended, then the tree refreshes.
            var pasted = new AnimationChainSave { Name = "Walk2" };
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(pasted);
            TriggerRefreshTreeView(window);
            Dispatcher.UIThread.RunJobs();

            Assert.False(roots[0].IsExpanded);   // pre-existing chain stays collapsed
            Assert.Same(pasted, roots[1].Data);
            Assert.True(roots[1].IsExpanded);    // pasted chain defaults to expanded
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RenameChain_DoesNotCollapseChainNode()
    {
        // Regression: double-click rename was collapsing the chain node because
        // OnAnimTreeDoubleTapped toggled IsExpanded even when the TextBlock's
        // DoubleTapped handler had already handled the event.
        // This test verifies that the rename flow (AppCommands.RenameChain →
        // RefreshChainNode) leaves IsExpanded untouched.
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            chain.Frames.Add(new AnimationFrameSave { TextureName = "a.png" });
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            TriggerRefreshTreeView(window);
            Dispatcher.UIThread.RunJobs();

            var chainNode = GetRoots(GetTree(window))[0];
            chainNode.IsExpanded = true;

            ctx.AppCommands.RenameChain(chain, "Walk Renamed");
            Dispatcher.UIThread.RunJobs();

            Assert.True(chainNode.IsExpanded, "Chain node must stay expanded after inline rename.");
            Assert.Equal("Walk Renamed", chainNode.Header);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RefreshTreeNode_ForFrame_DoesNotCollapseParentChainNode()
    {
        // Regression: refreshing a frame's tree node was collapsing its parent chain.
        // RefreshTreeNode(frame) runs from many live paths (here driven via
        // SetFrameTextureName) and must leave the parent's IsExpanded intact.
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            var frame = new AnimationFrameSave { TextureName = "a.png" };
            chain.Frames.Add(frame);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            TriggerRefreshTreeView(window);
            Dispatcher.UIThread.RunJobs();

            var chainNode = GetRoots(GetTree(window))[0];
            chainNode.IsExpanded = true;

            ctx.AppCommands.SetFrameTextureName(frame, "renamed.png");
            Dispatcher.UIThread.RunJobs();

            Assert.True(chainNode.IsExpanded,
                "Parent chain node must stay expanded after a frame node refresh.");
            Assert.Equal("Frame 1", chainNode.Children[0].Header);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void DoubleTapOnPlusButtonDescendant_DoesNotTriggerChainRename()
    {
        // Regression (#218): rapid double-clicks on the Add Frame (+) button were misinterpreted
        // as a double-click on the chain row. The button uses a ContentTemplate (SVG icon), so
        // the DoubleTapped event source is the SVG — a visual child of the Button, not the
        // Button itself — meaning `if (e.Source is Button) return;` did not filter it out.
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            TriggerRefreshTreeView(window);
            Dispatcher.UIThread.RunJobs();

            var tree = GetTree(window);
            var chainNode = GetRoots(tree)[0];
            chainNode.IsExpanded = true;
            Dispatcher.UIThread.RunJobs();

            // Find the Add Frame (+) button inside the chain row.
            var addBtn = tree.GetVisualDescendants()
                .OfType<Button>()
                .FirstOrDefault(b => b.Classes.Contains("add-frame-btn"));
            Assert.NotNull(addBtn);

            // Use the button's first visual descendant (ContentPresenter / SVG) as the
            // event source. If the template hasn't been applied, fall back to the button
            // itself — FindAncestorOfType with includeSelf: true still catches it.
            Control innerControl = addBtn.GetVisualDescendants().OfType<Control>().FirstOrDefault()
                ?? addBtn;

            // Preconditions: innerControl really is under the Button and the chain's TreeViewItem.
            Assert.Same(addBtn, innerControl.FindAncestorOfType<Button>(includeSelf: true));
            var tvi = innerControl.FindAncestorOfType<TreeViewItem>(includeSelf: true);
            Assert.NotNull(tvi);
            Assert.Same(chainNode, tvi.DataContext);

            // Construct TappedEventArgs without calling the constructor (which requires a live
            // PointerEventArgs). The handler only reads e.Handled and e.Source.
            var fakeArgs = (TappedEventArgs)System.Runtime.CompilerServices.RuntimeHelpers
                .GetUninitializedObject(typeof(TappedEventArgs));
            fakeArgs.Source = innerControl;

            var handler = typeof(MainWindow).GetMethod(
                "OnAnimTreeDoubleTapped",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("OnAnimTreeDoubleTapped not found");
            handler.Invoke(window, [null, fakeArgs]);
            Dispatcher.UIThread.RunJobs();

            // The chain must NOT have been put into inline-rename mode.
            Assert.False(chainNode.IsEditing,
                "Double-tap from inside the Add Frame + button must not start an inline rename.");
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void AddFrameBtnDoubleTapped_MarksEventHandled()
    {
            // Regression (#218): the + button has DoubleTapped="OnAddFrameBtnDoubleTapped" which
            // marks e.Handled=true so that OnAnimTreeDoubleTapped (registered with handledEventsToo=false)
            // never fires when the double-tap originates from the + button area.
            var (window, ctx) = CreateWindow();
            try
            {
                var chain = new AnimationChainSave { Name = "Walk" };
                ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
                TriggerRefreshTreeView(window);
                Dispatcher.UIThread.RunJobs();

                var tree = GetTree(window);
                var chainNode = GetRoots(tree)[0];
                chainNode.IsExpanded = true;
                Dispatcher.UIThread.RunJobs();

                var fakeArgs = (TappedEventArgs)System.Runtime.CompilerServices.RuntimeHelpers
                    .GetUninitializedObject(typeof(TappedEventArgs));

                var handler = typeof(MainWindow).GetMethod(
                    "OnAddFrameBtnDoubleTapped",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                    ?? throw new InvalidOperationException("OnAddFrameBtnDoubleTapped not found");
                handler.Invoke(window, [null, fakeArgs]);

                Assert.True(fakeArgs.Handled,
                    "OnAddFrameBtnDoubleTapped must set e.Handled=true to block the chain rename.");
                Assert.False(chainNode.IsEditing,
                    "The chain node must not enter rename mode after + button double-tap.");
            }
            finally { window.Close(); }
    }

    [AvaloniaFact]
    public void PressEnterToConfirmRename_DoesNotCollapseChainNode()
    {
            // Regression: Avalonia 12.x TreeViewItem.OnKeyDown handles Key.Enter by toggling
            // IsExpanded. With a Bubble-only subscription on AnimTree, the TreeViewItem processes
            // Enter before our handler runs, collapsing the chain mid-rename.
            // Fix: use RoutingStrategies.Tunnel so our handler intercepts Enter at the TreeView
            // level (going DOWN) before the event ever reaches TreeViewItem.OnKeyDown.
            var (window, ctx) = CreateWindow();
            try
            {
                var chain = new AnimationChainSave { Name = "Walk" };
                ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

                TriggerRefreshTreeView(window);
                Dispatcher.UIThread.RunJobs();

                var tree = GetTree(window);
                var chainNode = GetRoots(tree)[0];
                chainNode.IsExpanded = true;
                Dispatcher.UIThread.RunJobs();

                // Start inline rename
                var beginMethod = typeof(MainWindow).GetMethod(
                    "BeginInlineRenameSelected",
                    BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    [typeof(AnimationChainSave)],
                    null)
                    ?? throw new InvalidOperationException("BeginInlineRenameSelected not found");
                beginMethod.Invoke(window, [chain]);
                Dispatcher.UIThread.RunJobs(); // flushes the Post(DispatcherPriority.Render) callback

                // Locate the TextBox that BeginInlineRename activated
                var tb = tree.GetVisualDescendants()
                    .OfType<TextBox>()
                    .FirstOrDefault(t => t.DataContext == chainNode);
                Assert.NotNull(tb);

                // Raise Key.Return — the Tunnel handler on AnimTree must intercept it
                // before TreeViewItem.OnKeyDown toggles IsExpanded.
                tb.RaiseEvent(new KeyEventArgs
                {
                    RoutedEvent = InputElement.KeyDownEvent,
                    Source = tb,
                    Key = Key.Return,
                });
                Dispatcher.UIThread.RunJobs();

                Assert.True(chainNode.IsExpanded,
                    "Pressing Enter to confirm rename must not collapse the chain node.");
            }
            finally { window.Close(); }
    }
}
