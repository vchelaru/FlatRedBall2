using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.Core.Tests;

[Collection("SequentialSingletons")]
public class InvertFrameOrderUndoTests
{
    [Fact]
    public void InvertFrameOrder_Undo_RestoresOriginalOrder()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 3);
        var frame0 = chain.Frames[0];
        var frame1 = chain.Frames[1];
        var frame2 = chain.Frames[2];

        ctx.AppCommands.InvertFrameOrder(chain);
        Assert.Equal(new[] { frame2, frame1, frame0 }, chain.Frames);

        ctx.UndoManager.Undo();

        Assert.Equal(new[] { frame0, frame1, frame2 }, chain.Frames);
    }

    [Fact]
    public void InvertFrameOrder_UndoThenRedo_RestoresInvertedOrder()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 3);
        var frame0 = chain.Frames[0];
        var frame1 = chain.Frames[1];
        var frame2 = chain.Frames[2];

        ctx.AppCommands.InvertFrameOrder(chain);
        ctx.UndoManager.Undo();

        ctx.UndoManager.Redo();

        Assert.Equal(new[] { frame2, frame1, frame0 }, chain.Frames);
    }
}
