using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
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
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Headless coverage for the chain-lock feature's UI surfaces (issue #1032): the tree row's
/// lock icon button and the Inspector's "Locked" checkbox both reflect and toggle
/// <see cref="AnimationChainSave.IsLocked"/> through <c>AppCommands.SetChainLocked</c>.
/// </summary>
public class ChainLockTests
{
    private static (MainWindow Window, TestServices Ctx, AnimationChainSave Chain) CreateWindowWithChain()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.FileName = null;
        ctx.AppCommands.DoOnUiThread = a => a();
        ctx.AppCommands.FileDialogService = NullFileDialogService.Instance;

        var chain = new AnimationChainSave { Name = "Walk" };

        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        // MainWindow.OnOpened resets AnimationChainListSave to a fresh empty one when there's no
        // CLI file / saved tabs -- assigning the project must happen after Show(), not before.
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.AnimationChainListSave.AnimationChains.Add(chain);

        typeof(MainWindow)
            .GetMethod("RefreshTreeView", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(window, null);
        Dispatcher.UIThread.RunJobs();

        // Force a full layout pass so the TreeViewItem template (and the lock button inside it)
        // is actually realized in the visual tree.
        window.Measure(new Avalonia.Size(1600, 900));
        window.Arrange(new Avalonia.Rect(0, 0, 1600, 900));
        Dispatcher.UIThread.RunJobs();

        return (window, ctx, chain);
    }

    private static TreeView GetTree(MainWindow w) => w.FindControl<TreeView>("AnimTree")!;

    private static Button GetLockButtonForChainRow(MainWindow window, AnimationChainSave chain)
    {
        var tree = GetTree(window);
        return tree.GetVisualDescendants()
            .OfType<Button>()
            .First(b => b.Classes.Contains("lock-btn") &&
                        b.DataContext is TreeNodeVm vm && ReferenceEquals(vm.Data, chain));
    }

    private static Button GetAddFrameButtonForChainRow(MainWindow window, AnimationChainSave chain)
    {
        var tree = GetTree(window);
        return tree.GetVisualDescendants()
            .OfType<Button>()
            .First(b => b.Classes.Contains("add-frame-btn") &&
                        b.DataContext is TreeNodeVm vm && ReferenceEquals(vm.Data, chain));
    }

    private static TextBlock GetMetaTextForChainRow(MainWindow window, AnimationChainSave chain)
    {
        var tree = GetTree(window);
        return tree.GetVisualDescendants()
            .OfType<TextBlock>()
            .First(t => t.Classes.Contains("meta") &&
                        t.DataContext is TreeNodeVm vm && ReferenceEquals(vm.Data, chain));
    }

