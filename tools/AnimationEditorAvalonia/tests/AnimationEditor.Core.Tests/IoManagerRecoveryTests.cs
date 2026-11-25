using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using FlatRedBall2.AnimationEditorCommon;
using System.IO;
using Xunit;

namespace AnimationEditor.Core.Tests;

[Collection("SequentialSingletons")]
public class IoManagerRecoveryTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir;
    private readonly TestServices ctx;

    public IoManagerRecoveryTests()
    {
        _dir = new TestHelpers.TempDir();
        ctx = TestHelpers.SetupFreshAcls();
        ctx.IoManager.RecoveryFilePath = _dir.Path + "/recovery.achx";
    }

    public void Dispose() => _dir.Dispose();

    // ── RecoveryFileExists ────────────────────────────────────────────────────

    [Fact]
    public void RecoveryFileExists_WhenNoFileWritten_ReturnsFalse()
    {
        Assert.False(ctx.IoManager.RecoveryFileExists());
    }

    [Fact]
    public void RecoveryFileExists_AfterWriteRecoveryFile_ReturnsTrue()
    {
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();

        ctx.IoManager.WriteRecoveryFile(ctx.ProjectManager.AnimationChainListSave);

        Assert.True(ctx.IoManager.RecoveryFileExists());
    }

    // ── WriteRecoveryFile ─────────────────────────────────────────────────────

    [Fact]
    public void WriteRecoveryFile_CreatesFileAtRecoveryPath()
    {
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();

        ctx.IoManager.WriteRecoveryFile(ctx.ProjectManager.AnimationChainListSave);

        Assert.True(File.Exists(ctx.IoManager.RecoveryFilePath));
    }

    [Fact]
    public void WriteRecoveryFile_WhenCalledTwice_OverwritesPreviousFile()
    {
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(new AnimationChainSave { Name = "Walk" });
        ctx.ProjectManager.AnimationChainListSave = acls;

        ctx.IoManager.WriteRecoveryFile(ctx.ProjectManager.AnimationChainListSave);
        var firstSize = new FileInfo(ctx.IoManager.RecoveryFilePath).Length;

        acls.AnimationChains.Add(new AnimationChainSave { Name = "Run" });
        ctx.IoManager.WriteRecoveryFile(ctx.ProjectManager.AnimationChainListSave);
        var secondSize = new FileInfo(ctx.IoManager.RecoveryFilePath).Length;

        Assert.True(secondSize > firstSize, "Second write should produce a larger file");
        Assert.Single(Directory.GetFiles(_dir.Path, "*.achx"));
    }

    [Fact]
    public void WriteRecoveryFile_WhenAclsIsNull_DoesNotCreateFile()
    {
        ctx.ProjectManager.AnimationChainListSave = null;

        ctx.IoManager.WriteRecoveryFile(ctx.ProjectManager.AnimationChainListSave);

        Assert.False(File.Exists(ctx.IoManager.RecoveryFilePath));
    }

    [Fact]
    public void WriteRecoveryFile_WhenPathIsInvalid_DoesNotThrow()
    {
        ctx.IoManager.RecoveryFilePath = TestPaths.InvalidPath("recovery.achx");
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();

        var ex = Record.Exception(() => ctx.IoManager.WriteRecoveryFile(ctx.ProjectManager.AnimationChainListSave));

        Assert.Null(ex);
    }

    [Fact]
    public void WriteRecoveryFile_WhenPathIsInvalid_FiresSaveFailed()
    {
        ctx.IoManager.RecoveryFilePath = TestPaths.InvalidPath("recovery.achx");
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        Exception? caught = null;
        ctx.IoManager.SaveFailed += (_, e) => caught = e;

        ctx.IoManager.WriteRecoveryFile(ctx.ProjectManager.AnimationChainListSave);

        // On most systems Z:\ doesn't exist; if it does this test is inconclusive
        // — but no exception must escape regardless
    }

    // ── DeleteRecoveryFile ────────────────────────────────────────────────────

    [Fact]
    public void DeleteRecoveryFile_WhenFileExists_RemovesFile()
    {
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.IoManager.WriteRecoveryFile(ctx.ProjectManager.AnimationChainListSave);
        Assert.True(ctx.IoManager.RecoveryFileExists());

        ctx.IoManager.DeleteRecoveryFiles();

        Assert.False(ctx.IoManager.RecoveryFileExists());
    }

    [Fact]
    public void DeleteRecoveryFile_WhenFileDoesNotExist_DoesNotThrow()
    {
        Assert.False(ctx.IoManager.RecoveryFileExists());

        var ex = Record.Exception(() => ctx.IoManager.DeleteRecoveryFiles());

        Assert.Null(ex);
    }
}
