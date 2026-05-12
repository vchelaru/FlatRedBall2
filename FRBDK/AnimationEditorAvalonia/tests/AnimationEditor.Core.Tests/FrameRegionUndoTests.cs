using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.Core.Tests;

[Collection("SequentialSingletons")]
public class FrameRegionUndoTests
{
    // ── FrameRegionChangedCommand ─────────────────────────────────────────────

    [Fact]
    public void FrameRegionChangedCommand_Redo_RestoresAfterUvCoordinates()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Anim");
        var frame = TestHelpers.MakeFrame();
        chain.Frames.Add(frame);

        // before UV
        float bL = 0f, bT = 0f, bR = 1f, bB = 1f;
        // after UV
        float aL = 0.1f, aT = 0.2f, aR = 0.8f, aB = 0.9f;

        frame.LeftCoordinate   = aL;
        frame.TopCoordinate    = aT;
        frame.RightCoordinate  = aR;
        frame.BottomCoordinate = aB;

        var cmd = new FrameRegionChangedCommand(frame, bL, bT, bR, bB, aL, aT, aR, aB,
            ctx.AppCommands, ctx.ApplicationEvents);
        ctx.UndoManager.Record(cmd);

        ctx.UndoManager.Undo();
        Assert.Equal(bL, frame.LeftCoordinate,   precision: 5);
        Assert.Equal(bT, frame.TopCoordinate,    precision: 5);
        Assert.Equal(bR, frame.RightCoordinate,  precision: 5);
        Assert.Equal(bB, frame.BottomCoordinate, precision: 5);

        ctx.UndoManager.Redo();
        Assert.Equal(aL, frame.LeftCoordinate,   precision: 5);
        Assert.Equal(aT, frame.TopCoordinate,    precision: 5);
        Assert.Equal(aR, frame.RightCoordinate,  precision: 5);
        Assert.Equal(aB, frame.BottomCoordinate, precision: 5);
    }

    [Fact]
    public void FrameRegionChangedCommand_Undo_RestoresBeforeUvCoordinates()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Anim");
        var frame = TestHelpers.MakeFrame();
        chain.Frames.Add(frame);

        float bL = 0f, bT = 0f, bR = 0.5f, bB = 0.5f;
        float aL = 0.25f, aT = 0.25f, aR = 0.75f, aB = 0.75f;

        var cmd = new FrameRegionChangedCommand(frame, bL, bT, bR, bB, aL, aT, aR, aB,
            ctx.AppCommands, ctx.ApplicationEvents);

        // Simulate: command was recorded after UV was updated to "after" values
        frame.LeftCoordinate   = aL;
        frame.TopCoordinate    = aT;
        frame.RightCoordinate  = aR;
        frame.BottomCoordinate = aB;

        ctx.UndoManager.Record(cmd);
        ctx.UndoManager.Undo();

        Assert.Equal(bL, frame.LeftCoordinate,   precision: 5);
        Assert.Equal(bT, frame.TopCoordinate,    precision: 5);
        Assert.Equal(bR, frame.RightCoordinate,  precision: 5);
        Assert.Equal(bB, frame.BottomCoordinate, precision: 5);
    }
}
