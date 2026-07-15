using AnimationEditor.Core.ViewModels;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using System.Linq;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Regression tests for issue #724: Ctrl+click multi-select across frames highlighted every
/// selected AARectSave/CircleSave, but editing ScaleX/ScaleY in the property panel only applied
/// to the last-selected shape. Mirrors the existing frame multi-select property-panel behavior.
/// </summary>
public class ShapeMultiSelectPropertyPanelTests
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
    public void ApplyRectProps_TwoRectsMultiSelectedAcrossFrames_AppliesScaleXToBoth()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            var f0 = new AnimationFrameSave { TextureName = "a.png", FrameLength = 0.1f, ShapesSave = new ShapesSave() };
            var f1 = new AnimationFrameSave { TextureName = "b.png", FrameLength = 0.1f, ShapesSave = new ShapesSave() };
            var rect0 = new AARectSave { Name = "Box0", ScaleX = 8f, ScaleY = 8f };
            var rect1 = new AARectSave { Name = "Box1", ScaleX = 8f, ScaleY = 8f };
            f0.ShapesSave!.Shapes.Add(rect0);
            f1.ShapesSave!.Shapes.Add(rect1);
            chain.Frames.AddRange(new[] { f0, f1 });
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            RebuildTree(window);

            var tree = window.FindControl<TreeView>("AnimTree")!;
            var chainNode = FirstChainNode(tree);
            chainNode.IsExpanded = true;
            FlushUi();
            var frame0Node = chainNode.Children[0];
            var frame1Node = chainNode.Children[1];
            frame0Node.IsExpanded = true;
            frame1Node.IsExpanded = true;
            FlushUi();
            var rect0Node = frame0Node.Children.Single(n => ReferenceEquals(n.Data, rect0));
            var rect1Node = frame1Node.Children.Single(n => ReferenceEquals(n.Data, rect1));

            // Ctrl+click multi-select of both rect nodes, spanning two different frames.
            tree.SelectedItems!.Clear();
            tree.SelectedItems.Add(rect0Node);
            tree.SelectedItems.Add(rect1Node);
            FlushUi();

            Assert.Equal(2, ctx.SelectedState.SelectedRectangles.Count);

            var scaleXInput = window.FindControl<NumericUpDown>("PropRectScaleX")!;
            scaleXInput.Value = 20m;
            FlushUi();

            Assert.Equal(20f, rect0.ScaleX);
            Assert.Equal(20f, rect1.ScaleX);
        }
        finally { window.Close(); }
    }
}
