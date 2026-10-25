using System.Numerics;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

/// <summary>
/// Keyframe interpolation while a frame plays backward (#1538, Reverse and PingPong's backward half): showing frame N
/// glides from N's value toward the previous keyframe's, governed by the earlier keyframe's curve played time-reversed,
/// so the backward path retraces the forward one. Snap holds the displayed keyframe.
/// </summary>
public class BackwardKeyframeEvaluatorTests
{
    private static AnimationFrameSave Frame(float x, string? type = null, string? easing = null, bool skip = false)
        => new() { FrameLength = 1f, RelativeX = x, OffsetInterpolation = type, OffsetEasing = easing, SkipOffsetKeyframe = skip };

    private static AnimationChainSave Chain(bool loop, params AnimationFrameSave[] frames)
    {
        var chain = new AnimationChainSave { Name = "c", Loop = loop };
        chain.Frames.AddRange(frames);
        return chain;
    }

    private static float BackwardX(AnimationChainSave chain, int frame, double elapsed)
        => KeyframeEvaluator.EvaluateOffset(chain, frame, elapsed, backward: true).X;

    [Fact]
    public void EvaluateOffset_Backward_EnteringAKeyframe_ShowsItsOwnValue()
    {
        var chain = Chain(false, Frame(0, "Linear"), Frame(100, "Linear"), Frame(200));

        BackwardX(chain, 2, 0).ShouldBe(200f);
        BackwardX(chain, 1, 0).ShouldBe(100f);
    }

    [Fact]
    public void EvaluateOffset_Backward_Linear_GlidesTowardThePreviousKeyframe()
    {
        var chain = Chain(false, Frame(0, "Linear"), Frame(100));

        BackwardX(chain, 1, 0.25).ShouldBe(75f, 0.001f);
    }

    [Fact]
    public void EvaluateOffset_Backward_Eased_RetracesTheForwardPathInReverse()
    {
        var chain = Chain(false, Frame(0, "Quadratic", "Out"), Frame(100));

        foreach (double p in new[] { 0.1, 0.3, 0.8 })
            BackwardX(chain, 1, p).ShouldBe(KeyframeEvaluator.EvaluateOffset(chain, 0, 1 - p).X, 0.001f);
    }

    [Fact]
    public void EvaluateOffset_Backward_Snap_HoldsTheDisplayedKeyframe()
    {
        var chain = Chain(true, Frame(0), Frame(100), Frame(200));

        BackwardX(chain, 1, 0.9).ShouldBe(100f);
    }

    [Fact]
    public void EvaluateOffset_Backward_SpansSkippedFrames()
    {
        // Keyframes 0 and 2; showing 2 then 1 backward covers the 0..2 segment.
        var chain = Chain(false, Frame(0, "Linear"), Frame(999, skip: true), Frame(100));

        BackwardX(chain, 1, 0).ShouldBe(50f, 0.001f);
    }

    [Fact]
    public void EvaluateOffset_Backward_FirstFrameOfNonLoopingChain_Holds()
    {
        var chain = Chain(false, Frame(0, "Linear"), Frame(100, "Linear"));

        BackwardX(chain, 0, 0.5).ShouldBe(0f);
    }

    [Fact]
    public void EvaluateOffset_Backward_FirstFrameOfLoopingChain_GlidesAcrossTheLoopToTheLastKeyframe()
    {
        // Wrap segment: the last keyframe's curve (100 -> 0 forward) retraced backward, 0 -> 100.
        var chain = Chain(true, Frame(0, "Linear"), Frame(100, "Linear"));

        BackwardX(chain, 0, 0.5).ShouldBe(50f, 0.001f);
    }

    [Fact]
    public void EvaluateOffset_Backward_PingPongChain_DoesNotGlideAcrossTheLoop()
    {
        var chain = Chain(true, Frame(0, "Linear"), Frame(100, "Linear"));
        chain.PingPong = true;

        BackwardX(chain, 0, 0.5).ShouldBe(0f);
        KeyframeEvaluator.EvaluateOffset(chain, 1, 0.5).X.ShouldBe(100f);
    }

    [Fact]
    public void EvaluateColor_Backward_Linear_GlidesTowardThePreviousKeyframe()
    {
        var chain = Chain(false,
            new AnimationFrameSave { FrameLength = 1f, Red = 0, ColorInterpolation = "Linear" },
            new AnimationFrameSave { FrameLength = 1f, Red = 100 });

        KeyframeEvaluator.EvaluateColor(chain, 1, 0.25, backward: true).Red.ShouldBe(75);
    }

    [Fact]
    public void EvaluateShape_Backward_Linear_GlidesTowardThePreviousKeyframe()
    {
        var first = new AARectSave { Name = "Box", X = 0, ScaleX = 16, ScaleY = 16, OffsetInterpolation = "Linear" };
        var second = new AARectSave { Name = "Box", X = 100, ScaleX = 16, ScaleY = 16 };
        var chain = Chain(false, ShapeFrame(first), ShapeFrame(second));

        KeyframeEvaluator.EvaluateShape(chain, 1, 0.25, second, backward: true).X.ShouldBe(75f, 0.001f);
    }

    [Fact]
    public void EvaluateShapePoints_Backward_Linear_GlidesTowardThePreviousKeyframe()
    {
        var first = new PolygonSave { Name = "Blade", PointsInterpolation = "Linear" };
        first.Points.Add(new Vector2Save { X = 0, Y = 0 });
        var second = new PolygonSave { Name = "Blade" };
        second.Points.Add(new Vector2Save { X = 100, Y = 0 });
        var chain = Chain(false, ShapeFrame(first), ShapeFrame(second));

        KeyframeEvaluator.EvaluateShapePoints(chain, 1, 0.25, second, backward: true)[0].X.ShouldBe(75f, 0.001f);
    }

    private static AnimationFrameSave ShapeFrame(ShapeSave shape)
    {
        var frame = new AnimationFrameSave { FrameLength = 1f, ShapesSave = new ShapesSave() };
        frame.ShapesSave.Add(shape);
        return frame;
    }
}
