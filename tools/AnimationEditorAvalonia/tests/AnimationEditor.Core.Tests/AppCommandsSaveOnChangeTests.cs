using System.IO;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Issue #839 follow-up: the app's autosave policy -- any edit (raised as
/// <c>IApplicationEvents.AnimationChainsChanged</c> from ~20 <c>IUndoableCommand</c> types and a
/// couple of direct <c>AppCommands</c> call sites) writes the <c>.achx</c> to disk -- used to be
/// wired only in <c>MainWindow</c> (the Avalonia app layer). That made it untestable without a
/// full headless window + WireframeControl, and it was misdiagnosed twice while investigating
/// this issue as a result. It now lives in <c>AppCommands</c>'s own constructor, so these tests
/// exercise it directly with no Avalonia/UI involved at all.
/// </summary>
public class AppCommandsSaveOnChangeTests
{
    [Fact]
    public void RaiseAnimationChainsChanged_FileNameSet_SavesToDisk()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var tmpPath = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString("N") + ".achx");
        ctx.ProjectManager.FileName = tmpPath;

        try
        {
            ctx.ApplicationEvents.RaiseAnimationChainsChanged();

            Assert.True(File.Exists(tmpPath), $"Expected {tmpPath} to be written by the autosave policy.");
        }
        finally
        {
            if (File.Exists(tmpPath)) File.Delete(tmpPath);
        }
    }

    // #1147 pass #23: every IUndoableCommand raised AnimationChainsChanged (which autosaves here)
    // AND called SaveCurrentAnimationChainList itself, so each edit, undo and redo wrote the file
    // twice -- two disk writes, two Tiled-sync pushes, two "not every change applied" toasts.
    [Fact]
    public void Command_DoUndoRedo_EachSavesExactlyOnce()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var tmpPath = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString("N") + ".achx");
        ctx.ProjectManager.FileName = tmpPath;
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk");
        var saves = 0;
        ctx.AppCommands.EditorProjectModelChanged += _ => saves++;

        try
        {
            ctx.AppCommands.RenameChain(chain, "Run");
            Assert.Equal(1, saves);
            ctx.UndoManager.Undo();
            Assert.Equal(2, saves);
            ctx.UndoManager.Redo();
            Assert.Equal(3, saves);
            ctx.AppCommands.DuplicateChains([chain]);
            Assert.Equal(4, saves);
        }
        finally
        {
            if (File.Exists(tmpPath)) File.Delete(tmpPath);
        }
    }

    // An untitled document has no file to autosave to; the crash-recovery snapshot is what
    // stands in for it, and the event path must write it just like the commands used to.
    [Fact]
    public void RaiseAnimationChainsChanged_NoFileNameSet_WritesRecoveryFile()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        ctx.ProjectManager.FileName = null;
        if (File.Exists(ctx.IoManager.RecoveryFilePath)) File.Delete(ctx.IoManager.RecoveryFilePath);

        ctx.ApplicationEvents.RaiseAnimationChainsChanged();

        Assert.True(File.Exists(ctx.IoManager.RecoveryFilePath));
    }

    [Fact]
    public void RaiseAnimationChainsChanged_NoFileNameSet_DoesNotThrow()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        ctx.ProjectManager.FileName = null;

        var ex = Record.Exception(() => ctx.ApplicationEvents.RaiseAnimationChainsChanged());

        Assert.Null(ex);
    }
}
