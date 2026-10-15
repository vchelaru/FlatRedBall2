using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

/// <summary>
/// Keyframe interpolation (#1478 phase 1): every frame is a keyframe, a keyframe's curve runs to the
/// next keyframe of the same group, and Snap (the default) reproduces the pre-interpolation behavior.
/// </summary>
public class KeyframeEvaluatorTests
{
    private static AnimationFrameSave Frame(float x = 0, float y = 0, float length = 1f,
        string? offsetType = null, string? offsetEasing = null, bool skipOffset = false)
        => new()
        {
            FrameLength = length,
            RelativeX = x,
            RelativeY = y,
            OffsetInterpolation = offsetType,
            OffsetEasing = offsetEasing,
            SkipOffsetKeyframe = skipOffset,
        };

    private static AnimationChainSave Chain(bool loop, params AnimationFrameSave[] frames)
    {
        var chain = new AnimationChainSave { Name = "c", Loop = loop };
        chain.Frames.AddRange(frames);
        return chain;
    }

    // ---- Offset: snap is today's behavior -------------------------------------------------------

    [Fact]
    public void EvaluateOffset_DefaultFrames_ReturnsFrameOwnOffsetAtAnyElapsed()
    {
        var chain = Chain(true, Frame(10, 20), Frame(50, 60), Frame(-5, 7));

        for (int i = 0; i < 3; i++)
            foreach (double elapsed in new[] { 0.0, 0.3, 0.999 })
            {
                var (x, y) = KeyframeEvaluator.EvaluateOffset(chain, i, elapsed);
                x.ShouldBe(chain.Frames[i].RelativeX);
                y.ShouldBe(chain.Frames[i].RelativeY);
            }
    }

    [Fact]
    public void EvaluateOffset_ExplicitInstant_BehavesAsSnap()
    {
        var chain = Chain(true, Frame(0, offsetType: "Instant"), Frame(100));

        KeyframeEvaluator.EvaluateOffset(chain, 0, 0.99).X.ShouldBe(0f);
    }

    // ---- Offset: glide --------------------------------------------------------------------------

    [Fact]
    public void EvaluateOffset_LinearToNextFrame_ReturnsMidpointAtHalfway()
    {
        var chain = Chain(true, Frame(0, 10, offsetType: "Linear"), Frame(100, 30));

        var (x, y) = KeyframeEvaluator.EvaluateOffset(chain, 0, 0.5);

        x.ShouldBe(50f, 0.001f);
        y.ShouldBe(20f, 0.001f);
    }

    [Fact]
    public void EvaluateOffset_ArrivesExactlyAtNextKeyframeValue()
    {
        var chain = Chain(true, Frame(0, offsetType: "Linear"), Frame(100));

        KeyframeEvaluator.EvaluateOffset(chain, 1, 0).X.ShouldBe(100f);
    }

    [Fact]
    public void EvaluateOffset_UnequalFrameLengths_UsesOwnFrameLength()
    {
        var chain = Chain(true, Frame(0, length: 4f, offsetType: "Linear"), Frame(100, length: 1f));

        KeyframeEvaluator.EvaluateOffset(chain, 0, 1.0).X.ShouldBe(25f, 0.001f);
    }

    // ---- Offset: skipped frames -----------------------------------------------------------------

    [Fact]
    public void EvaluateOffset_SkippedFrameBetween_GlideSpansSummedFrameLengths()
    {
        // A(0) -Linear-> C(100), B skipped. Segment = A.length + B.length = 2s.
        var chain = Chain(true,
            Frame(0, offsetType: "Linear"),
            Frame(skipOffset: true),
            Frame(100));

        KeyframeEvaluator.EvaluateOffset(chain, 0, 0.5).X.ShouldBe(25f, 0.001f);
        KeyframeEvaluator.EvaluateOffset(chain, 1, 0.5).X.ShouldBe(75f, 0.001f);
        KeyframeEvaluator.EvaluateOffset(chain, 2, 0).X.ShouldBe(100f);
    }

