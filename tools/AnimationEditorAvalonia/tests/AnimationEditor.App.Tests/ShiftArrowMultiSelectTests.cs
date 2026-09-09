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
/// Issue #1023: Shift+Down/Up in the ANIMATIONS tree must extend a contiguous range
/// selection from an anchor row (file-explorer semantics), not just move the single
/// selection like Avalonia's native TreeViewItem arrow-key navigation does.
/// </summary>
public class ShiftArrowMultiSelectTests
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

    // Real MouseDown/MouseUp (not a direct model assignment) so the click's focus and
    // selection side effects — the ones our Shift+Arrow anchor tracking depends on — actually run.
    private static void RealSingleClick(MainWindow window, Control target)
    {
        var local = new Point(target.Bounds.Width / 2, target.Bounds.Height / 2);
        var p = target.TranslatePoint(local, window)!.Value;
        window.MouseDown(p, MouseButton.Left);
        window.MouseUp(p, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Builds two chains, each with two frames, all expanded, and returns the flattened
    /// visible-row order alongside the realized TreeViewItem for each row.
    /// </summary>
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
        foreach (var root in roots) root.IsExpanded = true; // default, but explicit for clarity
        Dispatcher.UIThread.RunJobs();

        var flatOrder = TreeBuilder.FlattenVisible(roots);
        var rows = flatOrder
            .Select(vm => tree.GetVisualDescendants().OfType<TreeViewItem>()
                .First(t => ReferenceEquals(t.DataContext, vm)))
            .ToList();

        return (window, ctx, flatOrder, rows);
    }

    private static void ShiftKeyPress(MainWindow window, Key key) =>
        window.KeyPress(key, RawInputModifiers.Shift, PhysicalKey.None, null);

    [AvaloniaFact]
    public void ShiftDown_FromSelectedFrame_ExtendsSelectionToNextRow()
    {
        var (window, _, flatOrder, rows) = BuildTwoChainsTwoFramesEach();
        try
        {
            var tree = window.FindControl<TreeView>("AnimTree")!;
            RealSingleClick(window, rows[1]); // chain0's frame0 (index 1: chain0=0, frame0=1)

            ShiftKeyPress(window, Key.Down);

            var selected = tree.SelectedItems!.Cast<TreeNodeVm>().ToList();
            Assert.Equal(2, selected.Count);
            Assert.Contains(flatOrder[1], selected); // original row stays selected
            Assert.Contains(flatOrder[2], selected); // extended to the next row (chain0's frame1)
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ShiftDown_TwiceAcrossChainBoundary_GrowsContiguousRangeFromAnchor()
    {
        var (window, _, flatOrder, rows) = BuildTwoChainsTwoFramesEach();
        try
        {
            var tree = window.FindControl<TreeView>("AnimTree")!;
            // flatOrder: [0]=chain0 [1]=frame0-0 [2]=frame0-1 [3]=chain1 [4]=frame1-0 [5]=frame1-1
            RealSingleClick(window, rows[2]); // anchor = chain0's frame1 (last row of chain0)

            ShiftKeyPress(window, Key.Down); // crosses into chain1
            ShiftKeyPress(window, Key.Down); // grows into chain1's frame0

            var selected = tree.SelectedItems!.Cast<TreeNodeVm>().ToList();
            Assert.Equal(3, selected.Count);
            Assert.Contains(flatOrder[2], selected); // anchor
            Assert.Contains(flatOrder[3], selected); // chain1 (crossed boundary)
            Assert.Contains(flatOrder[4], selected); // chain1's frame0
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ShiftUp_AfterShiftDownTwice_ShrinksRangeBackTowardAnchor()
    {
        var (window, _, flatOrder, rows) = BuildTwoChainsTwoFramesEach();
        try
        {
            var tree = window.FindControl<TreeView>("AnimTree")!;
            RealSingleClick(window, rows[2]); // anchor = chain0's frame1

            ShiftKeyPress(window, Key.Down);
            ShiftKeyPress(window, Key.Down); // range now [2..4]
            ShiftKeyPress(window, Key.Up);   // shrink back to [2..3]

            var selected = tree.SelectedItems!.Cast<TreeNodeVm>().ToList();
            Assert.Equal(2, selected.Count);
            Assert.Contains(flatOrder[2], selected); // anchor stays
            Assert.Contains(flatOrder[3], selected); // far end shrank back to here
            Assert.DoesNotContain(flatOrder[4], selected);
        }
        finally { window.Close(); }
    }

    /// <summary>Regression: plain (no-Shift) Down must keep switching the single selection.</summary>
    [AvaloniaFact]
    public void Down_NoModifier_StillReplacesSelectionWithSingleNextRow()
    {
        var (window, _, flatOrder, rows) = BuildTwoChainsTwoFramesEach();
        try
        {
            var tree = window.FindControl<TreeView>("AnimTree")!;
            RealSingleClick(window, rows[1]); // chain0's frame0

            window.KeyPress(Key.Down, RawInputModifiers.None, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();

            var selected = tree.SelectedItems!.Cast<TreeNodeVm>().ToList();
            Assert.Single(selected);
            Assert.Same(flatOrder[2], selected[0]); // moved to chain0's frame1, old row deselected
        }
        finally { window.Close(); }
    }
}
