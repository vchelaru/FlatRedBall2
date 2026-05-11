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
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk");

        ctx.AppCommands.AddFrame(chain, "sprite.png");
        Assert.Single(chain.Frames);

        ctx.UndoManager.Undo();

        Assert.Empty(chain.Frames);
    }

    [Fact]
    public void AddFrame_UndoThenRedo_ReAddsFrame()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk");

        ctx.AppCommands.AddFrame(chain, "sprite.png");
        var originalFrame = chain.Frames[0];
        ctx.UndoManager.Undo();
        Assert.Empty(chain.Frames);

        ctx.UndoManager.Redo();

        Assert.Single(chain.Frames);
        Assert.Same(originalFrame, chain.Frames[0]);
    }
}
