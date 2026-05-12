using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.Core.Tests;

[Collection("SequentialSingletons")]
public class AppCommandsReorderTests
{
    // ── HandleReorder — chain selected ───────────────────────────────────────

    [Fact]
    public void HandleReorder_ChainSelected_DeltaPos1_MovesChainDown()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var walk = TestHelpers.MakeChain(acls, "Walk");
        var run  = TestHelpers.MakeChain(acls, "Run");
        ctx.SelectedState.SelectedChain = walk;

        ctx.AppCommands.HandleReorder(+1);

        Assert.Equal(run,  acls.AnimationChains[0]);
        Assert.Equal(walk, acls.AnimationChains[1]);
    }

    [Fact]
    public void HandleReorder_ChainSelected_DeltaNeg1_MovesChainUp()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var walk = TestHelpers.MakeChain(acls, "Walk");
        var run  = TestHelpers.MakeChain(acls, "Run");
        ctx.SelectedState.SelectedChain = run;

        ctx.AppCommands.HandleReorder(-1);

        Assert.Equal(run,  acls.AnimationChains[0]);
        Assert.Equal(walk, acls.AnimationChains[1]);
    }

    [Fact]
    public void HandleReorder_ChainSelected_AtTop_DeltaNeg1_IsNoOp()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var walk = TestHelpers.MakeChain(acls, "Walk");
        TestHelpers.MakeChain(acls, "Run");
        ctx.SelectedState.SelectedChain = walk;

        ctx.AppCommands.HandleReorder(-1);

        Assert.Equal(walk, acls.AnimationChains[0]);
    }

    // ── HandleReorder — frame selected ───────────────────────────────────────

    [Fact]
    public void HandleReorder_FrameSelected_DeltaPos1_MovesFrameDown()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk", 3);
        var frameA = chain.Frames[0];
        var frameB = chain.Frames[1];
        ctx.SelectedState.SelectedFrame = frameA;

        ctx.AppCommands.HandleReorder(+1);

        Assert.Equal(frameB, chain.Frames[0]);
        Assert.Equal(frameA, chain.Frames[1]);
    }

    [Fact]
    public void HandleReorder_FrameSelected_DeltaNeg1_MovesFrameUp()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk", 3);
        var frameA = chain.Frames[0];
        var frameB = chain.Frames[1];
        ctx.SelectedState.SelectedFrame = frameB;

        ctx.AppCommands.HandleReorder(-1);

        Assert.Equal(frameB, chain.Frames[0]);
        Assert.Equal(frameA, chain.Frames[1]);
    }

    [Fact]
    public void HandleReorder_NothingSelected_DoesNotThrow()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        // SelectedChain and SelectedFrame are both null after SetupFreshAcls

        var ex = Record.Exception(() => ctx.AppCommands.HandleReorder(+1));

        Assert.Null(ex);
    }
}
