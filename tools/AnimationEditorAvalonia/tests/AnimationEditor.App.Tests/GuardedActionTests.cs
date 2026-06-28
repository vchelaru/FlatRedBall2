using AnimationEditor.Core.IO;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using System;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Copy/Paste run fire-and-forget (<c>_ = HandleCopyAsync()</c>), so before this
/// guard an exception in them vanished as an unobserved task exception — which is
/// exactly why a clipboard-serialization failure looked like "nothing happened".
/// RunGuardedAsync must surface any failure as a visible error banner.
/// </summary>
public class GuardedActionTests
{
    private static (MainWindow Window, TestServices Ctx) CreateWindow()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName = null;
        ctx.AppCommands.ConfirmAsync = (_, _) => Task.FromResult(true);
        ctx.AppCommands.FileDialogService = NullFileDialogService.Instance;
        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, ctx);
    }

    [AvaloniaFact]
    public async Task RunGuardedAsync_ActionThrows_ShowsErrorStatusAndDoesNotRethrow()
    {
        var (window, _) = CreateWindow();
        try
        {
            // Must not throw — the whole point is that fire-and-forget callers stay safe.
            await window.RunGuardedAsync(
                () => throw new InvalidOperationException("boom"), "Copy");
            Dispatcher.UIThread.RunJobs();

            var banner = window.FindControl<Border>("ErrorBanner")!;
            var text   = window.FindControl<TextBlock>("ErrorBannerText")!;
            Assert.True(banner.IsVisible);
            Assert.Contains("Copy", text.Text);
            Assert.Contains("boom", text.Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task RunGuardedAsync_ActionSucceeds_DoesNotShowError()
    {
        var (window, _) = CreateWindow();
        try
        {
            var msg = window.FindControl<TextBlock>("StatusMessage")!;
            Assert.False(msg.IsVisible);

            await window.RunGuardedAsync(() => Task.CompletedTask, "Copy");
            Dispatcher.UIThread.RunJobs();

            Assert.False(msg.IsVisible);
        }
        finally { window.Close(); }
    }
}