    [Fact]
    public void EvaluateOffset_SkippedFrame_OwnValueAndCurveAreIgnored()
    {
        var chain = Chain(true,
            Frame(0, offsetType: "Linear"),
            Frame(999, 999, offsetType: "Cubic", offsetEasing: "In", skipOffset: true),
            Frame(100));

        var (x, y) = KeyframeEvaluator.EvaluateOffset(chain, 1, 0.5);

        x.ShouldBe(75f, 0.001f);
        y.ShouldBe(0f, 0.001f);
    }

    [Fact]
    public void EvaluateOffset_SkippedFramesAfterSnapKeyframe_HoldStartThenSnapAtNextKeyframe()
    {
        var chain = Chain(true,
            Frame(10),
            Frame(skipOffset: true),
            Frame(skipOffset: true),
            Frame(90));

        KeyframeEvaluator.EvaluateOffset(chain, 1, 0.9).X.ShouldBe(10f);
        KeyframeEvaluator.EvaluateOffset(chain, 2, 0.9).X.ShouldBe(10f);
        KeyframeEvaluator.EvaluateOffset(chain, 3, 0).X.ShouldBe(90f);
    }

    [Fact]
    public void EvaluateOffset_FirstFrameMarkedSkipped_StillActsAsKeyframe()
    {
        var chain = Chain(true, Frame(0, offsetType: "Linear", skipOffset: true), Frame(100));

        KeyframeEvaluator.EvaluateOffset(chain, 0, 0.5).X.ShouldBe(50f, 0.001f);
    }

    // ---- Offset: ends of the chain --------------------------------------------------------------

    [Fact]
    public void EvaluateOffset_LoopingChain_LastKeyframeGlidesBackToFirst()
    {
        var chain = Chain(true, Frame(-10), Frame(10, offsetType: "Linear"));

        KeyframeEvaluator.EvaluateOffset(chain, 1, 0.5).X.ShouldBe(0f, 0.001f);
    }

    [Fact]
    public void EvaluateOffset_LoopingChain_TrailingSkippedFramesAreCoveredByWrapGlide()
    {
        // B(keyframe) -Linear-> wraps to A across B (1s) + C (1s, skipped).
        var chain = Chain(true,
            Frame(0),
            Frame(100, offsetType: "Linear"),
            Frame(skipOffset: true));

        KeyframeEvaluator.EvaluateOffset(chain, 1, 0.5).X.ShouldBe(75f, 0.001f);
        KeyframeEvaluator.EvaluateOffset(chain, 2, 0.5).X.ShouldBe(25f, 0.001f);
    }

    [Fact]
    public void EvaluateOffset_NonLoopingChain_LastFrameHolds()
    {
        var chain = Chain(false, Frame(0, offsetType: "Linear"), Frame(100, offsetType: "Linear"));

        KeyframeEvaluator.EvaluateOffset(chain, 1, 0.5).X.ShouldBe(100f);
    }

    [Fact]
    public void EvaluateOffset_NonLoopingChain_TrailingSkippedFramesHoldLastKeyframe()
    {
        var chain = Chain(false,
            Frame(0),
            Frame(100, offsetType: "Linear"),
            Frame(skipOffset: true));

        KeyframeEvaluator.EvaluateOffset(chain, 2, 0.5).X.ShouldBe(100f);
    }

    [Fact]
    public void EvaluateOffset_SingleFrameChain_HoldsOwnValue()
    {
        var chain = Chain(true, Frame(7, 9, offsetType: "Linear"));

        var (x, y) = KeyframeEvaluator.EvaluateOffset(chain, 0, 0.5);

        x.ShouldBe(7f);
        y.ShouldBe(9f);
    }

    // ---- Offset: easing -------------------------------------------------------------------------

    [Theory]
    [InlineData("In", 25f)]
    [InlineData("Out", 75f)]
    [InlineData("InOut", 50f)]
    [InlineData(null, 75f)]
    [InlineData("NotAnEasing", 75f)]
    public void EvaluateOffset_QuadraticAtHalfway_FollowsEasing(string? easing, float expected)
    {
        var chain = Chain(true, Frame(0, offsetType: "Quadratic", offsetEasing: easing), Frame(100));

        KeyframeEvaluator.EvaluateOffset(chain, 0, 0.5).X.ShouldBe(expected, 0.001f);
    }

