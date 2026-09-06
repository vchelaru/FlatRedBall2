using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.ViewModels;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
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
}
