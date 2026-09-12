using AnimationEditor.Core.ViewModels;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using System.Linq;
using System.Reflection;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Issue #1108: <see cref="AnimationEditor.Core.CommandsAndState.AppCommands.DeleteShapes"/> itself
/// was fixed for cross-frame selections in #1102, but nothing drove that fix through the real
/// UI-selection + Delete-keypress path (<c>MainWindow.HandleDelete</c>) with shapes selected across
/// two different frames. This proves the tree-selection-to-AppCommands routing, not #1102's own fix.
/// </summary>
public class CrossFrameShapeDeleteAppTests
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

    [AvaloniaFact]
    public void Delete_ShapesSelectedAcrossTwoFrames_DeletesBoth_AndOneUndoRestoresBoth()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            var frameA = new AnimationFrameSave { TextureName = "a.png", FrameLength = 0.1f, ShapesSave = new ShapesSave() };
            var frameB = new AnimationFrameSave { TextureName = "b.png", FrameLength = 0.1f, ShapesSave = new ShapesSave() };
            var rect = new AARectSave { Name = "RectInA" };
            var circle = new CircleSave { Name = "CircleInB", Radius = 10 };
            frameA.ShapesSave.Shapes.Add(rect);
            frameB.ShapesSave.Shapes.Add(circle);
            chain.Frames.AddRange(new[] { frameA, frameB });
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            RebuildTree(window);

            var tree = window.FindControl<TreeView>("AnimTree")!;
            var frameNodes = FirstChainNode(tree).Children;
            var rectNode = frameNodes[0].Children[0];
            var circleNode = frameNodes[1].Children[0];

            tree.SelectedItems!.Clear();
            tree.SelectedItems.Add(rectNode);
            tree.SelectedItems.Add(circleNode);
            FlushUi();

            tree.Focus();
            window.KeyPress(Key.Delete, RawInputModifiers.None, PhysicalKey.None, null);
            FlushUi();

            Assert.Empty(frameA.ShapesSave!.Shapes);
            Assert.Empty(frameB.ShapesSave!.Shapes);

            window.KeyPress(Key.Z, RawInputModifiers.Control, PhysicalKey.None, null);
            FlushUi();

            Assert.Equal(new[] { rect }, frameA.ShapesSave!.AARectSaves);
            Assert.Equal(new[] { circle }, frameB.ShapesSave!.CircleSaves);
        }
        finally { window.Close(); }
    }
}
