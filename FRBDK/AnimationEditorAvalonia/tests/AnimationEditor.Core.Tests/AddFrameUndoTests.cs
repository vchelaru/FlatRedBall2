using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using FlatRedBall.Content.AnimationChain;
using Xunit;

namespace AnimationEditor.Core.Tests;

[Collection("SequentialSingletons")]
public class AddFrameUndoTests
{
    // ── AddFrame + Undo ───────────────────────────────────────────────────────

    [Fact]
    public void AddFrame_Undo_RemovesFrameFromChain()
    {
        var acls = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(acls, "Walk");

        AppCommands.Self.AddFrame(chain, "sprite.png");
        Assert.Single(chain.Frames);

        UndoManager.Self.Undo();

        Assert.Empty(chain.Frames);
    }

    [Fact]
    public void AddFrame_UndoThenRedo_ReAddsFrame()
    {
        var acls = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(acls, "Walk");

        AppCommands.Self.AddFrame(chain, "sprite.png");
        var originalFrame = chain.Frames[0];
        UndoManager.Self.Undo();
        Assert.Empty(chain.Frames);

        UndoManager.Self.Redo();

        Assert.Single(chain.Frames);
        Assert.Same(originalFrame, chain.Frames[0]);
    }
}
