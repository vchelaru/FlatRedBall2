using AnimationEditor.Core.Tiled;
using FlatRedBall2.AnimationEditorCommon;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Tiled;

public class FrameFootprintSyncTests
{
    private static AnimationChainSave TwoFrameChain(
        float f0L, float f0T, float f0R, float f0B,
        float f1L, float f1T, float f1R, float f1B)
    {
        var chain = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(new AnimationFrameSave { LeftCoordinate = f0L, TopCoordinate = f0T, RightCoordinate = f0R, BottomCoordinate = f0B });
        chain.Frames.Add(new AnimationFrameSave { LeftCoordinate = f1L, TopCoordinate = f1T, RightCoordinate = f1R, BottomCoordinate = f1B });
        return chain;
    }

    [Fact]
    public void ComputeSiblingMatches_MatchesNewSize_ButKeepsSiblingsOwnPosition()
    {
        // Frame 0 at (0,0)-(0.25,0.25); frame 1 at (0.25,0)-(0.5,0.25) -- both one 16px tile
        // in a 64px texture. Frame 0 was just stretched to two tiles wide (0,0)-(0.5,0.25).
        var chain = TwoFrameChain(0f, 0f, 0.5f, 0.25f, 0.25f, 0f, 0.5f, 0.25f);
        var frame0 = chain.Frames[0];
        var frame1 = chain.Frames[1];

        var matches = FrameFootprintSync.ComputeSiblingMatches(chain, frame0, newWidth: 0.5f, newHeight: 0.25f);

        var match = Assert.Single(matches);
        Assert.Same(frame1, match.Frame);
        Assert.Equal(0.25f, match.Before.Left);
        Assert.Equal(0.5f,  match.Before.Right);
        // Own Left/Top preserved -- only Right/Bottom move to match the new size.
        Assert.Equal(0.25f, match.After.Left);
        Assert.Equal(0f,    match.After.Top);
        Assert.Equal(0.75f, match.After.Right);
        Assert.Equal(0.25f, match.After.Bottom);
    }

    [Fact]
    public void ComputeSiblingMatches_ExcludesTheResizedFrameItself()
    {
        var chain = TwoFrameChain(0f, 0f, 0.5f, 0.25f, 0.25f, 0f, 0.5f, 0.25f);
        var frame0 = chain.Frames[0];

        var matches = FrameFootprintSync.ComputeSiblingMatches(chain, frame0, newWidth: 0.5f, newHeight: 0.25f);

        Assert.DoesNotContain(matches, m => ReferenceEquals(m.Frame, frame0));
    }

    [Fact]
    public void ComputeSiblingMatches_SingleFrameChain_ReturnsEmpty()
    {
        var chain = new AnimationChainSave { Name = "Idle" };
        var frame0 = new AnimationFrameSave { LeftCoordinate = 0f, TopCoordinate = 0f, RightCoordinate = 0.25f, BottomCoordinate = 0.25f };
        chain.Frames.Add(frame0);

        var matches = FrameFootprintSync.ComputeSiblingMatches(chain, frame0, newWidth: 0.5f, newHeight: 0.25f);

        Assert.Empty(matches);
    }
}
