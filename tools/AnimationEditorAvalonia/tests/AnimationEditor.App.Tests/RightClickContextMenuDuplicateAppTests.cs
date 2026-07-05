using AnimationEditor.Core.IO;
using AnimationEditor.Core.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FlatRedBall2.Animation.Content;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Regression tests for issue #565: right-click preserves a multi-selection (see
/// <see cref="RightClickMultiSelectAppTests"/> and <see cref="RightClickContextMenuDeleteAppTests"/>),
/// but the context menu's "Duplicate" items must also act on the whole preserved selection,
/// not just the right-clicked node — matching Ctrl+D (<c>HandleDuplicate</c>).
/// </summary>
public class RightClickContextMenuDuplicateAppTests
{
    private static (MainWindow Window, TestServices Ctx) CreateWindow()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName = null;
        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, ctx);
    }

    private static void RebuildTree(MainWindow window)
    {
        typeof(MainWindow).GetMethod("RebuildTreeView", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(window, new object[] { System.Array.Empty<string>() });
        FlushUi();
    }

    private static void FlushUi()
    {
        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();
    }

    private static TreeNodeVm FirstChainNode(TreeView tree) =>
        tree.ItemsSource is System.Collections.IEnumerable roots
            ? roots.Cast<TreeNodeVm>().First()
            : throw new Xunit.Sdk.XunitException("No tree roots");

    // Right-clicks the realized TreeViewItem for `node` via a real pointer press, so the
    // test exercises OnTreePointerPressed's selection-preserving logic, not just its effect.
    private static void RightClick(MainWindow window, TreeView tree, TreeNodeVm node)
    {
        var tvi = tree.GetVisualDescendants().OfType<TreeViewItem>()
            .First(t => ReferenceEquals(t.DataContext, node));
        var centre = new Point(tvi.Bounds.Width / 2, tvi.Bounds.Height / 2);
        var pointInWindow = tvi.TranslatePoint(centre, window)!.Value;
        window.MouseDown(pointInWindow, MouseButton.Right);
        FlushUi();
    }

    private static void OpenContextMenu(MainWindow window) =>
        typeof(MainWindow)
            .GetMethod("OnTreeContextMenuOpening", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(window, new object?[] { null, new CancelEventArgs() });

    private static void ClickContextMenuItem(TreeView tree, string header)
    {
        var item = tree.ContextMenu!.Items
            .OfType<MenuItem>()
            .First(m => m.Header?.ToString() == header);
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
    }

    private static void ClickContextSubMenuItem(TreeView tree, string parentHeader, string childHeader)
    {
        var parent = tree.ContextMenu!.Items
            .OfType<MenuItem>()
            .First(m => m.Header?.ToString() == parentHeader);
        var child = parent.Items
            .OfType<MenuItem>()
            .First(m => m.Header?.ToString() == childHeader);
        child.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
    }

    [AvaloniaFact]
    public void DuplicateFrame_ContextMenu_OnMultiSelection_DuplicatesAllSelectedFrames()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            var f0 = new AnimationFrameSave { TextureName = "a.png", FrameLength = 0.1f };
            var f1 = new AnimationFrameSave { TextureName = "b.png", FrameLength = 0.1f };
            var f2 = new AnimationFrameSave { TextureName = "c.png", FrameLength = 0.1f };
            chain.Frames.AddRange(new[] { f0, f1, f2 });
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            RebuildTree(window);

            var tree = window.FindControl<TreeView>("AnimTree")!;
            var chainNode = FirstChainNode(tree);
            chainNode.IsExpanded = true;
            FlushUi();

            var frameNodes = chainNode.Children;
            tree.SelectedItems!.Clear();
            tree.SelectedItems.Add(frameNodes[0]);
            tree.SelectedItems.Add(frameNodes[1]);
            tree.SelectedItems.Add(frameNodes[2]);
            FlushUi();

            // Right-click a non-primary member of the selection — selection must survive
            // (already fixed for #561), and Duplicate must then act on all 3.
            RightClick(window, tree, frameNodes[1]);
            Assert.Equal(3, tree.SelectedItems!.Count);

            OpenContextMenu(window);
            ClickContextMenuItem(tree, "Duplicate");

            Assert.Equal(6, chain.Frames.Count);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void DuplicateRectangle_ContextMenu_OnMultiSelection_DuplicatesAllSelectedRectangles()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            var frame = new AnimationFrameSave { TextureName = "a.png", ShapesSave = new ShapesSave() };
            var r0 = new AARectSave { Name = "R0" };
            var r1 = new AARectSave { Name = "R1" };
            frame.ShapesSave.Shapes.Add(r0);
            frame.ShapesSave.Shapes.Add(r1);
            chain.Frames.Add(frame);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            RebuildTree(window);

            var tree = window.FindControl<TreeView>("AnimTree")!;
            var chainNode = FirstChainNode(tree);
            chainNode.IsExpanded = true;
            FlushUi();

            var frameNode = chainNode.Children[0];
            frameNode.IsExpanded = true;
            FlushUi();

            var rectNodes = frameNode.Children;
            tree.SelectedItems!.Clear();
            tree.SelectedItems.Add(rectNodes[0]);
            tree.SelectedItems.Add(rectNodes[1]);
            FlushUi();

            RightClick(window, tree, rectNodes[0]);
            Assert.Equal(2, tree.SelectedItems!.Count);

            OpenContextMenu(window);
            ClickContextMenuItem(tree, "Duplicate");

            Assert.Equal(4, frame.ShapesSave.AARectSaves.Count());
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void DuplicateChain_ContextMenu_OriginalOnMultiSelection_DuplicatesAllSelectedChains()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var walk = new AnimationChainSave { Name = "Walk" };
            var run  = new AnimationChainSave { Name = "Run" };
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(walk);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(run);

            RebuildTree(window);

            var tree = window.FindControl<TreeView>("AnimTree")!;
            var chainNodes = tree.ItemsSource is System.Collections.IEnumerable roots
                ? roots.Cast<TreeNodeVm>().ToList()
                : throw new Xunit.Sdk.XunitException("No tree roots");
            tree.SelectedItems!.Clear();
            tree.SelectedItems.Add(chainNodes[0]);
            tree.SelectedItems.Add(chainNodes[1]);
            FlushUi();

            RightClick(window, tree, chainNodes[0]);
            Assert.Equal(2, tree.SelectedItems!.Count);

            OpenContextMenu(window);
            ClickContextSubMenuItem(tree, "Duplicate", "Original");

            Assert.Equal(4, ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Count);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void DuplicateChain_ContextMenu_FlipHorizontalOnMultiSelection_DuplicatesAllSelectedChainsFlipped()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var walk = new AnimationChainSave { Name = "Walk" };
            walk.Frames.Add(new AnimationFrameSave { TextureName = "a.png", FlipHorizontal = false });
            var run = new AnimationChainSave { Name = "Run" };
            run.Frames.Add(new AnimationFrameSave { TextureName = "b.png", FlipHorizontal = false });
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(walk);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(run);

            RebuildTree(window);

            var tree = window.FindControl<TreeView>("AnimTree")!;
            var chainNodes = tree.ItemsSource is System.Collections.IEnumerable roots
                ? roots.Cast<TreeNodeVm>().ToList()
                : throw new Xunit.Sdk.XunitException("No tree roots");
            tree.SelectedItems!.Clear();
            tree.SelectedItems.Add(chainNodes[0]);
            tree.SelectedItems.Add(chainNodes[1]);
            FlushUi();

            RightClick(window, tree, chainNodes[0]);
            Assert.Equal(2, tree.SelectedItems!.Count);

            OpenContextMenu(window);
            ClickContextSubMenuItem(tree, "Duplicate", "Flip Horizontal");

            var acls = ctx.ProjectManager.AnimationChainListSave!;
            Assert.Equal(4, acls.AnimationChains.Count);
            var walkCopy = acls.AnimationChains.Single(c => c.Name.StartsWith("Walk") && c != walk);
            var runCopy  = acls.AnimationChains.Single(c => c.Name.StartsWith("Run") && c != run);
            Assert.True(walkCopy.Frames[0].FlipHorizontal);
            Assert.True(runCopy.Frames[0].FlipHorizontal);
        }
        finally { window.Close(); }
    }
}
