using System.Collections.ObjectModel;
using System.Reflection;
using AnimationEditor.Core;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.ViewModels;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// End-to-end check that a drag-and-drop frame move (issue #500) flows through the real
/// window tree: the chain node's frame children reorder and relabel, and one undo reverts.
/// The pointer-driven drag itself (DoDragDropAsync) is exercised manually; here we drive the
/// resolved move via AppCommands.MoveFrames, which is what the drop handler calls.
/// </summary>
public class FrameDragReorderHeadlessTests
{
    private static (MainWindow Window, TestServices Ctx) CreateWindow()
    {
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

    private static void RebuildTree(MainWindow window)
    {
        typeof(MainWindow)
            .GetMethod("RebuildTreeView", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(window, null);
    }

    private static ObservableCollection<TreeNodeVm> Roots(MainWindow window)
    {
        var tree = window.FindControl<TreeView>("AnimTree")!;
        return (ObservableCollection<TreeNodeVm>)tree.ItemsSource!;
    }

    private static TreeNodeVm ChainNode(MainWindow window, AnimationChainSave chain)
        => Roots(window).First(n => ReferenceEquals(n.Data, chain));

    private static AnimationChainSave MakeChain(AnimationChainListSave acls, string name, int frameCount)
    {
        var chain = new AnimationChainSave { Name = name };
        for (int i = 0; i < frameCount; i++)
            chain.Frames.Add(new AnimationFrameSave
            {
                TextureName = $"frame{i}.png",
                LeftCoordinate = 0f, RightCoordinate = 1f,
                TopCoordinate = 0f, BottomCoordinate = 1f,
                FrameLength = 0.1f, ShapesSave = new ShapesSave(),
            });
        acls.AnimationChains.Add(chain);
        return chain;
    }

    [AvaloniaFact]
    public void MoveFrames_WithinChain_TreeChildrenReorderAndRelabel()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = MakeChain(ctx.ProjectManager.AnimationChainListSave!, "Walk", 4);
            var original = chain.Frames.ToArray();
            RebuildTree(window);

            // Move the first frame to the end → expected order 1,2,3,0.
            ctx.AppCommands.MoveFrames(new[] { original[0] }, chain, chain, insertIndex: 4);
            Dispatcher.UIThread.RunJobs();

            var children = ChainNode(window, chain).Children;
            Assert.Equal(
                new[] { original[1], original[2], original[3], original[0] },
                children.Select(c => c.Data).ToArray());
            // Positional "Frame N" labels follow the new order.
            Assert.Equal(new[] { "Frame 1", "Frame 2", "Frame 3", "Frame 4" },
                children.Select(c => c.Header).ToArray());
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void SelectSingleFrame_FromMultiSelection_CollapsesTreeToThatFrame()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = MakeChain(ctx.ProjectManager.AnimationChainListSave!, "Walk", 4);
            RebuildTree(window);

            var tree = window.FindControl<TreeView>("AnimTree")!;
            var frameNodes = ChainNode(window, chain).Children.ToList();
            foreach (var node in frameNodes)
                tree.SelectedItems!.Add(node);
            Assert.Equal(4, tree.SelectedItems!.Count);

            // A plain click on one already-selected frame collapses to just that frame —
            // the bug was that the first click left the tree multi-selected.
            var target = chain.Frames[1];
            typeof(MainWindow)
                .GetMethod("SelectSingleFrame", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(window, new object[] { target });
            Dispatcher.UIThread.RunJobs();

            Assert.Single(tree.SelectedItems!);
            Assert.Same(target, ((TreeNodeVm)tree.SelectedItems![0]!).Data);
            Assert.Same(target, ctx.SelectedState.SelectedFrame);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void MoveFrames_MultiSelect_OneUndoRevertsEntireMove()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = MakeChain(ctx.ProjectManager.AnimationChainListSave!, "Walk", 5);
            var original = chain.Frames.ToArray();
            RebuildTree(window);

            // Move frames 0 and 1 to the end as one block.
            ctx.AppCommands.MoveFrames(new[] { original[0], original[1] }, chain, chain, insertIndex: 5);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(
                new[] { original[2], original[3], original[4], original[0], original[1] },
                chain.Frames.ToArray());

            ctx.UndoManager.Undo();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(original, chain.Frames.ToArray());
            Assert.Equal(original, ChainNode(window, chain).Children.Select(c => c.Data).ToArray());
        }
        finally { window.Close(); }
    }
}
