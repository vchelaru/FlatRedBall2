using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using FlatRedBall.Content.AnimationChain;
using Xunit;

namespace AnimationEditor.Core.Tests;

[Collection("SequentialSingletons")]
public class AppCommandsRecoveryTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir;

    public AppCommandsRecoveryTests()
    {
        _dir = new TestHelpers.TempDir();
        TestHelpers.SetupFreshAcls();
        IoManager.Self.RecoveryFilePath = _dir.Path + "/recovery.achx";
    }

    public void Dispose() => _dir.Dispose();

    // ── Recovery write via SaveCurrentAnimationChainList ─────────────────────

    [Fact]
    public void SaveCurrentAnimationChainList_WhenFileNameIsNull_WritesRecoveryFile()
    {
        ProjectManager.Self.FileName = null;
        ProjectManager.Self.AnimationChainListSave = new AnimationChainListSave();

        AppCommands.Self.SaveCurrentAnimationChainList();

        Assert.True(IoManager.Self.RecoveryFileExists());
    }

    [Fact]
    public void SaveCurrentAnimationChainList_WhenFileNameIsSet_DoesNotWriteRecoveryFile()
    {
        var target = _dir.Path + "/hero.achx";
        ProjectManager.Self.AnimationChainListSave = new AnimationChainListSave();
        ProjectManager.Self.FileName = target;

        AppCommands.Self.SaveCurrentAnimationChainList();

        Assert.False(IoManager.Self.RecoveryFileExists());
    }

    [Fact]
    public void SaveCurrentAnimationChainList_WhenFileNameIsNull_RecoveryFileContainsChainData()
    {
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(new AnimationChainSave { Name = "Idle" });
        ProjectManager.Self.AnimationChainListSave = acls;
        ProjectManager.Self.FileName = null;

        AppCommands.Self.SaveCurrentAnimationChainList();

        var xml = File.ReadAllText(IoManager.Self.RecoveryFilePath);
        Assert.Contains("Idle", xml);
    }

    // ── Recovery deletion via SaveCurrentAnimationChainListAsync ─────────────

    [Fact]
    public async Task SaveCurrentAnimationChainListAsync_WhenSaved_DeletesRecoveryFile()
    {
        var target = _dir.Path + "/out.achx";
        ProjectManager.Self.AnimationChainListSave = new AnimationChainListSave();
        AppCommands.Self.FileDialogService = new StubFileDialogService(target);

        // Write a recovery file first
        IoManager.Self.WriteRecoveryFile();
        Assert.True(IoManager.Self.RecoveryFileExists());

        await AppCommands.Self.SaveCurrentAnimationChainListAsync();

        Assert.False(IoManager.Self.RecoveryFileExists());
    }

    [Fact]
    public async Task SaveCurrentAnimationChainListAsync_WhenDialogCancelled_PreservesRecoveryFile()
    {
        ProjectManager.Self.AnimationChainListSave = new AnimationChainListSave();
        AppCommands.Self.FileDialogService = new StubFileDialogService(null);

        IoManager.Self.WriteRecoveryFile();
        Assert.True(IoManager.Self.RecoveryFileExists());

        await AppCommands.Self.SaveCurrentAnimationChainListAsync();

        Assert.True(IoManager.Self.RecoveryFileExists(), "Recovery should be preserved when user cancels Save As");
    }
}
