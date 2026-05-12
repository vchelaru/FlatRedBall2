using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using FlatRedBall.Content.AnimationChain;
using Xunit;

namespace AnimationEditor.Core.Tests;

[Collection("SequentialSingletons")]
public class AppCommandsReorderTests
{
    // ── HandleReorder — chain selected ───────────────────────────────────────

    [Fact]
    public void HandleReorder_ChainSelected_DeltaPos1_MovesChainDown()
    {
        var acls = TestHelpers.SetupFreshAcls();
        var walk = TestHelpers.MakeChain(acls, "Walk");
        var run  = TestHelpers.MakeChain(acls, "Run");
        SelectedState.Self.SelectedChain = walk;

        AppCommands.Self.HandleReorder(+1);

        Assert.Equal(run,  acls.AnimationChains[0]);
        Assert.Equal(walk, acls.AnimationChains[1]);
    }

    [Fact]
    public void HandleReorder_ChainSelected_DeltaNeg1_MovesChainUp()
    {
        var acls = TestHelpers.SetupFreshAcls();
        var walk = TestHelpers.MakeChain(acls, "Walk");
        var run  = TestHelpers.MakeChain(acls, "Run");
        SelectedState.Self.SelectedChain = run;

        AppCommands.Self.HandleReorder(-1);

        Assert.Equal(run,  acls.AnimationChains[0]);
        Assert.Equal(walk, acls.AnimationChains[1]);
    }

    [Fact]
    public void HandleReorder_ChainSelected_AtTop_DeltaNeg1_IsNoOp()
    {
        var acls = TestHelpers.SetupFreshAcls();
        var walk = TestHelpers.MakeChain(acls, "Walk");
        TestHelpers.MakeChain(acls, "Run");
        SelectedState.Self.SelectedChain = walk;

        AppCommands.Self.HandleReorder(-1);

        Assert.Equal(walk, acls.AnimationChains[0]);
    }

    // ── HandleReorder — frame selected ───────────────────────────────────────

    [Fact]
    public void HandleReorder_FrameSelected_DeltaPos1_MovesFrameDown()
    {
        var acls  = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(acls, "Walk", 3);
        var frameA = chain.Frames[0];
        var frameB = chain.Frames[1];
        SelectedState.Self.SelectedFrame = frameA;

        AppCommands.Self.HandleReorder(+1);

        Assert.Equal(frameB, chain.Frames[0]);
        Assert.Equal(frameA, chain.Frames[1]);
    }

    [Fact]
    public void HandleReorder_FrameSelected_DeltaNeg1_MovesFrameUp()
    {
        var acls  = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(acls, "Walk", 3);
        var frameA = chain.Frames[0];
        var frameB = chain.Frames[1];
        SelectedState.Self.SelectedFrame = frameB;

        AppCommands.Self.HandleReorder(-1);

        Assert.Equal(frameB, chain.Frames[0]);
        Assert.Equal(frameA, chain.Frames[1]);
    }

    [Fact]
    public void HandleReorder_NothingSelected_DoesNotThrow()
    {
        TestHelpers.SetupFreshAcls();
        // SelectedChain and SelectedFrame are both null after SetupFreshAcls

        var ex = Record.Exception(() => AppCommands.Self.HandleReorder(+1));

        Assert.Null(ex);
    }
}