    [Theory]
    [InlineData("In")]
    [InlineData("Out")]
    [InlineData("InOut")]
    public void EvaluateOffset_LinearIgnoresEasing(string easing)
    {
        var chain = Chain(true, Frame(0, offsetType: "Linear", offsetEasing: easing), Frame(100));

        KeyframeEvaluator.EvaluateOffset(chain, 0, 0.25).X.ShouldBe(25f, 0.001f);
    }

    [Theory]
    [InlineData("In")]
    [InlineData("Out")]
    [InlineData("InOut")]
    public void EvaluateOffset_InstantIgnoresEasing_HoldsStartUntilSegmentEnd(string easing)
    {
        var chain = Chain(true, Frame(0, offsetType: "Instant", offsetEasing: easing), Frame(100));

        KeyframeEvaluator.EvaluateOffset(chain, 0, 0.999).X.ShouldBe(0f);
    }

    [Theory]
    [InlineData("Bogus")]
    [InlineData("")]
    [InlineData("3")]
    public void EvaluateOffset_UnknownCurveName_TreatedAsSnap(string type)
    {
        var chain = Chain(true, Frame(0, offsetType: type), Frame(100));

        KeyframeEvaluator.EvaluateOffset(chain, 0, 0.5).X.ShouldBe(0f);
    }

    [Fact]
    public void EvaluateOffset_EasingWithoutType_StaysSnap()
    {
        var chain = Chain(true, Frame(0, offsetEasing: "In"), Frame(100));

        KeyframeEvaluator.EvaluateOffset(chain, 0, 0.5).X.ShouldBe(0f);
    }

    // ---- Color ----------------------------------------------------------------------------------

    private static AnimationFrameSave ColorFrame(int? red = null, int? green = null, int? alpha = null,
        string? type = null, string? easing = null, bool skip = false, ColorOpArg op = ColorOpArg.None)
        => new()
        {
            FrameLength = 1f,
            Red = red,
            Green = green,
            Alpha = alpha,
            ColorInterpolation = type,
            ColorEasing = easing,
            SkipColorKeyframe = skip,
            ColorOperation = op switch
            {
                ColorOpArg.Add => FlatRedBall2.Animation.ColorOperation.Add,
                ColorOpArg.Multiply => FlatRedBall2.Animation.ColorOperation.Multiply,
                _ => null,
            },
        };

    private enum ColorOpArg { None, Add, Multiply }

    [Fact]
    public void EvaluateColor_DefaultFrames_ReturnsResolvedFrameColor()
    {
        var chain = Chain(true,
            ColorFrame(red: 10, alpha: 200),
            ColorFrame(green: 40));

        var c0 = KeyframeEvaluator.EvaluateColor(chain, 0, 0.7);
        c0.Red.ShouldBe(10);
        c0.Green.ShouldBeNull();
        c0.Alpha.ShouldBe(200);

        var c1 = KeyframeEvaluator.EvaluateColor(chain, 1, 0.7);
        c1.Red.ShouldBe(10);   // inherited
        c1.Green.ShouldBe(40);
        c1.Alpha.ShouldBe(200); // inherited
    }

    [Fact]
    public void EvaluateColor_ChannelsInterpolateIndependently()
    {
        var chain = Chain(true,
            ColorFrame(red: 0, green: 0, type: "Linear"),
            ColorFrame(red: 100, green: 200));

        var c = KeyframeEvaluator.EvaluateColor(chain, 0, 0.5);

        c.Red.ShouldBe(50);
        c.Green.ShouldBe(100);
    }

    [Fact]
    public void EvaluateColor_NullChannelOnKeyframeInheritsBeforeInterpolating()
    {
        // Frame 1's Red is unset so it inherits 0 from frame 0; it is the keyframe that glides to frame 2.
        var chain = Chain(true,
            ColorFrame(red: 0),
            ColorFrame(type: "Linear"),
            ColorFrame(red: 100));

        KeyframeEvaluator.EvaluateColor(chain, 1, 0.5).Red.ShouldBe(50);
    }

    [Fact]
    public void EvaluateColor_ChannelNullAtBothEnds_StaysNull()
    {
        var chain = Chain(true, ColorFrame(red: 0, type: "Linear"), ColorFrame(red: 100));

        KeyframeEvaluator.EvaluateColor(chain, 0, 0.5).Green.ShouldBeNull();
    }

