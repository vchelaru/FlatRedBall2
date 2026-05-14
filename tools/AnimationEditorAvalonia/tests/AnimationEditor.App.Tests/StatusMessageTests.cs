using AnimationEditor.Core.IO;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.App.Tests;

public class StatusMessageTests
{
    private static (MainWindow Window, TestServices Ctx) CreateWindow()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName               = null;
        ctx.AppCommands.ConfirmAsync              = (_, _) => Task.FromResult(true);
        ctx.AppCommands.FileDialogService         = NullFileDialogService.Instance;
        var window = ctx.CreateMainWindow();
        window.Show();
        return (window, ctx);
    }

    [AvaloniaFact]
    public void CommitInlineRename_EmptyName_ShowsStatusMessage()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = ctx.AppCommands.AddAnimationChainWithName("Walk")!;
            Dispatcher.UIThread.RunJobs();

            var vm = window.GetTreeRoots().First(v => v.Data == chain);
            window.CommitInlineRenamePublic(vm, "");
            Dispatcher.UIThread.RunJobs();

            var msg = window.FindControl<TextBlock>("StatusMessage")!;
            Assert.True(msg.IsVisible);
            Assert.Contains("cannot be empty", msg.Text);
            Assert.Equal("Walk", chain.Name);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void LoadFailed_Event_ShowsStatusBarMessage()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            ctx.AppCommands.LoadAnimationChain("hero.achx");
            Dispatcher.UIThread.RunJobs();

            var msg = window.FindControl<TextBlock>("StatusMessage")!;
            Assert.True(msg.IsVisible);
            Assert.Contains("hero.achx", msg.Text);
        }
        finally { window.Close(); }
    }
}
