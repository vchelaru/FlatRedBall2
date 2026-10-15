using AnimationEditor.Core.Keyframes;
using AnimationEditor.Core.ViewModels;
using FlatRedBall2.AnimationEditorCommon;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>The Offset and Size keyframe lanes of a selected shape (#1489).</summary>
public class ShapeKeyframeLaneBuilderTests
{
    private static readonly double[] Widths = { 20, 30, 40, 50 };

    // frames 0..n-1, each holding one rectangle named "A" whose X is i*10 (so the Offset lane is in use).
    private static (AnimationChainSave Chain, AARectSave[] Rects) RectChain(int frames, bool loop = false)
    {
        var chain = new AnimationChainSave { Name = "c", Loop = loop };
        var rects = new AARectSave[frames];
        for (int i = 0; i < frames; i++)
        {
            var frame = new AnimationFrameSave { TextureName = "t.png", FrameLength = 0.1f, ShapesSave = new ShapesSave() };
            rects[i] = new AARectSave { Name = "A", X = i * 10, ScaleX = 8, ScaleY = 8 + i };
            frame.ShapesSave.Add(rects[i]);
            chain.Frames.Add(frame);
        }
        return (chain, rects);
    }

    private static KeyframeLane? Offset(AnimationChainSave chain, ShapeSave shape, double[]? widths = null) =>
        KeyframeLaneBuilder.BuildShape(chain, shape, ShapeKeyframeGroup.Offset, widths ?? Widths);

    private static KeyframeLane? Size(AnimationChainSave chain, ShapeSave shape, double[]? widths = null) =>
        KeyframeLaneBuilder.BuildShape(chain, shape, ShapeKeyframeGroup.Size, widths ?? Widths);

    private static void RemoveFrom(AnimationChainSave chain, int frame, ShapeSave shape) =>
        chain.Frames[frame].ShapesSave!.Remove(shape);

    // ---- When a lane shows ---------------------------------------------------------------------

    [Fact]
    public void Build_NullArguments_ReturnNull()
    {
        var (chain, rects) = RectChain(4);
        Assert.Null(KeyframeLaneBuilder.BuildShape(null, rects[0], ShapeKeyframeGroup.Offset, Widths));
        Assert.Null(KeyframeLaneBuilder.BuildShape(chain, null, ShapeKeyframeGroup.Offset, Widths));
        Assert.Null(Offset(chain, rects[0], new double[] { 20 })); // fewer widths than frames
    }

    [Fact]
    public void Build_NothingDiffersAndNothingGlides_ReturnsNull()
    {
        var (chain, rects) = RectChain(4);
        foreach (var r in rects) r.X = 5;

        Assert.Null(Offset(chain, rects[0]));
    }

    [Fact]
    public void Build_ValueDiffers_ShowsWithADiamondOnEveryKeyframe_EvenOnesRepeatingThePreviousValue()
    {
        var (chain, rects) = RectChain(4);
        rects[1].X = 0; // repeats frame 0

        var lane = Offset(chain, rects[0])!;

        Assert.Equal(new double[] { 0, 20, 50, 90 }, lane.KeyframeXs);
    }

    [Fact]
    public void Build_AGlidingSpanShowsTheLane_EvenIfEveryValueIsTheSame()
    {
        var (chain, rects) = RectChain(4);
        foreach (var r in rects) r.X = 5;
        rects[2].OffsetInterpolation = "Linear";

        Assert.NotNull(Offset(chain, rects[0]));
    }

    [Fact]
    public void Build_GroupsAreIndependent_OffsetInUseDoesNotMakeSizeInUse()
    {
        var (chain, rects) = RectChain(4);
        foreach (var r in rects) r.ScaleY = 8; // sizes all equal, offsets differ

        Assert.NotNull(Offset(chain, rects[0]));
        Assert.Null(Size(chain, rects[0]));

        rects[3].ScaleX = 20;
        Assert.NotNull(Size(chain, rects[0]));
    }

    [Fact]
    public void Build_SizeLane_ForACircle_ComparesRadius()
    {
        var (chain, _) = RectChain(3);
        var circles = new CircleSave[3];
        for (int i = 0; i < 3; i++)
        {
            chain.Frames[i].ShapesSave = new ShapesSave();
            circles[i] = new CircleSave { Name = "C", Radius = 4 };
            chain.Frames[i].ShapesSave!.Add(circles[i]);
        }
        var w = new double[] { 10, 10, 10 };
        Assert.Null(Size(chain, circles[0], w));

        circles[2].Radius = 9;
        Assert.Equal(new double[] { 0, 10, 20 }, Size(chain, circles[0], w)!.KeyframeXs);
    }

    [Fact]
    public void Build_PolygonHasAnOffsetLaneButNoSizeLane()
    {
        var (chain, _) = RectChain(3);
        var polys = new PolygonSave[3];
        for (int i = 0; i < 3; i++)
        {
            chain.Frames[i].ShapesSave = new ShapesSave();
            polys[i] = new PolygonSave { Name = "P", X = i * 5 };
            chain.Frames[i].ShapesSave!.Add(polys[i]);
        }
        var w = new double[] { 10, 10, 10 };

        Assert.NotNull(Offset(chain, polys[0], w));
        Assert.Null(Size(chain, polys[0], w));
    }

    [Fact]
    public void Build_UnnamedShape_GetsNoLane()
    {
        var (chain, rects) = RectChain(4);
        foreach (var r in rects) r.Name = "";

        Assert.Null(Offset(chain, rects[0]));
    }

    [Fact]
    public void Build_DuplicateNameInTheShapesFrame_GetsNoLane_AndBreaksTheRunForTheOtherFrames()
    {
        var (chain, rects) = RectChain(4);
        chain.Frames[1].ShapesSave!.Add(new AARectSave { Name = "A", X = 99, ScaleX = 8, ScaleY = 8 });

        Assert.Null(Offset(chain, rects[1])); // ambiguous in its own frame: it snaps, no lane

        // From frame 0 the ambiguous frame 1 is a gap, so the runs are [0] and [2..3].
        var lane = Offset(chain, rects[0])!;
        Assert.Equal(new double[] { 0, 50, 90 }, lane.KeyframeXs);
        Assert.Equal(new double[] { 20, 90, 140 }, lane.TickXs);
    }

    [Fact]
    public void Build_AShapeOfAnotherKindWithTheSameName_IsAGap()
    {
        var (chain, rects) = RectChain(4);
        RemoveFrom(chain, 2, rects[2]);
        chain.Frames[2].ShapesSave!.Add(new CircleSave { Name = "A", Radius = 3 });

        var lane = Offset(chain, rects[0])!;

        Assert.Equal(new double[] { 0, 20, 90 }, lane.KeyframeXs);
    }

    // ---- Where the name is held ----------------------------------------------------------------

    [Fact]
    public void Build_AFrameWithoutTheName_LeavesAGap_NoTickNoDiamond_AndNoSpanCrossesIt()
    {
        var (chain, rects) = RectChain(4);
        RemoveFrom(chain, 2, rects[2]);
        rects[0].OffsetInterpolation = "Linear";
        rects[1].OffsetInterpolation = "Linear";

        var lane = Offset(chain, rects[0])!;

        Assert.Equal(new double[] { 0, 20, 90 }, lane.KeyframeXs);
        Assert.Equal(new double[] { 20, 50, 140 }, lane.TickXs); // frame 2 (right edge 90) has none
        var span = Assert.Single(lane.Spans);
        Assert.Equal(0, span.StartX);
        Assert.Equal(20, span.EndX);
        // frame 1's keyframe holds to the end of its run, the gap, and no further
        Assert.Contains(new KeyframeLaneHold(20, 50), lane.Holds);
    }

    [Fact]
    public void Build_TheFirstFrameOfEachRunIsAlwaysAKeyframe_EvenWhenItsSkipFlagIsSet()
    {
        var (chain, rects) = RectChain(4);
        RemoveFrom(chain, 1, rects[1]);
        rects[0].SkipOffsetKeyframe = true; // frame 0, a run start
        rects[2].SkipOffsetKeyframe = true; // frame 2, the start of the run after the gap

        var lane = Offset(chain, rects[0])!;

        Assert.Equal(new double[] { 0, 50, 90 }, lane.KeyframeXs);
    }

    [Fact]
    public void Build_ASkippedFrame_DrawsNoDiamond_AndTheSpanFromThePreviousKeyframeRunsThroughIt()
    {
        var (chain, rects) = RectChain(4);
        rects[1].SkipOffsetKeyframe = true;
        rects[0].OffsetInterpolation = "Linear";
        rects[1].OffsetInterpolation = "Bounce"; // skipped: its own curve is ignored

        var lane = Offset(chain, rects[0])!;

        Assert.Equal(new double[] { 0, 50, 90 }, lane.KeyframeXs);
        Assert.Equal(new KeyframeLaneSpan(0, 50, KeyframeCurve.Parse("Linear", null)), lane.Spans[0]);
        Assert.Equal(new double[] { 20, 50, 90, 140 }, lane.TickXs);
    }

    [Fact]
    public void Build_SizeSkipFlagsAreIndependentOfOffsetSkipFlags()
    {
        var (chain, rects) = RectChain(4);
        rects[1].SkipOffsetKeyframe = true;
        rects[2].SkipSizeKeyframe = true;

        Assert.Equal(new double[] { 0, 50, 90 }, Offset(chain, rects[0])!.KeyframeXs);
        Assert.Equal(new double[] { 0, 20, 90 }, Size(chain, rects[0])!.KeyframeXs);
    }

    [Fact]
    public void Build_SpanUsesTheStartingKeyframesCurveForThatGroup()
    {
        var (chain, rects) = RectChain(3);
        rects[0].OffsetInterpolation = "Linear";
        rects[0].SizeInterpolation = "Quadratic";
        rects[0].SizeEasing = "In";
        var w = new double[] { 10, 10, 10 };

        Assert.Equal(KeyframeCurve.Parse("Linear", null), Offset(chain, rects[0], w)!.Spans[0].Curve);
        Assert.Equal(KeyframeCurve.Parse("Quadratic", "In"), Size(chain, rects[0], w)!.Spans[0].Curve);
    }

    [Fact]
    public void Build_TheSameLaneFromAnyFramesShape_NameIsTheIdentity()
    {
        var (chain, rects) = RectChain(4);
        RemoveFrom(chain, 2, rects[2]);

        var fromFirst = Offset(chain, rects[0])!;
        var fromLast = Offset(chain, rects[3])!;

        Assert.Equal(fromFirst.KeyframeXs, fromLast.KeyframeXs);
        Assert.Equal(fromFirst.TickXs, fromLast.TickXs);
        Assert.Equal(fromFirst.Holds, fromLast.Holds);
    }

    // ---- Holds, looping ------------------------------------------------------------------------

    [Fact]
    public void Build_NonLoopingChain_LastKeyframeHoldsToTheEndOfTheStrip()
    {
        var (chain, rects) = RectChain(4);

        var lane = Offset(chain, rects[0])!;

        Assert.Equal(new[] { new KeyframeLaneHold(90, 140) }, lane.Holds);
        Assert.Equal(140, lane.ContentWidth);
    }

    [Fact]
    public void Build_LoopingChain_LastKeyframeGlidesBackToTheEnd_WithNoHold()
    {
        var (chain, rects) = RectChain(4, loop: true);
        rects[3].OffsetInterpolation = "Linear";

        var lane = Offset(chain, rects[0])!;

        Assert.Empty(lane.Holds);
        Assert.Equal(new KeyframeLaneSpan(90, 140, KeyframeCurve.Parse("Linear", null)), lane.Spans[^1]);
    }

    [Fact]
    public void Build_LoopingChain_WhereTheNameIsNotInTheFirstFrame_DoesNotWrap_AndHolds()
    {
        var (chain, rects) = RectChain(4, loop: true);
        RemoveFrom(chain, 0, rects[0]);

        var lane = Offset(chain, rects[1])!;

        Assert.Equal(new[] { new KeyframeLaneHold(90, 140) }, lane.Holds);
        Assert.Equal(2, lane.Spans.Count);
    }

    [Fact]
    public void Build_LoopingChain_WithAGap_WrapsFromTheRunThatEndsTheChain_AndTheEarlierRunHolds()
    {
        var (chain, rects) = RectChain(4, loop: true);
        RemoveFrom(chain, 1, rects[1]);

        var lane = Offset(chain, rects[0])!;

        Assert.Equal(new KeyframeLaneSpan(50, 90, KeyframeCurve.Snap), lane.Spans[0]);
        Assert.Equal(new KeyframeLaneSpan(90, 140, KeyframeCurve.Snap), lane.Spans[1]);
        Assert.Equal(new[] { new KeyframeLaneHold(0, 20) }, lane.Holds);
    }

    [Fact]
    public void Build_LoopingChain_WhoseOnlyKeyframeIsTheFirstFrame_HoldsAndIsNotInUse()
    {
        var (chain, rects) = RectChain(4, loop: true);
        for (int i = 1; i < 4; i++) rects[i].SkipOffsetKeyframe = true;

        Assert.Null(Offset(chain, rects[0]));
    }

    [Fact]
    public void Build_SpanProgressIsTheEvaluators()
    {
        var (chain, rects) = RectChain(3);
        rects[0].X = 0;
        rects[2].X = 100;
        rects[1].SkipOffsetKeyframe = true;
        rects[0].OffsetInterpolation = "Bounce";
        rects[0].OffsetEasing = "Out";
        var w = new double[] { 10, 10, 10 };

        var lane = Offset(chain, rects[0], w)!;

        foreach (var (t, progress) in KeyframeLaneBuilder.CurvePoints(lane.Spans[0].Curve, 20))
        {
            double elapsed = t * 0.2;
            int frame = elapsed >= 0.1 ? 1 : 0;
            var value = KeyframeEvaluator.EvaluateShape(chain, frame, elapsed - frame * 0.1, rects[frame]);
            Assert.Equal(value.X / 100f, progress, 3);
        }
    }
}
