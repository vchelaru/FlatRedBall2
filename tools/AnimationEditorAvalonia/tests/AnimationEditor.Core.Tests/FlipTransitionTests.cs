using System.Collections.Generic;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.Rendering;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class FlipTransitionTests
{
    private static readonly FlipPose Unflipped = new(false, false, false, 0f, 0f);

    [Fact]
    public void Matrix_AtStart_IsTheOldPose()
    {
        var from = new FlipPose(true, false, false, 0f, 0f);
        var to = new FlipPose(true, true, false, 0f, 0f);

        var m = FlipTransition.Matrix(from, to, 0f);

        Assert.Equal((-1f, 0f, 0f, 1f), m);
    }

    [Fact]
    public void Matrix_AtEnd_IsTheNewPose()
    {
        var to = new FlipPose(false, false, true, 0f, 0f);

        var m = FlipTransition.Matrix(Unflipped, to, 1f);

        Assert.Equal((0f, 1f, 1f, 0f), m);
    }

    [Fact]
    public void Matrix_HorizontalFlipHalfway_SquashesWidthToZeroAndKeepsHeight()
    {
        var to = new FlipPose(true, false, false, 0f, 0f);

        var m = FlipTransition.Matrix(Unflipped, to, 0.5f);

        Assert.Equal(0f, m.a, 4);
        Assert.Equal(1f, m.d, 4);
    }

    [Fact]
    public void Matrix_DiagonalFlipHalfway_FoldsOntoTheDiagonal()
    {
        var to = new FlipPose(false, false, true, 0f, 0f);

        var m = FlipTransition.Matrix(Unflipped, to, 0.5f);

        // Halfway between identity and the (x,y) -> (y,x) swap every coefficient is 0.5: the
        // sprite is a flat line along the main diagonal, which is the axis it flips across.
        Assert.Equal((0.5f, 0.5f, 0.5f, 0.5f), m);
    }

    [Fact]
    public void Matrix_UsesEasingSoTheMotionStartsAndEndsGently()
    {
        var to = new FlipPose(true, false, false, 0f, 0f);

        var early = FlipTransition.Matrix(Unflipped, to, 0.1f);

        // Linear would give a = 1 - 2*0.1 = 0.8. Ease-in-out lags behind that at the start.
        Assert.True(early.a > 0.9f);
    }

    [Fact]
    public void Ease_HitsEndpointsAndMidpoint()
    {
        Assert.Equal(0f, FlipTransition.Ease(0f));
        Assert.Equal(1f, FlipTransition.Ease(1f));
        Assert.Equal(0.5f, FlipTransition.Ease(0.5f), 4);
    }

    [Fact]
    public void OffsetDelta_AtStart_PutsTheSpriteAtItsOldOffset()
    {
        var from = new FlipPose(false, false, false, 10f, 4f);
        var to = new FlipPose(true, false, false, -10f, 4f);

        var delta = FlipTransition.OffsetDelta(from, to, 0f);

        // The frame already holds the new offset (-10, 4); adding (20, 0) lands on the old (10, 4).
        Assert.Equal((20f, 0f), delta);
    }

    [Fact]
    public void OffsetDelta_AtEnd_IsZero()
    {
        var from = new FlipPose(false, false, false, 10f, 4f);
        var to = new FlipPose(true, false, false, -10f, 4f);

        var delta = FlipTransition.OffsetDelta(from, to, 1f);

        Assert.Equal((0f, 0f), delta);
    }

    [Fact]
    public void SetFrameFlip_HorizontalOnFrameWithOffset_RaisesFramesFlippedWithBeforeAndAfter()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var frame = new AnimationFrameSave { RelativeX = 10, RelativeY = 4 };
        IReadOnlyList<FrameFlipChange>? raised = null;
        ctx.ApplicationEvents.FramesFlipped += changes => raised = changes;

        ctx.AppCommands.SetFrameFlip(new[] { frame }, flipHorizontal: true, flipVertical: null);

        Assert.NotNull(raised);
        var change = Assert.Single(raised!);
        Assert.Same(frame, change.Frame);
        Assert.Equal(new FlipPose(false, false, false, 10f, 4f), change.Before);
        Assert.Equal(new FlipPose(true, false, false, -10f, 4f), change.After);
    }

    [Fact]
    public void Undo_AfterFlip_RaisesFramesFlippedBackToTheOldPose()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var frame = new AnimationFrameSave { RelativeX = 10, RelativeY = 4 };
        ctx.AppCommands.SetFrameFlip(new[] { frame }, flipHorizontal: true, flipVertical: null);
        IReadOnlyList<FrameFlipChange>? raised = null;
        ctx.ApplicationEvents.FramesFlipped += changes => raised = changes;

        ctx.UndoManager.Undo();

        var change = Assert.Single(raised!);
        Assert.Equal(new FlipPose(true, false, false, -10f, 4f), change.Before);
        Assert.Equal(new FlipPose(false, false, false, 10f, 4f), change.After);
    }
}
