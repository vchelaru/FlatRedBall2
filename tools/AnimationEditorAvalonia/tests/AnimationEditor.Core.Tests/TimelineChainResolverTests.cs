using AnimationEditor.Core.CommandsAndState;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class TimelineChainResolverTests
{
    [Fact]
    public void GetChain_NoSelectedChain_FallsBackToChainContainingSelectedFrame()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 2);
        ctx.SelectedState.SelectedChain = null;
        ctx.SelectedState.SelectedFrame = chain.Frames[0];

        var result = TimelineChainResolver.GetChain(ctx.SelectedState, ctx.ObjectFinder);

        Assert.Same(chain, result);
    }

    [Fact]
    public void GetChain_NothingSelected_ReturnsNull()
    {
        var ctx = TestHelpers.SetupFreshAcls();

        var result = TimelineChainResolver.GetChain(ctx.SelectedState, ctx.ObjectFinder);

        Assert.Null(result);
    }

    [Fact]
    public void GetChain_SelectedChainSet_ReturnsIt()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 2);
        ctx.SelectedState.SelectedChain = chain;

        var result = TimelineChainResolver.GetChain(ctx.SelectedState, ctx.ObjectFinder);

        Assert.Same(chain, result);
    }

    [Fact]
    public void GetPreferredFrameIndex_EmptyChain_ReturnsNegativeOne()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Empty", 0);

        var result = TimelineChainResolver.GetPreferredFrameIndex(ctx.SelectedState, ctx.ObjectFinder, chain, playbackFrameIndex: 3);

        Assert.Equal(-1, result);
    }

    [Fact]
    public void GetPreferredFrameIndex_NoSelectedFrame_FallsBackToPlaybackIndex()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 3);

        var result = TimelineChainResolver.GetPreferredFrameIndex(ctx.SelectedState, ctx.ObjectFinder, chain, playbackFrameIndex: 1);

        Assert.Equal(1, result);
    }

    [Fact]
    public void GetPreferredFrameIndex_NullChain_ReturnsNegativeOne()
    {
        var ctx = TestHelpers.SetupFreshAcls();

        var result = TimelineChainResolver.GetPreferredFrameIndex(ctx.SelectedState, ctx.ObjectFinder, null, playbackFrameIndex: 3);

        Assert.Equal(-1, result);
    }

    [Fact]
    public void GetPreferredFrameIndex_SelectedFrameBelongsToChain_ReturnsItsIndex()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 3);
        ctx.SelectedState.SelectedFrame = chain.Frames[2];

        var result = TimelineChainResolver.GetPreferredFrameIndex(ctx.SelectedState, ctx.ObjectFinder, chain, playbackFrameIndex: 0);

        Assert.Equal(2, result);
    }

    [Fact]
    public void GetPreferredFrameIndex_SelectedFrameBelongsToDifferentChain_FallsBackToPlaybackIndex()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 3);
        var otherChain = TestHelpers.MakeChain(ctx.Acls, "Jump", 2);
        ctx.SelectedState.SelectedFrame = otherChain.Frames[0];

        var result = TimelineChainResolver.GetPreferredFrameIndex(ctx.SelectedState, ctx.ObjectFinder, chain, playbackFrameIndex: 1);

        Assert.Equal(1, result);
    }
}
