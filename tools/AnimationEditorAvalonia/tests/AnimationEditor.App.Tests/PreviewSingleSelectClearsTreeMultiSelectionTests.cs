using AnimationEditor.Core.ViewModels;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using System.Linq;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Regression test for issue #727: after a ctrl+click multi-select of shapes in the tree,
/// selecting a single shape (e.g. by clicking it in the preview panel) left the other
/// multi-selected shapes highlighted in the tree. Root cause: MainWindow.SyncTreeSelection
/// only re-pushed the tree selection when the target node was NOT already present in
/// AnimTree.SelectedItems — so when the target was already part of a stale multi-selection,
/// the sync was skipped entirely and the other stale nodes stayed selected.
/// </summary>
public class PreviewSingleSelectClearsTreeMultiSelectionTests
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

    [AvaloniaFact]
    public void SelectingSingleShape_ClearsStaleTreeMultiSelection()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            var frame = new AnimationFrameSave { TextureName = "a.png", FrameLength = 0.1f, ShapesSave = new ShapesSave() };
            var rect0 = new AARectSave { Name = "Box0", ScaleX = 8f, ScaleY = 8f };
            var rect1 = new AARectSave { Name = "Box1", ScaleX = 8f, ScaleY = 8f };
            frame.ShapesSave!.Shapes.Add(rect0);
            frame.ShapesSave!.Shapes.Add(rect1);
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
            var rect0Node = frameNode.Children.Single(n => ReferenceEquals(n.Data, rect0));
            var rect1Node = frameNode.Children.Single(n => ReferenceEquals(n.Data, rect1));

            // Ctrl+click multi-select of both rects in the tree.
            tree.SelectedItems!.Clear();
            tree.SelectedItems.Add(rect0Node);
            tree.SelectedItems.Add(rect1Node);
            FlushUi();
            Assert.Equal(2, tree.SelectedItems.Count);

            // Select just rect0 — mirrors PreviewControl.OnPointerPressed's shape-hit branch,
            // which clears SelectedNodes before assigning the singular selection.
            ctx.SelectedState.SelectedNodes = new System.Collections.Generic.List<object>();
            ctx.SelectedState.SelectedRectangle = rect0;
            FlushUi();

            Assert.Single(tree.SelectedItems.Cast<object>());
            Assert.Contains(rect0Node, tree.SelectedItems.Cast<object>());
        }
        finally { window.Close(); }
    }
}
