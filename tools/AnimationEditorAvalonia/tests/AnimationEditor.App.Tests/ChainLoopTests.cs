using AnimationEditor.Core.IO;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Headless coverage for the chain-loop feature's UI surfaces (issue #1120): the Inspector's
/// "Loop" checkbox and the timeline's Loop toggle both reflect and toggle
/// <see cref="AnimationChainSave.Loop"/> through <c>AppCommands.SetChainLoop</c>, and can never
/// disagree since both write to the same persisted value.
/// </summary>
public class ChainLoopTests
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

        return (window, ctx, chain);
    }

    [AvaloniaFact]
    public void SelectingChain_ShowsPropChainLoopCheckbox_ReflectingChainsLoopValue()
    {
        var (window, ctx, chain) = CreateWindowWithChain();
        try
        {
            chain.Loop = false;
            ctx.SelectedState.SelectedChain = chain;
            Dispatcher.UIThread.RunJobs();

            var propChainLoop = window.FindControl<CheckBox>("PropChainLoop")!;

            Assert.False(propChainLoop.IsChecked);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void TogglingPropChainLoopCheckbox_SetsChainLoop()
    {
        var (window, ctx, chain) = CreateWindowWithChain();
        try
        {
            ctx.SelectedState.SelectedChain = chain;
            Dispatcher.UIThread.RunJobs();

            var propChainLoop = window.FindControl<CheckBox>("PropChainLoop")!;
            propChainLoop.IsChecked = false;
            Dispatcher.UIThread.RunJobs();

            Assert.False(chain.Loop);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void TogglingLoopToggle_SetsChainLoop_AndPropChainLoopStaysInSync()
    {
        var (window, ctx, chain) = CreateWindowWithChain();
        try
        {
            ctx.SelectedState.SelectedChain = chain;
            Dispatcher.UIThread.RunJobs();

            var loopToggle = window.FindControl<ToggleButton>("LoopToggle")!;
            var propChainLoop = window.FindControl<CheckBox>("PropChainLoop")!;

            loopToggle.IsChecked = false;
            Dispatcher.UIThread.RunJobs();

            Assert.False(chain.Loop);
            Assert.False(propChainLoop.IsChecked);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void SelectingDifferentChain_ResyncsLoopToggle_ToThatChainsOwnValue()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.FileName = null;
        ctx.AppCommands.DoOnUiThread = a => a();
        ctx.AppCommands.FileDialogService = NullFileDialogService.Instance;

        var loopingChain = new AnimationChainSave { Name = "Walk" };
        var nonLoopingChain = new AnimationChainSave { Name = "Attack", Loop = false };

        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.AnimationChainListSave.AnimationChains.Add(loopingChain);
        ctx.ProjectManager.AnimationChainListSave.AnimationChains.Add(nonLoopingChain);

        try
        {
            ctx.SelectedState.SelectedChain = loopingChain;
            Dispatcher.UIThread.RunJobs();
            var loopToggle = window.FindControl<ToggleButton>("LoopToggle")!;
            Assert.True(loopToggle.IsChecked);

            ctx.SelectedState.SelectedChain = nonLoopingChain;
            Dispatcher.UIThread.RunJobs();

            Assert.False(loopToggle.IsChecked);
        }
        finally { window.Close(); }
    }
}
