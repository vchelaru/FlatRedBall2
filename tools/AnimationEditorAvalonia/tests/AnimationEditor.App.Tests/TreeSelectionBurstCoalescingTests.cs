using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using AnimationEditor.Core.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Live bug found while investigating a 5-10 second freeze on multi-selecting many animations:
/// a multi-row tree-selection gesture (Shift/Ctrl+Click range-select, our own Shift+Arrow range
/// extension) can fire Avalonia's <c>TreeView.SelectionChanged</c> once PER ROW rather than once
/// for the whole gesture. <c>MainWindow.OnTreeSelectionChanged</c> used to react to every one of
/// those by re-syncing <c>ISelectedState</c> (which cascades into the timeline strip, property
/// panel, preview, and status bar), turning an O(1) selection into O(N) redundant rebuilds --
/// measured: selecting 40 chains fired 80 syncs, ~8 seconds of UI-thread work.
/// </summary>
public class TreeSelectionBurstCoalescingTests
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

    private static void TriggerRefreshTreeView(MainWindow window)
    {
        typeof(MainWindow).GetMethod("RefreshTreeView", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(window, null);
        Dispatcher.UIThread.RunJobs();
    }

    private static void RealSingleClick(MainWindow window, Control target)
    {
        var local = new Point(target.Bounds.Width / 2, target.Bounds.Height / 2);
        var p = target.TranslatePoint(local, window)!.Value;
        window.MouseDown(p, MouseButton.Left);
        window.MouseUp(p, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Builds two chains, each with two frames, all expanded, and returns the flattened
    /// visible-row order alongside the realized TreeViewItem for each row.</summary>
    private static (MainWindow Window, TestServices Ctx, List<TreeNodeVm> FlatOrder, List<TreeViewItem> Rows)
        BuildTwoChainsTwoFramesEach()
    {
        var (window, ctx) = CreateWindow();

        var chain0 = new AnimationChainSave { Name = "Walk" };
        chain0.Frames.Add(new AnimationFrameSave { TextureName = "a.png" });
        chain0.Frames.Add(new AnimationFrameSave { TextureName = "b.png" });
        var chain1 = new AnimationChainSave { Name = "Run" };
        chain1.Frames.Add(new AnimationFrameSave { TextureName = "c.png" });
        chain1.Frames.Add(new AnimationFrameSave { TextureName = "d.png" });
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain0);
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain1);

        TriggerRefreshTreeView(window);

        var tree = window.FindControl<TreeView>("AnimTree")!;
        var roots = (ObservableCollection<TreeNodeVm>)tree.ItemsSource!;
        foreach (var root in roots) root.IsExpanded = true;
        Dispatcher.UIThread.RunJobs();

        var flatOrder = TreeBuilder.FlattenVisible(roots);
        var rows = flatOrder
            .Select(vm => tree.GetVisualDescendants().OfType<TreeViewItem>()
                .First(t => ReferenceEquals(t.DataContext, vm)))
            .ToList();

        return (window, ctx, flatOrder, rows);
    }

    [AvaloniaFact]
    public void RapidTreeSelectionBurst_CoalescesIntoLeadingAndTrailingSyncNotOnePerRow()
    {
        var (window, ctx, flatOrder, rows) = BuildTwoChainsTwoFramesEach();
        try
        {
            var tree = window.FindControl<TreeView>("AnimTree")!;
            RealSingleClick(window, rows[0]); // settle an initial selection + drain any pending sync

            int fireCount = 0;
            ctx.SelectedState.SelectionChanged += () => fireCount++;

            // Simulate a rapid multi-row selection burst the way Avalonia's own native
            // Shift/Ctrl+Click range-select does it internally, and the way our own Shift+Arrow
            // handler does across a wide range: every Add fires the tree's own SelectionChanged
            // synchronously, all within this one call, with no intervening dispatcher pump.
            tree.SelectedItems!.Clear();
            foreach (var row in flatOrder)
                tree.SelectedItems.Add(row);

            // Leading edge: the first row of the burst syncs immediately and synchronously
            // (SelectedNodes assignment + RouteNodeSelection) -- every row after that is
            // coalesced, not resynced individually.
            Assert.Equal(2, fireCount);

            Dispatcher.UIThread.RunJobs();

            // Trailing edge: exactly one catch-up sync once the burst drains -- not one per
            // remaining row (flatOrder.Count - 1 of them).
            Assert.Equal(4, fireCount);
            Assert.Equal(flatOrder.Count, ctx.SelectedState.SelectedNodes.Count);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void SingleTreeSelectionChange_StillSyncsImmediatelyWithNoTrailingReplay()
    {
        // The common case (one selection change, nothing else queued) must behave exactly as
        // before this fix: sync happens synchronously on the leading edge, and the dispatcher
        // pump afterward must not replay/resync anything (regression: a stale trailing sync
        // firing after something else changed ISelectedState in between -- e.g. a command
        // selecting a newly-added shape right after a tree click -- would clobber it, see
        // MainWindowMenuFlowTests.ContextMenu_Add{Rect,Circle}_ExpandsFrameNodeAndSelectsShape).
        var (window, ctx, flatOrder, _) = BuildTwoChainsTwoFramesEach();
        try
        {
            var tree = window.FindControl<TreeView>("AnimTree")!;
            tree.SelectedItem = flatOrder[0]; // chain0 -- drain any pending sync from setup
            Dispatcher.UIThread.RunJobs();

            int fireCount = 0;
            ctx.SelectedState.SelectionChanged += () => fireCount++;

            tree.SelectedItem = flatOrder[1]; // chain0's frame0
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(2, fireCount); // leading-edge sync only, no trailing replay
            Assert.Same(flatOrder[1].Data, ctx.SelectedState.SelectedFrame);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RapidTreeSelectionBurst_SyncsFinalSelectionNotAnIntermediateStep()
    {
        // A burst that's immediately followed by more Adds before the dispatcher ever gets to
        // run the coalesced sync must still land on the LAST state, not whatever the selection
        // looked like when the first row of the burst was added.
        var (window, ctx, flatOrder, rows) = BuildTwoChainsTwoFramesEach();
        try
        {
            var tree = window.FindControl<TreeView>("AnimTree")!;
            RealSingleClick(window, rows[0]);

            tree.SelectedItems!.Clear();
            tree.SelectedItems.Add(flatOrder[0]);
            tree.SelectedItems.Add(flatOrder[1]);
            tree.SelectedItems.Add(flatOrder[2]);

            Dispatcher.UIThread.RunJobs();

            Assert.Equal(3, ctx.SelectedState.SelectedNodes.Count);
            Assert.Contains(flatOrder[2].Data!, ctx.SelectedState.SelectedNodes);
        }
        finally { window.Close(); }
    }
}
