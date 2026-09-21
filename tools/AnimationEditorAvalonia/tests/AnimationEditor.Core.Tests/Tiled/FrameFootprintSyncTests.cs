using AnimationEditor.Core.Tiled;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationEditor.Core.Tests.Tiled;

using FrameRect = FrameFootprintSync.FrameRect;

public class FrameFootprintSyncTests
{
    private static AnimationChainSave TwoFrameChain(FrameRect f0, FrameRect f1)
    {
        var chain = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(new AnimationFrameSave { LeftCoordinate = f0.Left, TopCoordinate = f0.Top, RightCoordinate = f0.Right, BottomCoordinate = f0.Bottom });
        chain.Frames.Add(new AnimationFrameSave { LeftCoordinate = f1.Left, TopCoordinate = f1.Top, RightCoordinate = f1.Right, BottomCoordinate = f1.Bottom });
        return chain;
    }

    // Every test uses one 16px tile in a 64px texture, so one tile == 0.25 UV. Frame 0 is the
    // resized frame; frame 1 is a sibling one tile away in the direction being tested. Each
    // asserts the sibling's edge(s) moved by the exact same delta as frame 0's, in whichever
    // direction (grow = outward, shrink = inward) and on whichever edge (right/left/down/up).

    [Fact]
    public void ComputeSiblingMatches_GrowingRight_GrowsSiblingRightToo()
    {
        var chain = TwoFrameChain(new FrameRect(0f, 0f, 0.5f, 0.25f), new FrameRect(0.25f, 0f, 0.5f, 0.25f));
        var frame0 = chain.Frames[0];
        var frame1 = chain.Frames[1];

        var matches = FrameFootprintSync.ComputeSiblingMatches(chain, frame0,
            new FrameRect(0f, 0f, 0.25f, 0.25f), new FrameRect(0f, 0f, 0.5f, 0.25f));

        var match = Assert.Single(matches);
        Assert.Same(frame1, match.Frame);
        Assert.Equal(new FrameRect(0.25f, 0f, 0.75f, 0.25f), match.After);
    }

    [Fact]
    public void ComputeSiblingMatches_ShrinkingRight_ShrinksSiblingRightToo()
    {
        var chain = TwoFrameChain(new FrameRect(0f, 0f, 0.25f, 0.25f), new FrameRect(0.5f, 0f, 0.75f, 0.25f));
        var frame0 = chain.Frames[0];
        var frame1 = chain.Frames[1];

        // Frame 0's right edge pulled inward by one tile: (0,0,0.5,0.25) -> (0,0,0.25,0.25).
        var matches = FrameFootprintSync.ComputeSiblingMatches(chain, frame0,
            new FrameRect(0f, 0f, 0.5f, 0.25f), new FrameRect(0f, 0f, 0.25f, 0.25f));

        var match = Assert.Single(matches);
        Assert.Same(frame1, match.Frame);
        Assert.Equal(new FrameRect(0.5f, 0f, 0.5f, 0.25f), match.After);
    }

    [Fact]
    public void ComputeSiblingMatches_GrowingLeft_GrowsSiblingLeftToo()
    {
        var chain = TwoFrameChain(new FrameRect(0f, 0f, 0.5f, 0.25f), new FrameRect(0.5f, 0f, 0.75f, 0.25f));
        var frame0 = chain.Frames[0];
        var frame1 = chain.Frames[1];

        // Frame 0's left edge dragged out one tile: (0.25,0,0.5,0.25) -> (0,0,0.5,0.25).
        var matches = FrameFootprintSync.ComputeSiblingMatches(chain, frame0,
            new FrameRect(0.25f, 0f, 0.5f, 0.25f), new FrameRect(0f, 0f, 0.5f, 0.25f));

        var match = Assert.Single(matches);
        Assert.Same(frame1, match.Frame);
        // Grew leftward by one tile, same as frame 0 -- right edge unchanged, left edge moved.
        Assert.Equal(new FrameRect(0.25f, 0f, 0.75f, 0.25f), match.After);
    }

    [Fact]
    public void ComputeSiblingMatches_ShrinkingLeft_ShrinksSiblingLeftToo()
    {
        var chain = TwoFrameChain(new FrameRect(0.25f, 0f, 0.5f, 0.25f), new FrameRect(0.5f, 0f, 0.75f, 0.25f));
        var frame0 = chain.Frames[0];
        var frame1 = chain.Frames[1];

        // Frame 0's left edge pulled inward one tile: (0,0,0.5,0.25) -> (0.25,0,0.5,0.25).
        var matches = FrameFootprintSync.ComputeSiblingMatches(chain, frame0,
            new FrameRect(0f, 0f, 0.5f, 0.25f), new FrameRect(0.25f, 0f, 0.5f, 0.25f));

        var match = Assert.Single(matches);
        Assert.Same(frame1, match.Frame);
        // Shrank leftward-edge inward by one tile -- right edge unchanged, left edge moved right.
        Assert.Equal(new FrameRect(0.75f, 0f, 0.75f, 0.25f), match.After);
    }

