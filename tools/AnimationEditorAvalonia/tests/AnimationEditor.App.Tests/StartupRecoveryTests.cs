using System.Linq;
using System.Threading.Tasks;
using AnimationEditor.Core.IO;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Covers the crash-recovery restore-on-launch path wired through <c>MainWindow.OnOpened</c>
/// (issue #754 Phase 0): a recovery file written by <see cref="IIoManager.WriteRecoveryFile"/>
/// after an unclean shutdown must be offered back to the user on the next launch. The prompt
/// itself goes through <c>MainWindow.ShowTwoButtonDialogAsync</c> (custom Restore/Delete button
/// labels, #772 follow-up), not the shared <c>IAppCommands.ConfirmAsync</c> Yes/No dialog.
/// </summary>
public class StartupRecoveryTests
{
    [AvaloniaFact]
    public void NoRecoveryFile_StartsNormally_NeverPrompts()
    {
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        int confirmCalls = 0;
        // Must be set AFTER CreateMainWindow: the constructor's WireAppCommands wires this to
        // the real dialog, so a mock set beforehand gets overwritten. Setting it here (before
        // Show, which fires Opened) is what actually takes effect.
        window.ShowTwoButtonDialogAsync = (_, _, _, _) => { confirmCalls++; return Task.FromResult(true); };

        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            Assert.Equal(0, confirmCalls);
            Assert.NotNull(ctx.ProjectManager.AnimationChainListSave);
            Assert.Empty(ctx.ProjectManager.AnimationChainListSave!.AnimationChains);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RecoveryFilePresent_UserConfirms_RestoresContentAsUnsavedDocument()
    {
        var ctx = TestHelpers.BuildServices();
        var seed = new AnimationChainListSave();
        seed.AnimationChains.Add(new AnimationChainSave { Name = "Recovered" });
        ctx.IoManager.WriteRecoveryFile(seed);

        var window = ctx.CreateMainWindow();
        window.ShowTwoButtonDialogAsync = (_, _, _, _) => Task.FromResult(true);

        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            Assert.Contains(ctx.ProjectManager.AnimationChainListSave!.AnimationChains, c => c.Name == "Recovered");
            Assert.Null(ctx.ProjectManager.FileName);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RecoveryFilePresent_UserConfirms_DeletesRecoveryFileAfterRestore()
    {
        // Otherwise the same recovery file re-prompts on every subsequent launch, whether or
        // not the user goes on to Save As the restored content.
        var ctx = TestHelpers.BuildServices();
        var seed = new AnimationChainListSave();
        seed.AnimationChains.Add(new AnimationChainSave { Name = "Recovered" });
        ctx.IoManager.WriteRecoveryFile(seed);

        var window = ctx.CreateMainWindow();
        window.ShowTwoButtonDialogAsync = (_, _, _, _) => Task.FromResult(true);

        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            Assert.False(ctx.IoManager.RecoveryFileExists());
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RecoveryFilePresent_UserDeclines_DeletesFileAndStartsNormally()
    {
        var ctx = TestHelpers.BuildServices();
        var seed = new AnimationChainListSave();
        seed.AnimationChains.Add(new AnimationChainSave { Name = "Recovered" });
        ctx.IoManager.WriteRecoveryFile(seed);

        var window = ctx.CreateMainWindow();
        window.ShowTwoButtonDialogAsync = (_, _, _, _) => Task.FromResult(false);

        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            Assert.False(ctx.IoManager.RecoveryFileExists());
            Assert.NotNull(ctx.ProjectManager.AnimationChainListSave);
            Assert.Empty(ctx.ProjectManager.AnimationChainListSave!.AnimationChains);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RecoveryFilePresent_PromptsWithRestoreDeleteLabels_NotYesNo()
    {
        var ctx = TestHelpers.BuildServices();
        var seed = new AnimationChainListSave();
        seed.AnimationChains.Add(new AnimationChainSave { Name = "Recovered" });
        ctx.IoManager.WriteRecoveryFile(seed);

        var window = ctx.CreateMainWindow();
        string? confirmLabel = null, cancelLabel = null;
        window.ShowTwoButtonDialogAsync = (_, _, confirm, cancel) =>
        {
            confirmLabel = confirm;
            cancelLabel = cancel;
            return Task.FromResult(true);
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            Assert.Equal("Restore", confirmLabel);
            Assert.Equal("Delete", cancelLabel);
        }
        finally { window.Close(); }
    }
}
