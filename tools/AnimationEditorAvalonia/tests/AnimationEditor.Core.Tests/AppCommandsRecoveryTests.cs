using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using FlatRedBall2.AnimationEditorCommon;
using System.IO;
using Xunit;

namespace AnimationEditor.Core.Tests;

[Collection("SequentialSingletons")]
public class AppCommandsRecoveryTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir;
    private readonly TestServices ctx;

    public AppCommandsRecoveryTests()
    {
        _dir = new TestHelpers.TempDir();
        ctx = TestHelpers.SetupFreshAcls();
        ctx.IoManager.RecoveryFilePath = _dir.Path + "/recovery.achx";
    }

    public void Dispose() => _dir.Dispose();

    // ── Recovery write via SaveCurrentAnimationChainList ─────────────────────

    [Fact]
    public void SaveCurrentAnimationChainList_WhenFileNameIsNull_WritesRecoveryFile()
    {
        ctx.ProjectManager.FileName = null;
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();

        ctx.AppCommands.SaveCurrentAnimationChainList();

        Assert.True(ctx.IoManager.RecoveryFileExists());
    }

    [Fact]
    public void SaveCurrentAnimationChainList_WhenFileNameIsSet_DoesNotWriteRecoveryFile()
    {
        var target = _dir.Path + "/hero.achx";
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName = target;

        ctx.AppCommands.SaveCurrentAnimationChainList();

        Assert.False(ctx.IoManager.RecoveryFileExists());
    }

    [Fact]
    public void SaveCurrentAnimationChainList_WhenFileNameIsNull_RecoveryFileContainsChainData()
    {
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(new AnimationChainSave { Name = "Idle" });
        ctx.ProjectManager.AnimationChainListSave = acls;
        ctx.ProjectManager.FileName = null;

        ctx.AppCommands.SaveCurrentAnimationChainList();

        var xml = File.ReadAllText(ctx.IoManager.RecoveryFilePath);
        Assert.Contains("Idle", xml);
    }

    // ── Recovery deletion via SaveCurrentAnimationChainListAsync ─────────────

    [Fact]
    public async Task SaveCurrentAnimationChainListAsync_WhenSaved_DeletesRecoveryFile()
    {
        var target = _dir.Path + "/out.achx";
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.AppCommands.FileDialogService = new StubFileDialogService(target);

        // Write a recovery file first
        ctx.IoManager.WriteRecoveryFile(ctx.ProjectManager.AnimationChainListSave);
        Assert.True(ctx.IoManager.RecoveryFileExists());

        await ctx.AppCommands.SaveCurrentAnimationChainListAsync();

        Assert.False(ctx.IoManager.RecoveryFileExists());
    }

    [Fact]
    public async Task SaveCurrentAnimationChainListAsync_WhenDialogCancelled_PreservesRecoveryFile()
    {
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.AppCommands.FileDialogService = new StubFileDialogService(null);

        ctx.IoManager.WriteRecoveryFile(ctx.ProjectManager.AnimationChainListSave);
        Assert.True(ctx.IoManager.RecoveryFileExists());

        await ctx.AppCommands.SaveCurrentAnimationChainListAsync();

        Assert.True(ctx.IoManager.RecoveryFileExists(), "Recovery should be preserved when user cancels Save As");
    }

    // ── Recovery deletion on a normal window close (#894) ────────────────────

    [Fact]
    public void HandleApplicationClosing_WhenFileNameIsNull_DeletesRecoveryFile()
    {
        // Simulates an unsaved document (FileName never set) that picked up a
        // recovery file from an earlier edit, then closed normally rather than crashing.
        ctx.ProjectManager.FileName = null;
        ctx.IoManager.WriteRecoveryFile(new AnimationChainListSave());
        Assert.True(ctx.IoManager.RecoveryFileExists());

        ctx.AppCommands.HandleApplicationClosing();

        Assert.False(ctx.IoManager.RecoveryFileExists(),
            "A normal close should not leave a stray recovery file behind to falsely trigger the 'closed unexpectedly' prompt on next launch.");
    }

    [Fact]
    public void HandleApplicationClosing_WhenFileNameIsSet_LeavesNoRecoveryFileBehind()
    {
        ctx.ProjectManager.FileName = _dir.Path + "/hero.achx";

        ctx.AppCommands.HandleApplicationClosing();

        Assert.False(ctx.IoManager.RecoveryFileExists());
    }
}
