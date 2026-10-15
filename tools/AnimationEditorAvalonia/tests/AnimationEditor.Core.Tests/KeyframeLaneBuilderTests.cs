using AnimationEditor.Core.Keyframes;
using AnimationEditor.Core.ViewModels;
using FlatRedBall2.AnimationEditorCommon;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class KeyframeLaneBuilderTests
{
    // Every frame a keyframe, each with its own offset and red value, so both groups are "in use".
    private static AnimationChainSave Chain(int frames, bool loop = false)
    {
        var chain = Flat(frames, loop);
        for (int i = 0; i < frames; i++)
        {
            chain.Frames[i].RelativeX = i * 10;
            chain.Frames[i].Red = i * 10;
        }
        return chain;
    }

    // Every frame a keyframe, all unset: nothing differs and nothing glides.
    private static AnimationChainSave Flat(int frames, bool loop = false)
    {
        var chain = new AnimationChainSave { Name = "c", Loop = loop };
        for (int i = 0; i < frames; i++)
            chain.Frames.Add(new AnimationFrameSave { TextureName = "t.png", FrameLength = 0.1f });
        return chain;
    }

    private static readonly double[] Widths = { 20, 30, 40, 50 };

    [Fact]
    public void Build_NullChain_OrFewerThanTwoKeyframes_ReturnsNull()
    {
        Assert.Null(KeyframeLaneBuilder.Build(null, KeyframeGroup.Offset, Widths));

        var chain = Chain(4);
        for (int i = 1; i < 4; i++) chain.Frames[i].SkipOffsetKeyframe = true;
        Assert.Null(KeyframeLaneBuilder.Build(chain, KeyframeGroup.Offset, Widths));

        Assert.Null(KeyframeLaneBuilder.Build(Chain(1), KeyframeGroup.Offset, new double[] { 20 }));
    }

    [Fact]
    public void Build_AllDefault_EveryFrameAKeyframeSameValueAllSnap_ReturnsNull()
    {
        Assert.Null(KeyframeLaneBuilder.Build(Flat(4), KeyframeGroup.Offset, Widths));
        Assert.Null(KeyframeLaneBuilder.Build(Flat(4), KeyframeGroup.Color, Widths));
    }

    [Fact]
    public void Build_ShowsWhenSomeKeyframesOffsetDiffersFromThePreviousOne()
    {
        var chain = Flat(4);
        chain.Frames[2].RelativeY = 5;

        var lane = KeyframeLaneBuilder.Build(chain, KeyframeGroup.Offset, Widths)!;

        Assert.Equal(4, lane.KeyframeXs.Count); // the keyframes that repeat the previous value still get a diamond
        Assert.Null(KeyframeLaneBuilder.Build(chain, KeyframeGroup.Color, Widths));
    }

    [Fact]
    public void Build_ShowsWhenSomeSpanGlides_EvenIfEveryValueIsTheSame()
    {
        var chain = Flat(4);
        chain.Frames[2].ColorInterpolation = "Linear";

        Assert.NotNull(KeyframeLaneBuilder.Build(chain, KeyframeGroup.Color, Widths));
        Assert.Null(KeyframeLaneBuilder.Build(chain, KeyframeGroup.Offset, Widths));
    }

    [Fact]
    public void Build_SkippedFramesOwnOffsetDoesNotCountAsADifference()
    {
        var chain = Flat(3);
        chain.Frames[1].RelativeX = 9;
        chain.Frames[1].SkipOffsetKeyframe = true;

        Assert.Null(KeyframeLaneBuilder.Build(chain, KeyframeGroup.Offset, new double[] { 10, 10, 10 }));
    }

    [Fact]
    public void Build_ColorDifferenceIsOnEffectiveValues_SoInheritedAndIdentityChannelsAreNotDifferences()
    {
        var chain = Flat(3);
        chain.Frames[0].Red = 100; // later frames inherit 100
        Assert.Null(KeyframeLaneBuilder.Build(chain, KeyframeGroup.Color, new double[] { 10, 10, 10 }));

        chain.Frames[2].Red = 50;
        Assert.NotNull(KeyframeLaneBuilder.Build(chain, KeyframeGroup.Color, new double[] { 10, 10, 10 }));

        var unsetVsIdentity = Flat(2);
        unsetVsIdentity.Frames[1].Red = 255; // Multiply's identity: the same as unset
        Assert.Null(KeyframeLaneBuilder.Build(unsetVsIdentity, KeyframeGroup.Color, new double[] { 10, 10 }));
    }

    [Fact]
    public void Build_AGlidingCurveOnASkippedFrame_DoesNotCount()
    {
        var chain = Chain(3);
        chain.Frames[1].OffsetInterpolation = "Linear";
        chain.Frames[1].SkipOffsetKeyframe = true; // skipped: its curve is ignored, but the skip itself shows the lane

        var lane = KeyframeLaneBuilder.Build(chain, KeyframeGroup.Offset, new double[] { 10, 10, 10 })!;

        Assert.True(lane.Spans.All(s => s.Curve.IsSnap));
    }

    [Fact]
    public void Build_PlacesEachKeyframeOnItsFramesLeftEdge()
    {
        var chain = Chain(4);
        chain.Frames[1].SkipOffsetKeyframe = true;

        var lane = KeyframeLaneBuilder.Build(chain, KeyframeGroup.Offset, Widths)!;

        Assert.Equal(new double[] { 0, 50, 90 }, lane.KeyframeXs);
    }

    [Fact]
    public void Build_KeyframesThatRepeatThePreviousValue_StillCount()
    {
        var chain = Chain(3);
        chain.Frames[0].RelativeX = 5;
        chain.Frames[1].RelativeX = 5;
        chain.Frames[2].RelativeX = 5;
        chain.Frames[0].OffsetInterpolation = "Linear";

        var lane = KeyframeLaneBuilder.Build(chain, KeyframeGroup.Offset, new double[] { 10, 10, 10 })!;

        Assert.Equal(3, lane.KeyframeXs.Count);
    }

    [Fact]
    public void Build_GroupsAreIndependent()
    {
        var chain = Chain(4);
        chain.Frames[1].SkipOffsetKeyframe = true;
        chain.Frames[2].SkipColorKeyframe = true;
        chain.Frames[3].SkipColorKeyframe = true;

        Assert.Equal(new double[] { 0, 50, 90 }, KeyframeLaneBuilder.Build(chain, KeyframeGroup.Offset, Widths)!.KeyframeXs);
        Assert.Equal(new double[] { 0, 20 }, KeyframeLaneBuilder.Build(chain, KeyframeGroup.Color, Widths)!.KeyframeXs);
    }

    [Fact]
    public void Build_SpanRunsFromKeyframeToNext_UsingTheStartingKeyframesCurve()
    {
        var chain = Chain(4);
        chain.Frames[2].SkipColorKeyframe = true;
        chain.Frames[0].ColorInterpolation = "Linear";
        chain.Frames[1].ColorInterpolation = "Quadratic";
        chain.Frames[1].ColorEasing = "In";
        chain.Frames[3].ColorInterpolation = "Bounce"; // last keyframe: no span of its own

        var lane = KeyframeLaneBuilder.Build(chain, KeyframeGroup.Color, Widths)!;

        Assert.Equal(2, lane.Spans.Count);
        Assert.Equal(0, lane.Spans[0].StartX);
        Assert.Equal(20, lane.Spans[0].EndX);
        Assert.Equal(KeyframeCurve.Parse("Linear", null), lane.Spans[0].Curve);
        // frame 2 is skipped, so frame 1's span covers frames 1 and 2 (20..90) and ignores frame 2's own curve
        Assert.Equal(20, lane.Spans[1].StartX);
        Assert.Equal(90, lane.Spans[1].EndX);
        Assert.Equal(KeyframeCurve.Parse("Quadratic", "In"), lane.Spans[1].Curve);
    }

    [Fact]
    public void Build_SkippedFramesOwnCurveIsIgnored()
    {
        var chain = Chain(3);
        chain.Frames[1].SkipOffsetKeyframe = true;
        chain.Frames[1].OffsetInterpolation = "Linear";

        var lane = KeyframeLaneBuilder.Build(chain, KeyframeGroup.Offset, new double[] { 10, 10, 10 })!;

        Assert.True(lane.Spans.All(s => s.Curve.IsSnap));
    }

    [Fact]
    public void Build_NonLoopingChain_HoldsFromTheLastKeyframeToTheEnd()
    {
        var chain = Chain(4);
        chain.Frames[1].SkipOffsetKeyframe = true;
        var lane = KeyframeLaneBuilder.Build(chain, KeyframeGroup.Offset, Widths)!;

        Assert.Equal(new[] { new KeyframeLaneHold(90, 140) }, lane.Holds);
        Assert.Equal(140, lane.ContentWidth);
    }

    [Fact]
    public void Build_LoopingChain_LastKeyframeGlidesBackToTheEnd_WithNoHold()
    {
        var chain = Chain(4, loop: true);
        chain.Frames[0].OffsetInterpolation = "Linear";
        chain.Frames[3].OffsetInterpolation = "Linear";

        var lane = KeyframeLaneBuilder.Build(chain, KeyframeGroup.Offset, Widths)!;

        Assert.Empty(lane.Holds);
        var last = lane.Spans[^1];
        Assert.Equal(90, last.StartX);
        Assert.Equal(140, last.EndX);
        Assert.Equal(KeyframeCurve.Parse("Linear", null), last.Curve);
    }

    [Fact]
    public void Build_TicksSitOnEveryCellsRightEdge()
    {
        var chain = Chain(4);
        chain.Frames[1].SkipOffsetKeyframe = true;
        var lane = KeyframeLaneBuilder.Build(chain, KeyframeGroup.Offset, Widths)!;

        Assert.Equal(new double[] { 20, 50, 90, 140 }, lane.TickXs);
    }

    [Fact]
    public void Build_SpanProgressMatchesTheEvaluatorAtTheSameFractionOfTheSpan()
    {
        // The drawn curve must be the evaluator's: sample a 2-frame span and compare to EvaluateOffset.
        var chain = Chain(3);
        chain.Frames[0].RelativeX = 0;
        chain.Frames[2].RelativeX = 100;
        chain.Frames[1].SkipOffsetKeyframe = true;
        chain.Frames[0].OffsetInterpolation = "Bounce"; // stays within 0..1; the evaluator clamps Back's overshoot
        chain.Frames[0].OffsetEasing = "Out";

        var lane = KeyframeLaneBuilder.Build(chain, KeyframeGroup.Offset, new double[] { 10, 10, 10 })!;
        var points = KeyframeLaneBuilder.CurvePoints(lane.Spans[0].Curve, 20);

        foreach (var (t, progress) in points)
        {
            double elapsed = t * 0.2; // span is frames 0..1 = 0.2s
            int frame = elapsed >= 0.1 ? 1 : 0;
            var (x, _) = KeyframeEvaluator.EvaluateOffset(chain, frame, elapsed - frame * 0.1);
            Assert.Equal(x / 100f, progress, 3);
        }
    }

    [Fact]
    public void CurvePoints_Snap_IsFlatThenAVerticalJumpAtTheEnd()
    {
        var points = KeyframeLaneBuilder.CurvePoints(KeyframeCurve.Snap, 16);

        Assert.Equal(new (double, double)[] { (0, 0), (1, 0), (1, 1) }, points);
    }

    [Fact]
    public void CurvePoints_Linear_RunsCornerToCorner()
    {
        var points = KeyframeLaneBuilder.CurvePoints(KeyframeCurve.Parse("Linear", null), 4);

        Assert.Equal((0.0, 0.0), points[0]);
        Assert.Equal((1.0, 1.0), points[^1]);
        Assert.Equal(0.5, points[2].Progress, 5);
    }
}

public class KeyframeLaneClipTests
{
    [Fact]
    public void ClipLeft_AtScrollZero_KeepsTheFirstDiamondsSpill()
        => Assert.Equal(-KeyframeLaneBuilder.DiamondHalfWidth, KeyframeLaneBuilder.ClipLeft(0));

    [Fact]
    public void ClipLeft_OnceScrolled_ClipsAtTheViewportLikeTheCells()
        => Assert.Equal(0, KeyframeLaneBuilder.ClipLeft(3));
}
