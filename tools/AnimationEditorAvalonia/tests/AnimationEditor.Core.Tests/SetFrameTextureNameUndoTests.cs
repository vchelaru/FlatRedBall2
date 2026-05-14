using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using Xunit;

namespace AnimationEditor.Core.Tests;

[Collection("SequentialSingletons")]
public class SetFrameTextureNameUndoTests
{
    // ── SetFrameTextureName + Undo ────────────────────────────────────────────

    [Fact]
    public void SetFrameTextureName_Undo_RestoresOldTextureName()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk");
        var frame = TestHelpers.MakeFrame("old.png");
        chain.Frames.Add(frame);

        ctx.AppCommands.SetFrameTextureName(frame, "new.png");
        Assert.Equal("new.png", frame.TextureName);

        ctx.UndoManager.Undo();

        Assert.Equal("old.png", frame.TextureName);
    }

    [Fact]
    public void SetFrameTextureName_UndoThenRedo_ReappliesNewName()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk");
        var frame = TestHelpers.MakeFrame("original.png");
        chain.Frames.Add(frame);

        ctx.AppCommands.SetFrameTextureName(frame, "updated.png");
        ctx.UndoManager.Undo();
        Assert.Equal("original.png", frame.TextureName);

        ctx.UndoManager.Redo();

        Assert.Equal("updated.png", frame.TextureName);
    }
}