    [Fact]
    public void EvaluateColor_ChannelNullAtOneEnd_UsesOperationIdentity()
    {
        // Unset Alpha reads as 255 (opaque), so 255 -> 100 at the midpoint is 177.5, rounded away from zero.
        var chain = Chain(true, ColorFrame(type: "Linear"), ColorFrame(alpha: 100));

        KeyframeEvaluator.EvaluateColor(chain, 0, 0.5).Alpha.ShouldBe(178);
    }

    [Fact]
    public void EvaluateColor_AddOperationNullChannel_UsesZeroIdentity()
    {
        var chain = Chain(true,
            ColorFrame(op: ColorOpArg.Add, type: "Linear"),
            ColorFrame(red: 100));

        KeyframeEvaluator.EvaluateColor(chain, 0, 0.5).Red.ShouldBe(50);
    }

    [Fact]
    public void EvaluateColor_SkippedFrame_GlideSpansItAndItsOwnValueIsIgnored()
    {
        var chain = Chain(true,
            ColorFrame(red: 0, type: "Linear"),
            ColorFrame(red: 255, skip: true),
            ColorFrame(red: 100));

        KeyframeEvaluator.EvaluateColor(chain, 1, 0.5).Red.ShouldBe(75);
    }

    [Fact]
    public void EvaluateColor_KeyframeAfterSkippedFrame_DoesNotInheritSkippedFramesChannel()
    {
        // Frame 2 leaves Red unset; it inherits from keyframe 0 (Red=20), not from skipped frame 1 (Red=255).
        var chain = Chain(false,
            ColorFrame(red: 20),
            ColorFrame(red: 255, skip: true),
            ColorFrame());

        KeyframeEvaluator.EvaluateColor(chain, 2, 0).Red.ShouldBe(20);
    }

    [Fact]
    public void EvaluateColor_OffsetAndColorGroupsAreIndependent()
    {
        var chain = Chain(true,
            new AnimationFrameSave { FrameLength = 1f, OffsetInterpolation = "Linear", ColorInterpolation = "Linear", Red = 0 },
            new AnimationFrameSave { FrameLength = 1f, RelativeX = 100, Red = 100, SkipColorKeyframe = true },
            new AnimationFrameSave { FrameLength = 1f, RelativeX = 200, Red = 200 });

        // Frame 1 is an offset keyframe (snap), but color is skipped there and glides 0 -> 200 over 2s.
        KeyframeEvaluator.EvaluateOffset(chain, 1, 0.5).X.ShouldBe(100f);
        KeyframeEvaluator.EvaluateColor(chain, 1, 0.5).Red.ShouldBe(150);
    }

    [Fact]
    public void EvaluateColor_LoopingLastKeyframeGlidesToFirst()
    {
        var chain = Chain(true, ColorFrame(alpha: 0), ColorFrame(alpha: 200, type: "Linear"));

        KeyframeEvaluator.EvaluateColor(chain, 1, 0.5).Alpha.ShouldBe(100);
    }

    [Fact]
    public void EvaluateColor_NonLoopingLastFrameHolds()
    {
        var chain = Chain(false, ColorFrame(alpha: 0, type: "Linear"), ColorFrame(alpha: 200, type: "Linear"));

        KeyframeEvaluator.EvaluateColor(chain, 1, 0.5).Alpha.ShouldBe(200);
    }

    // ---- Overshoot / anticipate: the evaluator returns the raw value, clamping is the renderers' job ----

    [Fact]
    public void EvaluateOffset_Overshoot_GoesPastEndValueMidSpan()
    {
        var chain = Chain(true, Frame(0, 10, offsetType: "Back", offsetEasing: "Out"), Frame(100, 30));

        var (x, y) = KeyframeEvaluator.EvaluateOffset(chain, 0, 0.5);

        x.ShouldBeGreaterThan(100f);
        y.ShouldBeGreaterThan(30f);
    }

    [Fact]
    public void EvaluateOffset_Anticipate_GoesBeforeStartValueMidSpan()
    {
        var chain = Chain(true, Frame(0, 30, offsetType: "Back", offsetEasing: "In"), Frame(100, 10));

        var (x, y) = KeyframeEvaluator.EvaluateOffset(chain, 0, 0.25);

        x.ShouldBeLessThan(0f);
        y.ShouldBeGreaterThan(30f); // dips "before" the start, which for a falling Y is above it
    }

