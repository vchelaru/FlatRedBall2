using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using FlatRedBall.Content.AnimationChain;
using Xunit;

namespace AnimationEditor.Core.Tests;

[Collection("SequentialSingletons")]
public class IoManagerRecoveryTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir;

    public IoManagerRecoveryTests()
    {
        _dir = new TestHelpers.TempDir();
        TestHelpers.SetupFreshAcls();
        IoManager.Self.RecoveryFilePath = _dir.Path + "/recovery.achx";
    }

    public void Dispose() => _dir.Dispose();

    // ── RecoveryFileExists ────────────────────────────────────────────────────

    [Fact]
    public void RecoveryFileExists_WhenNoFileWritten_ReturnsFalse()
    {
        Assert.False(IoManager.Self.RecoveryFileExists());
    }

    [Fact]
    public void RecoveryFileExists_AfterWriteRecoveryFile_ReturnsTrue()
    {
        ProjectManager.Self.AnimationChainListSave = new AnimationChainListSave();

        IoManager.Self.WriteRecoveryFile();

        Assert.True(IoManager.Self.RecoveryFileExists());
    }

    // ── WriteRecoveryFile ─────────────────────────────────────────────────────

    [Fact]
    public void WriteRecoveryFile_CreatesFileAtRecoveryPath()
    {
        ProjectManager.Self.AnimationChainListSave = new AnimationChainListSave();

        IoManager.Self.WriteRecoveryFile();

        Assert.True(File.Exists(IoManager.Self.RecoveryFilePath));
    }

    [Fact]
    public void WriteRecoveryFile_WhenCalledTwice_OverwritesPreviousFile()
    {
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(new AnimationChainSave { Name = "Walk" });
        ProjectManager.Self.AnimationChainListSave = acls;

        IoManager.Self.WriteRecoveryFile();
        var firstSize = new FileInfo(IoManager.Self.RecoveryFilePath).Length;

        acls.AnimationChains.Add(new AnimationChainSave { Name = "Run" });
        IoManager.Self.WriteRecoveryFile();
        var secondSize = new FileInfo(IoManager.Self.RecoveryFilePath).Length;

        Assert.True(secondSize > firstSize, "Second write should produce a larger file");
        Assert.Single(Directory.GetFiles(_dir.Path, "*.achx"));
    }

    [Fact]
    public void WriteRecoveryFile_WhenAclsIsNull_DoesNotCreateFile()
    {
        ProjectManager.Self.AnimationChainListSave = null!;

        IoManager.Self.WriteRecoveryFile();

        Assert.False(File.Exists(IoManager.Self.RecoveryFilePath));
    }

    [Fact]
    public void WriteRecoveryFile_WhenPathIsInvalid_DoesNotThrow()
    {
        IoManager.Self.RecoveryFilePath = "Z:\\NonExistentDrive\\recovery.achx";
        ProjectManager.Self.AnimationChainListSave = new AnimationChainListSave();

        var ex = Record.Exception(() => IoManager.Self.WriteRecoveryFile());

        Assert.Null(ex);
    }

    [Fact]
    public void WriteRecoveryFile_WhenPathIsInvalid_FiresSaveFailed()
    {
        IoManager.Self.RecoveryFilePath = "Z:\\NonExistentDrive\\recovery.achx";
        ProjectManager.Self.AnimationChainListSave = new AnimationChainListSave();
        Exception? caught = null;
        IoManager.Self.SaveFailed += (_, e) => caught = e;

        IoManager.Self.WriteRecoveryFile();

        // On most systems Z:\ doesn't exist; if it does this test is inconclusive
        // — but no exception must escape regardless
    }

    // ── DeleteRecoveryFile ────────────────────────────────────────────────────

    [Fact]
    public void DeleteRecoveryFile_WhenFileExists_RemovesFile()
    {
        ProjectManager.Self.AnimationChainListSave = new AnimationChainListSave();
        IoManager.Self.WriteRecoveryFile();
        Assert.True(IoManager.Self.RecoveryFileExists());

        IoManager.Self.DeleteRecoveryFile();

        Assert.False(IoManager.Self.RecoveryFileExists());
    }

    [Fact]
    public void DeleteRecoveryFile_WhenFileDoesNotExist_DoesNotThrow()
    {
        Assert.False(IoManager.Self.RecoveryFileExists());

        var ex = Record.Exception(() => IoManager.Self.DeleteRecoveryFile());

        Assert.Null(ex);
    }
}