    [Fact]
    public void ComputeSiblingMatches_GrowingDown_GrowsSiblingDownToo()
    {
        var chain = TwoFrameChain(new FrameRect(0f, 0f, 0.25f, 0.5f), new FrameRect(0f, 0.25f, 0.25f, 0.5f));
        var frame0 = chain.Frames[0];
        var frame1 = chain.Frames[1];

        // Frame 0's bottom edge dragged out one tile: (0,0,0.25,0.25) -> (0,0,0.25,0.5).
        var matches = FrameFootprintSync.ComputeSiblingMatches(chain, frame0,
            new FrameRect(0f, 0f, 0.25f, 0.25f), new FrameRect(0f, 0f, 0.25f, 0.5f));

        var match = Assert.Single(matches);
        Assert.Same(frame1, match.Frame);
        Assert.Equal(new FrameRect(0f, 0.25f, 0.25f, 0.75f), match.After);
    }

    [Fact]
    public void ComputeSiblingMatches_ShrinkingDown_ShrinksSiblingDownToo()
    {
        var chain = TwoFrameChain(new FrameRect(0f, 0f, 0.25f, 0.25f), new FrameRect(0f, 0.5f, 0.25f, 0.75f));
        var frame0 = chain.Frames[0];
        var frame1 = chain.Frames[1];

        // Frame 0's bottom edge pulled inward one tile: (0,0,0.25,0.5) -> (0,0,0.25,0.25).
        var matches = FrameFootprintSync.ComputeSiblingMatches(chain, frame0,
            new FrameRect(0f, 0f, 0.25f, 0.5f), new FrameRect(0f, 0f, 0.25f, 0.25f));

        var match = Assert.Single(matches);
        Assert.Same(frame1, match.Frame);
        Assert.Equal(new FrameRect(0f, 0.5f, 0.25f, 0.5f), match.After);
    }

    [Fact]
    public void ComputeSiblingMatches_GrowingUp_GrowsSiblingUpToo()
    {
        var chain = TwoFrameChain(new FrameRect(0f, 0.25f, 0.25f, 0.5f), new FrameRect(0f, 0.5f, 0.25f, 0.75f));
        var frame0 = chain.Frames[0];
        var frame1 = chain.Frames[1];

        // Frame 0's top edge dragged out one tile: (0,0.25,0.25,0.5) -> (0,0,0.25,0.5).
        var matches = FrameFootprintSync.ComputeSiblingMatches(chain, frame0,
            new FrameRect(0f, 0.25f, 0.25f, 0.5f), new FrameRect(0f, 0f, 0.25f, 0.5f));

        var match = Assert.Single(matches);
        Assert.Same(frame1, match.Frame);
        // Grew upward by one tile, same as frame 0 -- bottom edge unchanged, top edge moved.
        Assert.Equal(new FrameRect(0f, 0.25f, 0.25f, 0.75f), match.After);
    }

    [Fact]
    public void ComputeSiblingMatches_ShrinkingUp_ShrinksSiblingUpToo()
    {
        var chain = TwoFrameChain(new FrameRect(0f, 0f, 0.25f, 0.5f), new FrameRect(0f, 0.5f, 0.25f, 0.75f));
        var frame0 = chain.Frames[0];
        var frame1 = chain.Frames[1];

        // Frame 0's top edge pulled inward one tile: (0,0,0.25,0.5) -> (0,0.25,0.25,0.5).
        var matches = FrameFootprintSync.ComputeSiblingMatches(chain, frame0,
            new FrameRect(0f, 0f, 0.25f, 0.5f), new FrameRect(0f, 0.25f, 0.25f, 0.5f));

        var match = Assert.Single(matches);
        Assert.Same(frame1, match.Frame);
        // Shrank the top edge inward by one tile -- bottom edge unchanged, top edge moved down.
        Assert.Equal(new FrameRect(0f, 0.75f, 0.25f, 0.75f), match.After);
    }

    [Fact]
    public void ComputeSiblingMatches_ExcludesTheResizedFrameItself()
    {
        var chain = TwoFrameChain(new FrameRect(0f, 0f, 0.5f, 0.25f), new FrameRect(0.25f, 0f, 0.5f, 0.25f));
        var frame0 = chain.Frames[0];

        var matches = FrameFootprintSync.ComputeSiblingMatches(chain, frame0,
            new FrameRect(0f, 0f, 0.25f, 0.25f), new FrameRect(0f, 0f, 0.5f, 0.25f));

        Assert.DoesNotContain(matches, m => ReferenceEquals(m.Frame, frame0));
    }

    [Fact]
    public void ComputeSiblingMatches_SingleFrameChain_ReturnsEmpty()
    {
        var chain = new AnimationChainSave { Name = "Idle" };
        var frame0 = new AnimationFrameSave { LeftCoordinate = 0f, TopCoordinate = 0f, RightCoordinate = 0.25f, BottomCoordinate = 0.25f };
        chain.Frames.Add(frame0);

        var matches = FrameFootprintSync.ComputeSiblingMatches(chain, frame0,
            new FrameRect(0f, 0f, 0.25f, 0.25f), new FrameRect(0f, 0f, 0.5f, 0.25f));

        Assert.Empty(matches);
    }
}
