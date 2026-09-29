using AnimationEditor.Core;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.ViewModels;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Structure tests for the tree right-click menu (issue #454 follow-up):
/// Duplicate is a single item grouped with Copy/Paste, and for chains it is a
/// submenu (Original / Flip Horizontal / Flip Vertical) rather than three
/// top-level items.
/// </summary>
public class TreeContextMenuTests
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
        Dispatcher.UIThread.RunJobs();
        return (window, ctx);
    }

    // Selects a tree node and rebuilds the context menu by invoking the real
    // Opening handler (the popup itself doesn't open under the headless backend,
    // so we drive the builder directly). Returns its top-level items.
    private static List<object> OpenMenuFor(MainWindow window, object data, string header)
    {
        var tree = window.FindControl<TreeView>("AnimTree")!;
        var roots = (ObservableCollection<TreeNodeVm>)tree.ItemsSource!;
        var vm = new TreeNodeVm { Header = header, Data = data };
        roots.Add(vm);
        tree.SelectedItems!.Add(vm);
        Dispatcher.UIThread.RunJobs();

        typeof(MainWindow)
            .GetMethod("OnTreeContextMenuOpening", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(window, new object?[] { null, new CancelEventArgs() });
        return tree.ContextMenu!.Items.Cast<object>().ToList();
    }

    private static int IndexOfItem(List<object> items, string header) =>
        items.FindIndex(o => o is MenuItem m && (string?)m.Header == header);

    [AvaloniaFact]
    public void ChainMenu_DuplicateIsSubmenuWithThreeVariants()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Run" };
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            var items = OpenMenuFor(window, chain, "Run");

            // Exactly one "Duplicate" entry (no "Duplicate (original)" etc. at top level).
            var duplicate = items.OfType<MenuItem>().Single(m => (string?)m.Header == "Duplicate");
            var children = duplicate.Items.OfType<MenuItem>().Select(m => (string?)m.Header).ToList();
            Assert.Equal(new[] { "Original", "Flip Horizontal", "Flip Vertical" }, children);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ChainMenu_CopyPasteDuplicateAreConsecutive()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Run" };
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            var items = OpenMenuFor(window, chain, "Run");

            int copy = IndexOfItem(items, "Copy");
            Assert.True(copy >= 0);
            Assert.Equal(copy + 1, IndexOfItem(items, "Cut"));
            Assert.Equal(copy + 2, IndexOfItem(items, "Paste"));
            Assert.Equal(copy + 3, IndexOfItem(items, "Duplicate"));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void FrameMenu_CopyPasteDuplicateAreConsecutive()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            var frame = new AnimationFrameSave { TextureName = "run.png" };
            chain.Frames.Add(frame);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            var items = OpenMenuFor(window, frame, "run.png");

            int copy = IndexOfItem(items, "Copy");
            Assert.True(copy >= 0);
            Assert.Equal(copy + 1, IndexOfItem(items, "Cut"));
            Assert.Equal(copy + 2, IndexOfItem(items, "Paste"));
            Assert.Equal(copy + 3, IndexOfItem(items, "Duplicate"));
        }
        finally { window.Close(); }
    }

    // ── Shape menus (issue #458): parity with the frame menu ──────────────────

    // Builds a chain → frame holding two shapes so reorder items (guarded by
    // position) are present, and returns the frame + both shapes.
    private static (AnimationFrameSave Frame, AARectSave Rect, CircleSave Circle)
        SetupFrameWithTwoShapes(TestServices ctx)
    {
        var chain = new AnimationChainSave { Name = "Walk" };
        var frame = new AnimationFrameSave { TextureName = "run.png", ShapesSave = new ShapesSave() };
        var rect   = new AARectSave { Name = "Rect" };
        var circle = new CircleSave { Name = "Circle" };
        frame.ShapesSave.Shapes.Add(rect);    // index 0 → "first"
        frame.ShapesSave.Shapes.Add(circle);  // index 1 → "last"
        chain.Frames.Add(frame);
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
        return (frame, rect, circle);
    }

    [AvaloniaFact]
    public void RectMenu_HasCopyPasteDuplicateRenameAndMatchFrameSize()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var (_, rect, _) = SetupFrameWithTwoShapes(ctx);
            var items = OpenMenuFor(window, rect, "Rect");

            int copy = IndexOfItem(items, "Copy");
            Assert.True(copy >= 0);
            Assert.Equal(copy + 1, IndexOfItem(items, "Cut"));
            Assert.Equal(copy + 2, IndexOfItem(items, "Paste"));
            Assert.Equal(copy + 3, IndexOfItem(items, "Duplicate"));
            Assert.True(IndexOfItem(items, "Rename…")        >= 0);
            Assert.True(IndexOfItem(items, "Match Frame Size") >= 0);
            Assert.True(IndexOfItem(items, "Delete Rectangle") >= 0);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RectMenu_FirstOfTwoShapes_ShowsMoveDownButNotMoveUp()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            // Rect is at index 0 → only the "move toward the end" items make sense.
            var (_, rect, _) = SetupFrameWithTwoShapes(ctx);
            var items = OpenMenuFor(window, rect, "Rect");

            Assert.True(IndexOfItem(items, "Move Down")      >= 0);
            Assert.True(IndexOfItem(items, "Move To Bottom") >= 0);
            Assert.Equal(-1, IndexOfItem(items, "Move Up"));
            Assert.Equal(-1, IndexOfItem(items, "Move To Top"));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void CircleMenu_HasCopyPasteDuplicateRename_AndNoMatchFrameSize()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var (_, _, circle) = SetupFrameWithTwoShapes(ctx);
            var items = OpenMenuFor(window, circle, "Circle");

            int copy = IndexOfItem(items, "Copy");
            Assert.True(copy >= 0);
            Assert.Equal(copy + 1, IndexOfItem(items, "Cut"));
            Assert.Equal(copy + 2, IndexOfItem(items, "Paste"));
            Assert.Equal(copy + 3, IndexOfItem(items, "Duplicate"));
            Assert.True(IndexOfItem(items, "Rename…")      >= 0);
            Assert.True(IndexOfItem(items, "Delete Circle") >= 0);
            // Match Frame Size is rect-only.
            Assert.Equal(-1, IndexOfItem(items, "Match Frame Size"));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void CommitInlineRename_Rectangle_RenamesShape()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var (_, rect, _) = SetupFrameWithTwoShapes(ctx);
            var vm = new TreeNodeVm { Header = "Rect", Data = rect };

            window.CommitInlineRenamePublic(vm, "Renamed");
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("Renamed", rect.Name);
        }
        finally { window.Close(); }
    }
}
