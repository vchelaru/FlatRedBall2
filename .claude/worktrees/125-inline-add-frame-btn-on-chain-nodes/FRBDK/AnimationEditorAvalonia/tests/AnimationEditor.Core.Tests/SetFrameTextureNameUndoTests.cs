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
        var acls = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(acls, "Walk");
        var frame = TestHelpers.MakeFrame("old.png");
        chain.Frames.Add(frame);

        AppCommands.Self.SetFrameTextureName(frame, "new.png");
        Assert.Equal("new.png", frame.TextureName);

        UndoManager.Self.Undo();

        Assert.Equal("old.png", frame.TextureName);
    }

    [Fact]
    public void SetFrameTextureName_UndoThenRedo_ReappliesNewName()
    {
        var acls = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(acls, "Walk");
        var frame = TestHelpers.MakeFrame("original.png");
        chain.Frames.Add(frame);

        AppCommands.Self.SetFrameTextureName(frame, "updated.png");
        UndoManager.Self.Undo();
        Assert.Equal("original.png", frame.TextureName);

        UndoManager.Self.Redo();

        Assert.Equal("updated.png", frame.TextureName);
    }

    // ── RenameFrame + Undo ────────────────────────────────────────────────────

    [Fact]
    public void RenameFrame_Undo_RestoresOldTextureName()
    {
        var acls = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(acls, "Walk");
        var frame = TestHelpers.MakeFrame("before.png");
        chain.Frames.Add(frame);

        AppCommands.Self.RenameFrame(frame, "after.png");
        Assert.Equal("after.png", frame.TextureName);

        UndoManager.Self.Undo();

        Assert.Equal("before.png", frame.TextureName);
    }
}
