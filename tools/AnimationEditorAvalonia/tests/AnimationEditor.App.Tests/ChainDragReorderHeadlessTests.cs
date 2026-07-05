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
/// End-to-end check that a drag-and-drop chain reorder flows through the real window tree:
/// the top-level chain nodes reorder and one undo reverts. The pointer-driven drag itself
/// (DoDragDropAsync) is exercised manually; here we drive the resolved move via
/// AppCommands.MoveChainToIndex, which is what the drop handler calls.
/// </summary>
public class ChainDragReorderHeadlessTests
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
            .Invoke(window, new object[] { Array.Empty<string>() });
    }

    private static ObservableCollection<TreeNodeVm> Roots(MainWindow window)
    {
        var tree = window.FindControl<TreeView>("AnimTree")!;
        return (ObservableCollection<TreeNodeVm>)tree.ItemsSource!;
    }

    private static AnimationChainSave MakeChain(AnimationChainListSave acls, string name)
    {
        var chain = new AnimationChainSave { Name = name };
        acls.AnimationChains.Add(chain);
        return chain;
    }

    [AvaloniaFact]
    public void MoveChainToIndex_ReordersRootTreeNodes_OneUndoReverts()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var acls = ctx.ProjectManager.AnimationChainListSave!;
            var a = MakeChain(acls, "A");
            var b = MakeChain(acls, "B");
            var c = MakeChain(acls, "C");
            RebuildTree(window);

            // Drag C to the front (insert index 0).
            ctx.AppCommands.MoveChainToIndex(c, 0);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(new[] { c, a, b },
                Roots(window).Select(n => n.Data).ToArray());

            ctx.UndoManager.Undo();
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(new[] { a, b, c },
                Roots(window).Select(n => n.Data).ToArray());
        }
        finally { window.Close(); }
    }

    /// <summary>
    /// Multi-chain drag: dragging a selection of several chains moves the whole set together
    /// as a contiguous block, preserving relative order (issue #566). Exercises the resolved
    /// move via AppCommands.MoveChainsToIndex, which is what the drop handler calls for a
    /// multi-chain drag source.
    /// </summary>
    [AvaloniaFact]
    public void MoveChainsToIndex_MultiSelection_ReordersRootTreeNodesAsBlock_OneUndoReverts()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var acls = ctx.ProjectManager.AnimationChainListSave!;
            var a = MakeChain(acls, "A");
            var b = MakeChain(acls, "B");
            var c = MakeChain(acls, "C");
            var d = MakeChain(acls, "D");
            RebuildTree(window);

            // Drag {B, D} to the front — non-contiguous selection squashes to a block.
            ctx.AppCommands.MoveChainsToIndex(new[] { b, d }, 0);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(new[] { b, d, a, c },
                Roots(window).Select(n => n.Data).ToArray());

            ctx.UndoManager.Undo();
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(new[] { a, b, c, d },
                Roots(window).Select(n => n.Data).ToArray());
        }
        finally { window.Close(); }
    }
}
