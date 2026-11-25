using AnimationEditor.Core.CommandsAndState.Commands;
using System;
using System.IO;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Tests that AppCommands.SaveCurrentAnimationChainList transitions the undo
/// manager to SaveState.Failed when the underlying save throws.
/// </summary>
public class AppCommandsSaveFailTests
{
    [Fact]
    public void SaveCurrentAnimationChainList_SetsFailedState_WhenSaveThrows()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        ctx.ProjectManager.FileName = Path.Combine(dir.Path, "missing", "file.achx");

        ctx.AppCommands.SaveCurrentAnimationChainList();

        Assert.Equal(SaveState.Failed, ctx.UndoManager.SaveState);
    }

    // The edit is already made, so it must still reach the undo history.
    [Fact]
    public void SaveCurrentAnimationChainList_WhenAListenerThrows_FailsAndTheEditCanBeUndone()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        ctx.ProjectManager.FileName = Path.Combine(dir.Path, "file.achx");
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk");
        string? message = null;
        ctx.AppCommands.SaveFailed += text => message = text;
        ctx.AppCommands.EditorProjectModelChanged += _ => throw new InvalidOperationException("boom");

        ctx.AppCommands.RenameChain(chain, "Run");

        Assert.Equal("boom", message);
        Assert.Equal(SaveState.Failed, ctx.UndoManager.SaveState);
        ctx.UndoManager.Undo();
        Assert.Equal("Walk", chain.Name);
    }

    [Fact]
    public void SaveCurrentAnimationChainList_SetsAutoSaveOn_WhenSaveSucceeds()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        ctx.ProjectManager.FileName = "/some/file.achx";

        // Point to a real writable temp file so the save actually succeeds.
        var tmpPath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".achx");
        try
        {
            ctx.AppCommands.SaveCurrentAnimationChainList(tmpPath);

            Assert.Equal(SaveState.AutoSaveOn, ctx.UndoManager.SaveState);
        }
        finally
        {
            if (System.IO.File.Exists(tmpPath))
                System.IO.File.Delete(tmpPath);
        }
    }
}