    [Fact]
    public void EvaluateOffset_AnticipateTowardsLowerValue_GoesAboveStartValue()
    {
        var chain = Chain(true, Frame(100, offsetType: "Back", offsetEasing: "In"), Frame(0));

        KeyframeEvaluator.EvaluateOffset(chain, 0, 0.25).X.ShouldBeGreaterThan(100f);
    }

    [Theory]
    [InlineData("In")]
    [InlineData("Out")]
    public void EvaluateOffset_BackCurve_EndpointsStayExact(string easing)
    {
        var chain = Chain(true, Frame(0, offsetType: "Back", offsetEasing: easing), Frame(100));

        KeyframeEvaluator.EvaluateOffset(chain, 0, 0).X.ShouldBe(0f);
        KeyframeEvaluator.EvaluateOffset(chain, 1, 0).X.ShouldBe(100f);
    }

    [Fact]
    public void EvaluateColor_Overshoot_ReturnsValuesPastRangeUnclamped()
    {
        var chain = Chain(true,
            ColorFrame(red: 0, green: 100, alpha: 0, type: "Back", easing: "Out"),
            ColorFrame(red: 255, green: 255, alpha: 255));

        var c = KeyframeEvaluator.EvaluateColor(chain, 0, 0.5);

        c.Red!.Value.ShouldBeGreaterThan(255);
        c.Green!.Value.ShouldBeGreaterThan(255);
        c.Alpha!.Value.ShouldBeGreaterThan(255);
    }

    [Fact]
    public void EvaluateColor_Anticipate_ReturnsNegativeChannelsUnclamped()
    {
        var chain = Chain(true,
            ColorFrame(red: 0, alpha: 0, type: "Back", easing: "In"),
            ColorFrame(red: 100, alpha: 200));

        var c = KeyframeEvaluator.EvaluateColor(chain, 0, 0.25);

        c.Red!.Value.ShouldBeLessThan(0);
        c.Alpha!.Value.ShouldBeLessThan(0);
    }

    [Fact]
    public void EvaluateColor_BackCurve_EndpointsStayExact()
    {
        var chain = Chain(true,
            ColorFrame(red: 10, type: "Back", easing: "Out"),
            ColorFrame(red: 200));

        KeyframeEvaluator.EvaluateColor(chain, 0, 0).Red.ShouldBe(10);
        KeyframeEvaluator.EvaluateColor(chain, 1, 0).Red.ShouldBe(200);
    }

    [Fact]
    public void EvaluateColor_BackCurveNullChannelAtStart_StaysNullAtExactStart()
    {
        var chain = Chain(true, ColorFrame(type: "Back", easing: "Out"), ColorFrame(red: 100));

        KeyframeEvaluator.EvaluateColor(chain, 0, 0).Red.ShouldBeNull();
    }

    [Fact]
    public void EvaluateColor_OvershootWithNullStartChannel_UsesIdentityAsStart()
    {
        // Add: unset Red is 0. Overshoot towards 100 passes 100 mid-span.
        var chain = Chain(true,
            ColorFrame(op: ColorOpArg.Add, type: "Back", easing: "Out"),
            ColorFrame(red: 100));

        KeyframeEvaluator.EvaluateColor(chain, 0, 0.5).Red!.Value.ShouldBeGreaterThan(100);
    }

    [Fact]
    public void EvaluateColor_AnticipateWithNullStartChannel_DipsBelowIdentity()
    {
        // Add: unset Red is 0, so anticipating towards 100 dips below 0.
        var chain = Chain(true,
            ColorFrame(op: ColorOpArg.Add, type: "Back", easing: "In"),
            ColorFrame(red: 100));

        KeyframeEvaluator.EvaluateColor(chain, 0, 0.25).Red!.Value.ShouldBeLessThan(0);
    }

    [Fact]
    public void EvaluateColor_BackCurveChannelNullAtBothEnds_StaysNull()
    {
        var chain = Chain(true, ColorFrame(red: 0, type: "Back", easing: "Out"), ColorFrame(red: 100));

        KeyframeEvaluator.EvaluateColor(chain, 0, 0.5).Green.ShouldBeNull();
    }
}
