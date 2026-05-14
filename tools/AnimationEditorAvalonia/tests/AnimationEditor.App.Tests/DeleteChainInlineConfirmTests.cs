using AnimationEditor.Core.IO;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.App.Tests;

public class DeleteChainInlineConfirmTests
{
    private static (MainWindow Window, TestServices Ctx) CreateWindow()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.AppCommands.FileDialogService = NullFileDialogService.Instance;
        MainWindow window = ctx.CreateMainWindow();
        window.Show();
        return (window, ctx);
    }

    [AvaloniaFact]
    public void InlineConfirmPanel_HiddenInitially()
    {
        var (window, _) = CreateWindow();
        try
        {
            Border panel = window.FindControl<Border>("DeleteChainConfirmPanel")!;
            Assert.False(panel.IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void InlineConfirmPanel_ShowsWhenChainHasFrames()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            chain.Frames.Add(new AnimationFrameSave { TextureName = "Tex.png" });
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            window.ShowDeleteChainConfirmForTest(chain);
            Dispatcher.UIThread.RunJobs();

            Border panel = window.FindControl<Border>("DeleteChainConfirmPanel")!;
            Assert.True(panel.IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void InlineConfirmPanel_ConfirmDeletesChain()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            chain.Frames.Add(new AnimationFrameSave { TextureName = "Tex.png" });
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            window.ShowDeleteChainConfirmForTest(chain);
            Dispatcher.UIThread.RunJobs();

            Button confirmBtn = window.FindControl<Button>("DeleteChainConfirmBtn")!;
            confirmBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            Assert.Empty(ctx.ProjectManager.AnimationChainListSave!.AnimationChains);
            Border panel = window.FindControl<Border>("DeleteChainConfirmPanel")!;
            Assert.False(panel.IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void InlineConfirmPanel_CancelPreservesChain()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            chain.Frames.Add(new AnimationFrameSave { TextureName = "Tex.png" });
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            window.ShowDeleteChainConfirmForTest(chain);
            Dispatcher.UIThread.RunJobs();

            Button cancelBtn = window.FindControl<Button>("DeleteChainCancelBtn")!;
            cancelBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            Assert.Single(ctx.ProjectManager.AnimationChainListSave!.AnimationChains);
            Border panel = window.FindControl<Border>("DeleteChainConfirmPanel")!;
            Assert.False(panel.IsVisible);
        }
        finally { window.Close(); }
    }
}