    /// <summary>Hovers the pointer over the chain row's TreeViewItem so its <c>:pointerover</c>
    /// hover-reveal styles (add-frame/lock buttons, wide meta margin) apply.</summary>
    private static void HoverChainRow(MainWindow window, AnimationChainSave chain)
    {
        var tree = GetTree(window);
        var tvi = tree.GetVisualDescendants()
            .OfType<TreeViewItem>()
            .First(i => i.DataContext is TreeNodeVm vm && ReferenceEquals(vm.Data, chain));
        var local = new Point(tvi.Bounds.Width / 2, tvi.Bounds.Height / 2);
        var windowPoint = tvi.TranslatePoint(local, window)!.Value;
        window.MouseMove(windowPoint);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void LockButtonClick_TogglesChainIsLocked_AndIsUndoable()
    {
        var (window, ctx, chain) = CreateWindowWithChain();
        try
        {
            var lockBtn = GetLockButtonForChainRow(window, chain);

            lockBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            Assert.True(chain.IsLocked);
            Assert.True(ctx.UndoManager.CanUndo);

            ctx.UndoManager.Undo();
            Assert.False(chain.IsLocked);
        }
        finally { window.Close(); }
    }

    /// <summary>
    /// Issue #1042: multi-selecting chains A and B, then clicking B's lock icon, must keep both
    /// selected and lock B. Previously, <c>OnTreePointerPressed</c>'s Tunnel-phase press handling
    /// treated the press as a row press (armed via the chain-drag-candidate branch) because it
    /// never checked whether the press actually originated on a button embedded in the row
    /// template. Since B was already part of a multi-selection, that branch captured the pointer
    /// and marked the event Handled to defer to a single-select-on-release -- which both
    /// suppressed the lock button's own Click (so the lock never applied) and collapsed the
    /// selection down to just B on release.
    /// </summary>
    [AvaloniaFact]
    public void LockButtonClick_WhenChainPartOfMultiSelection_KeepsSelectionAndLocks()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.FileName = null;
        ctx.AppCommands.DoOnUiThread = a => a();
        ctx.AppCommands.FileDialogService = NullFileDialogService.Instance;

        var chainA = new AnimationChainSave { Name = "Walk" };
        var chainB = new AnimationChainSave { Name = "Run" };

        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.AnimationChainListSave.AnimationChains.Add(chainA);
        ctx.ProjectManager.AnimationChainListSave.AnimationChains.Add(chainB);

        typeof(MainWindow)
            .GetMethod("RefreshTreeView", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(window, null);
        Dispatcher.UIThread.RunJobs();

        window.Measure(new Avalonia.Size(1600, 900));
        window.Arrange(new Avalonia.Rect(0, 0, 1600, 900));
        Dispatcher.UIThread.RunJobs();

        try
        {
            // Multi-select both chains (lands in SelectedState the same way a real ctrl/shift
            // multi-select in the tree does; MainWindow's SelectionChanged→SyncTreeSelection then
            // pushes it into AnimTree.SelectedItems).
            ctx.SelectedState.SelectedNodes = new List<object> { chainA, chainB };
            Dispatcher.UIThread.RunJobs();

            var lockBtnB = GetLockButtonForChainRow(window, chainB);
            var local = new Point(lockBtnB.Bounds.Width / 2, lockBtnB.Bounds.Height / 2);
            var p = lockBtnB.TranslatePoint(local, window)!.Value;

            // Hover first so the lock button's hover-reveal styles make it hit-test-visible --
            // matches how a real user reaches it (Button.lock-btn is IsHitTestVisible=False until
            // TreeViewItem:pointerover or already locked).
            window.MouseMove(p);
            Dispatcher.UIThread.RunJobs();
            window.MouseDown(p, MouseButton.Left);
            window.MouseUp(p, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            Assert.True(chainB.IsLocked);
            Assert.Equal(2, ctx.SelectedState.SelectedNodes.Count);
            Assert.Contains(chainA, ctx.SelectedState.SelectedNodes);
            Assert.Contains(chainB, ctx.SelectedState.SelectedNodes);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void LockButtonClasses_ReflectLockedState()
    {
        var (window, _, chain) = CreateWindowWithChain();
        try
        {
            var lockBtn = GetLockButtonForChainRow(window, chain);
            Assert.DoesNotContain("locked", lockBtn.Classes);

            lockBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            Assert.Contains("locked", lockBtn.Classes);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void SelectingChainOnly_ShowsPropChainPanel_WithLockedCheckboxReflectingState()
    {
        var (window, ctx, chain) = CreateWindowWithChain();
        try
        {
            chain.IsLocked = true;
            ctx.SelectedState.SelectedChain = chain;
            Dispatcher.UIThread.RunJobs();

            var propChainPanel = window.FindControl<StackPanel>("PropChainPanel")!;
            var propNoneLabel  = window.FindControl<TextBlock>("PropNoneLabel")!;
            var propChainLocked = window.FindControl<CheckBox>("PropChainLocked")!;

            Assert.True(propChainPanel.IsVisible);
            Assert.False(propNoneLabel.IsVisible);
            Assert.True(propChainLocked.IsChecked);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void TogglingPropChainLockedCheckbox_SetsChainIsLocked()
    {
        var (window, ctx, chain) = CreateWindowWithChain();
        try
        {
            ctx.SelectedState.SelectedChain = chain;
            Dispatcher.UIThread.RunJobs();

            var propChainLocked = window.FindControl<CheckBox>("PropChainLocked")!;
            propChainLocked.IsChecked = true;
            Dispatcher.UIThread.RunJobs();

            Assert.True(chain.IsLocked);
        }
        finally { window.Close(); }
    }

    // ── Meta text vs. icon overlap (#1035) ────────────────────────────────────
    // The add-frame and lock buttons float over the row at the right edge; the meta text's
    // right margin must widen to clear whichever icon lane(s) are actually visible, or the
    // lock icon overlaps the meta text ("7 fr · 2.95s"). See the Margin selectors on
    // Button.meta in MainWindow.axaml for the four-state table this pins.

    [AvaloniaFact]
    public void AddFrameButton_HiddenWhenChainLocked()
    {
        var (window, ctx, chain) = CreateWindowWithChain();
        try
        {
            var addFrameBtn = GetAddFrameButtonForChainRow(window, chain);
            Assert.True(addFrameBtn.IsVisible);

            ctx.AppCommands.SetChainLocked(chain, true);
            Dispatcher.UIThread.RunJobs();

            Assert.False(addFrameBtn.IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void MetaTextMargin_UnlockedNotHovered_IsTight()
    {
        var (window, _, chain) = CreateWindowWithChain();
        try
        {
            var meta = GetMetaTextForChainRow(window, chain);
            Assert.Equal(8, meta.Margin.Right);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void MetaTextMargin_Locked_ClearsLockIconLane()
    {
        var (window, ctx, chain) = CreateWindowWithChain();
        try
        {
            ctx.AppCommands.SetChainLocked(chain, true);
            Dispatcher.UIThread.RunJobs();

            var meta = GetMetaTextForChainRow(window, chain);
            Assert.Equal(56, meta.Margin.Right);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void MetaTextMargin_UnlockedHovered_ClearsBothIconLanes()
    {
        var (window, _, chain) = CreateWindowWithChain();
        try
        {
            HoverChainRow(window, chain);

            var meta = GetMetaTextForChainRow(window, chain);
            Assert.Equal(56, meta.Margin.Right);
        }
        finally { window.Close(); }
    }
}
