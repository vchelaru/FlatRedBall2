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
/// Regression tests for issue #567: the tree context menu's "Match Frame Size" item
/// only resized the single right-clicked rectangle, silently leaving the rest of a
/// multi-selection unchanged — the same hardcoded-single-item bug already fixed for
/// Delete in <see cref="RightClickContextMenuDeleteAppTests"/>.
/// </summary>
public class RightClickContextMenuMatchFrameSizeAppTests
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

    [AvaloniaFact]
    public void MatchFrameSize_ContextMenu_OnMultiSelection_MatchesAllSelectedRectangles()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            var frame = new AnimationFrameSave
            {
                TextureName = "a.png", ShapesSave = new ShapesSave(),
                RelativeX = 40f, RelativeY = -20f
            };
            var r0 = new AARectSave { Name = "R0", X = 1f, Y = 1f };
            var r1 = new AARectSave { Name = "R1", X = 2f, Y = 2f };
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

            // Right-click the first rect (not the whole selection) — selection must survive,
            // and Match Frame Size must then act on both rectangles, not just this one.
            RightClick(window, tree, rectNodes[0]);
            Assert.Equal(2, tree.SelectedItems!.Count);

            OpenContextMenu(window);
            ClickContextMenuItem(tree, "Match Frame Size");

            Assert.Equal(40f, r0.X);
            Assert.Equal(-20f, r0.Y);
            Assert.Equal(40f, r1.X);
            Assert.Equal(-20f, r1.Y);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void MatchFrameSize_ContextMenu_OnMultiSelectionSpanningFrames_MatchesEachRectangleToItsOwnFrame()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            var frameA = new AnimationFrameSave
            {
                TextureName = "a.png", ShapesSave = new ShapesSave(),
                RelativeX = 5f, RelativeY = 6f
            };
            var frameB = new AnimationFrameSave
            {
                TextureName = "b.png", ShapesSave = new ShapesSave(),
                RelativeX = 50f, RelativeY = 60f
            };
            var rectInA = new AARectSave { Name = "InA", X = 1f, Y = 1f };
            var rectInB = new AARectSave { Name = "InB", X = 2f, Y = 2f };
            frameA.ShapesSave.Shapes.Add(rectInA);
            frameB.ShapesSave.Shapes.Add(rectInB);
            chain.Frames.Add(frameA);
            chain.Frames.Add(frameB);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            RebuildTree(window);

            var tree = window.FindControl<TreeView>("AnimTree")!;
            var chainNode = FirstChainNode(tree);
            chainNode.IsExpanded = true;
            FlushUi();

            var frameNodeA = chainNode.Children[0];
            var frameNodeB = chainNode.Children[1];
            frameNodeA.IsExpanded = true;
            frameNodeB.IsExpanded = true;
            FlushUi();

            var rectNodeA = frameNodeA.Children[0];
            var rectNodeB = frameNodeB.Children[0];
            tree.SelectedItems!.Clear();
            tree.SelectedItems.Add(rectNodeA);
            tree.SelectedItems.Add(rectNodeB);
            FlushUi();

            RightClick(window, tree, rectNodeA);
            Assert.Equal(2, tree.SelectedItems!.Count);

            OpenContextMenu(window);
            ClickContextMenuItem(tree, "Match Frame Size");

            // Each rectangle matches its OWN frame, not frameA (the "current"/primary frame).
            Assert.Equal(5f, rectInA.X);
            Assert.Equal(6f, rectInA.Y);
            Assert.Equal(50f, rectInB.X);
            Assert.Equal(60f, rectInB.Y);
        }
        finally { window.Close(); }
    }
}
