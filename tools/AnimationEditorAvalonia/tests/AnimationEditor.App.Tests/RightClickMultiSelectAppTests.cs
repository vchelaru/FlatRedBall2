using AnimationEditor.Core.IO;
using AnimationEditor.Core.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FlatRedBall2.Animation.Content;
using System.Linq;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Regression tests for issue #561: right-clicking a node that is already part of the current
/// multi-selection must not collapse the selection down to just that node, since the context
/// menu (e.g. Delete) should act on the whole group — mirroring Explorer-style behavior.
/// Right-clicking a node NOT currently selected should still collapse to just that node.
/// </summary>
public class RightClickMultiSelectAppTests
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
        typeof(MainWindow).GetMethod("RebuildTreeView", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
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

    /// <summary>
    /// Right-clicks the realized <see cref="TreeViewItem"/> for <paramref name="node"/> by
    /// simulating a real pointer press at that item's on-screen centre, so the test exercises
    /// the actual tunnel-phase routing in <c>OnTreePointerPressed</c>, not just its effect.
    /// </summary>
    private static void RightClick(MainWindow window, TreeView tree, TreeNodeVm node)
    {
        var tvi = tree.GetVisualDescendants().OfType<TreeViewItem>()
            .First(t => ReferenceEquals(t.DataContext, node));
        var centre = new Point(tvi.Bounds.Width / 2, tvi.Bounds.Height / 2);
        var pointInWindow = tvi.TranslatePoint(centre, window)!.Value;
        window.MouseDown(pointInWindow, MouseButton.Right);
        FlushUi();
    }

    [AvaloniaFact]
    public void RightClick_OnNodeAlreadyInMultiSelection_KeepsWholeSelection()
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

            // Right-click a non-primary member of the selection (frameNodes[1], not the
            // primary SelectedItem which is frameNodes[2] since it was added last).
            RightClick(window, tree, frameNodes[1]);

            Assert.Equal(3, tree.SelectedItems!.Count);
            Assert.Equal(3, ctx.SelectedState.SelectedFrames.Count);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RightClick_OnUnselectedNode_CollapsesSelectionToThatNode()
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
            FlushUi();

            // Right-click frameNodes[2], which is NOT part of the current selection.
            RightClick(window, tree, frameNodes[2]);

            Assert.Same(frameNodes[2], Assert.Single(tree.SelectedItems!.Cast<TreeNodeVm>()));
        }
        finally { window.Close(); }
    }
}
