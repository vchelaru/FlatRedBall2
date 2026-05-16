using AnimationEditor.Core.CommandsAndState;
using Xunit;

namespace AnimationEditor.Core.Tests;

[Collection("SequentialSingletons")]
public class RenameFrameUndoTests
{
    // ── RenameFrame ───────────────────────────────────────────────────────────

    [Fact]
    public void RenameFrame_SetsNameAndCustomFlag()
    {
        var ctx   = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", frameCount: 1);
        var frame = chain.Frames[0];

        ctx.AppCommands.RenameFrame(frame, "Hit Frame");

        Assert.Equal("Hit Frame", frame.Name);
        Assert.True(frame.HasCustomName);
    }

    [Fact]
    public void RenameFrame_Undo_RestoresOriginalState()
    {
        var ctx   = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", frameCount: 1);
        var frame = chain.Frames[0];
        string originalName    = frame.Name;
        bool   originalCustom  = frame.HasCustomName;

        ctx.AppCommands.RenameFrame(frame, "Custom");
        ctx.UndoManager.Undo();

        Assert.Equal(originalName,   frame.Name);
        Assert.Equal(originalCustom, frame.HasCustomName);
    }

    [Fact]
    public void RenameFrame_UndoThenRedo_ReappliesCustomName()
    {
        var ctx   = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", frameCount: 1);
        var frame = chain.Frames[0];

        ctx.AppCommands.RenameFrame(frame, "Custom");
        ctx.UndoManager.Undo();
        ctx.UndoManager.Redo();

        Assert.Equal("Custom", frame.Name);
        Assert.True(frame.HasCustomName);
    }
}
