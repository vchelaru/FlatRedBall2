using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

/// <summary>
/// Shape keyframe interpolation (#1478 final phase): each shape glides its Offset (X/Y) and Size
/// (ScaleX/ScaleY, or Radius) to the next keyframe of the same group, matched across frames by Name.
/// </summary>
public class ShapeKeyframeEvaluatorTests
{
    private static AARectSave Rect(string name, float x = 0, float y = 0, float sx = 16, float sy = 16,
        string? offsetType = null, string? offsetEasing = null, bool skipOffset = false,
        string? sizeType = null, string? sizeEasing = null, bool skipSize = false)
        => new()
        {
            Name = name, X = x, Y = y, ScaleX = sx, ScaleY = sy,
            OffsetInterpolation = offsetType, OffsetEasing = offsetEasing, SkipOffsetKeyframe = skipOffset,
            SizeInterpolation = sizeType, SizeEasing = sizeEasing, SkipSizeKeyframe = skipSize,
        };

    private static CircleSave Circle(string name, float x = 0, float y = 0, float radius = 16,
        string? offsetType = null, string? sizeType = null)
        => new()
        {
            Name = name, X = x, Y = y, Radius = radius,
            OffsetInterpolation = offsetType, SizeInterpolation = sizeType,
        };

    private static AnimationFrameSave Frame(float length, params ShapeSave[] shapes)
    {
        var frame = new AnimationFrameSave { FrameLength = length, ShapesSave = new ShapesSave() };
        foreach (var s in shapes) frame.ShapesSave.Add(s);
        return frame;
    }

    private static AnimationChainSave Chain(bool loop, params AnimationFrameSave[] frames)
    {
        var chain = new AnimationChainSave { Name = "c", Loop = loop };
        chain.Frames.AddRange(frames);
        return chain;
    }

    private static KeyframeShapeValue At(AnimationChainSave chain, int frame, double elapsed, ShapeSave shape)
        => KeyframeEvaluator.EvaluateShape(chain, frame, elapsed, shape);

    // ---- Snap is today's behavior ---------------------------------------------------------------

    [Fact]
    public void EvaluateShape_DefaultSnap_ReturnsShapeOwnValuesAtAnyElapsed()
    {
        var a0 = Rect("A", 1, 2, 3, 4);
        var chain = Chain(true, Frame(1, a0), Frame(1, Rect("A", 100, 200, 30, 40)));

        var v = At(chain, 0, 0.9, a0);

        v.ShouldBe(new KeyframeShapeValue(1, 2, 3, 4));
    }

    // ---- Offset ---------------------------------------------------------------------------------

    [Fact]
    public void EvaluateShape_LinearOffset_GlidesXAndYToNextFrame()
    {
        var a0 = Rect("A", 0, 10, offsetType: "Linear");
        var chain = Chain(true, Frame(1, a0), Frame(1, Rect("A", 100, 30)));

        var v = At(chain, 0, 0.5, a0);

        v.X.ShouldBe(50f, 0.001f);
        v.Y.ShouldBe(20f, 0.001f);
    }

    [Fact]
    public void EvaluateShape_LinearOffset_SizeStaysSnapped()
    {
        var a0 = Rect("A", 0, 0, 10, 20, offsetType: "Linear");
        var chain = Chain(true, Frame(1, a0), Frame(1, Rect("A", 100, 0, 50, 60)));

        var v = At(chain, 0, 0.5, a0);

        v.SizeX.ShouldBe(10f);
        v.SizeY.ShouldBe(20f);
    }

    [Fact]
    public void EvaluateShape_EaseOutOffset_FollowsCurve()
    {
        var a0 = Rect("A", 0, offsetType: "Quadratic", offsetEasing: "Out");
        var chain = Chain(true, Frame(1, a0), Frame(1, Rect("A", 100)));

        At(chain, 0, 0.5, a0).X.ShouldBe(75f, 0.001f);
    }

    [Fact]
    public void EvaluateShape_ArrivesExactlyAtNextKeyframeValue()
    {
        var a0 = Rect("A", 0, offsetType: "Linear");
        var a1 = Rect("A", 100);
        var chain = Chain(true, Frame(1, a0), Frame(1, a1));

        At(chain, 1, 0, a1).X.ShouldBe(100f);
    }

    // ---- Size -----------------------------------------------------------------------------------

    [Fact]
    public void EvaluateShape_LinearSize_GlidesRectScaleXAndY()
    {
        var a0 = Rect("A", sx: 10, sy: 20, sizeType: "Linear");
        var chain = Chain(true, Frame(1, a0), Frame(1, Rect("A", sx: 30, sy: 60)));

        var v = At(chain, 0, 0.5, a0);

        v.SizeX.ShouldBe(20f, 0.001f);
        v.SizeY.ShouldBe(40f, 0.001f);
    }

    [Fact]
    public void EvaluateShape_LinearSize_GlidesCircleRadius()
    {
        var c0 = Circle("C", radius: 10, sizeType: "Linear");
        var chain = Chain(true, Frame(1, c0), Frame(1, Circle("C", radius: 30)));

        At(chain, 0, 0.25, c0).SizeX.ShouldBe(15f, 0.001f);
    }

    [Fact]
    public void EvaluateShape_Circle_OffsetAndSizeUseTheirOwnCurves()
    {
        var c0 = Circle("C", 0, 0, 10, offsetType: "Linear");
        var chain = Chain(true, Frame(1, c0), Frame(1, Circle("C", 100, 0, 30)));

        var v = At(chain, 0, 0.5, c0);

        v.X.ShouldBe(50f, 0.001f);
        v.SizeX.ShouldBe(10f);
    }

    // ---- Matching by name -----------------------------------------------------------------------

    [Fact]
    public void EvaluateShape_ShapesInDifferentOrder_MatchByName()
    {
        var a0 = Rect("A", 0, offsetType: "Linear");
        var chain = Chain(true,
            Frame(1, Rect("B", 500), a0),
            Frame(1, Rect("A", 100), Rect("B", 900)));

        At(chain, 0, 0.5, a0).X.ShouldBe(50f, 0.001f);
    }

    [Fact]
    public void EvaluateShape_UnnamedShape_Snaps()
    {
        var u0 = Rect("", 0, offsetType: "Linear");
        var chain = Chain(true, Frame(1, u0), Frame(1, Rect("", 100)));

        At(chain, 0, 0.5, u0).X.ShouldBe(0f);
    }

    [Fact]
    public void EvaluateShape_DuplicateNameInNextFrame_Snaps()
    {
        var a0 = Rect("A", 0, offsetType: "Linear");
        var chain = Chain(true, Frame(1, a0), Frame(1, Rect("A", 100), Rect("A", 200)));

        At(chain, 0, 0.5, a0).X.ShouldBe(0f);
    }

    [Fact]
    public void EvaluateShape_DuplicateNameInOwnFrame_Snaps()
    {
        var a0 = Rect("A", 0, offsetType: "Linear");
        var chain = Chain(true, Frame(1, a0, Rect("A", 7)), Frame(1, Rect("A", 100)));

        At(chain, 0, 0.5, a0).X.ShouldBe(0f);
    }

    [Fact]
    public void EvaluateShape_SameNameDifferentKind_Snaps()
    {
        var a0 = Rect("A", 0, offsetType: "Linear");
        var chain = Chain(true, Frame(1, a0), Frame(1, Circle("A", 100)));

        At(chain, 0, 0.5, a0).X.ShouldBe(0f);
    }

    [Fact]
    public void EvaluateShape_NameAbsentFromNextFrame_HoldsOwnValue()
    {
        var a0 = Rect("A", 0, offsetType: "Linear");
        var chain = Chain(true, Frame(1, a0), Frame(1), Frame(1, Rect("A", 100)));

        At(chain, 0, 0.9, a0).X.ShouldBe(0f);
    }

    [Fact]
    public void EvaluateShape_GlideEndsAtFrameWhereNameStopsAppearing_EvenAcrossSkippedFrames()
    {
        // A appears in frames 0 and 1 (skipped), then not in frame 2: nothing to glide to.
        var a0 = Rect("A", 0, offsetType: "Linear");
        var chain = Chain(true, Frame(1, a0), Frame(1, Rect("A", 999, skipOffset: true)), Frame(1), Frame(1, Rect("A", 100)));

        At(chain, 0, 0.5, a0).X.ShouldBe(0f);
    }

    // ---- Skipped frames -------------------------------------------------------------------------

    [Fact]
    public void EvaluateShape_SkippedFrameBetween_GlideSpansSummedFrameLengths()
    {
        var a0 = Rect("A", 0, offsetType: "Linear");
        var a1 = Rect("A", 999, skipOffset: true);
        var chain = Chain(true, Frame(1, a0), Frame(1, a1), Frame(2, Rect("A", 100)));

        // 1.5s into a 2s glide, evaluated from the skipped frame (0.5s into it).
        At(chain, 1, 0.5, a1).X.ShouldBe(75f, 0.001f);
        At(chain, 0, 0.5, a0).X.ShouldBe(25f, 0.001f);
    }

    [Fact]
    public void EvaluateShape_SkippedOffsetFrame_StillHasItsOwnSizeKeyframe()
    {
        var a0 = Rect("A", 0, sx: 10, offsetType: "Linear", sizeType: "Linear");
        var a1 = Rect("A", 999, sx: 40, skipOffset: true, sizeType: "Linear");
        var chain = Chain(true, Frame(1, a0), Frame(1, a1), Frame(1, Rect("A", 100, sx: 70)));

        var v = At(chain, 1, 0.5, a1);

        v.X.ShouldBe(75f, 0.001f);   // offset glides 0 -> 100 across both frames
        v.SizeX.ShouldBe(55f, 0.001f); // size keyframe at frame 1: 40 -> 70, halfway
    }

    [Fact]
    public void EvaluateShape_SkippedFrameFirstOfRun_IsStillAKeyframe()
    {
        // A first appears in frame 1; its skip flag is ignored there, like frame 0 of a chain.
        var a1 = Rect("A", 40, skipOffset: true, offsetType: "Linear");
        var chain = Chain(true, Frame(1), Frame(1, a1), Frame(1, Rect("A", 100)));

        At(chain, 1, 0.5, a1).X.ShouldBe(70f, 0.001f);
    }

    [Fact]
    public void EvaluateShape_SkippedFrameAfterNameGap_StartsANewRun()
    {
        // Frame 2 is skipped but the previous frame lacks A, so frame 2 starts its own run.
        var a0 = Rect("A", 0, offsetType: "Linear");
        var a2 = Rect("A", 40, skipOffset: true, offsetType: "Linear");
        var chain = Chain(false, Frame(1, a0), Frame(1), Frame(1, a2), Frame(1, Rect("A", 100)));

        At(chain, 2, 0.5, a2).X.ShouldBe(70f, 0.001f);
    }

    // ---- Loop and hold --------------------------------------------------------------------------

    [Fact]
    public void EvaluateShape_NonLoopingLastFrame_Holds()
    {
        var a1 = Rect("A", 100, offsetType: "Linear");
        var chain = Chain(false, Frame(1, Rect("A", 0, offsetType: "Linear")), Frame(1, a1));

        At(chain, 1, 0.9, a1).X.ShouldBe(100f);
    }

    [Fact]
    public void EvaluateShape_LoopingLastFrame_GlidesBackToFirst()
    {
        var a1 = Rect("A", 100, offsetType: "Linear");
        var chain = Chain(true, Frame(1, Rect("A", 0)), Frame(1, a1));

        At(chain, 1, 0.5, a1).X.ShouldBe(50f, 0.001f);
    }

    [Fact]
    public void EvaluateShape_LoopingLastFrame_FirstFrameLacksName_Holds()
    {
        var a1 = Rect("A", 100, offsetType: "Linear");
        var chain = Chain(true, Frame(1), Frame(1, a1));

        At(chain, 1, 0.5, a1).X.ShouldBe(100f);
    }

    [Fact]
    public void EvaluateShape_SingleFrameLoop_Holds()
    {
        var a0 = Rect("A", 5, offsetType: "Linear");
        var chain = Chain(true, Frame(1, a0));

        At(chain, 0, 0.5, a0).X.ShouldBe(5f);
    }

    [Fact]
    public void EvaluateShape_BackCurve_SwingsPastEndpoints()
    {
        var a0 = Rect("A", 0, offsetType: "Back", offsetEasing: "In");
        var chain = Chain(true, Frame(1, a0), Frame(1, Rect("A", 100)));

        At(chain, 0, 0.2, a0).X.ShouldBeLessThan(0f);
    }

    [Fact]
    public void EvaluateShape_ShapeNotInFrame_ReturnsItsOwnValues()
    {
        var stray = Rect("A", 3, 4, 5, 6);
        var chain = Chain(true, Frame(1), Frame(1));

        At(chain, 0, 0.5, stray).ShouldBe(new KeyframeShapeValue(3, 4, 5, 6));
    }

    [Fact]
    public void EvaluateShape_PolygonOffset_Glides()
    {
        var p0 = new PolygonSave { Name = "P", X = 0, OffsetInterpolation = "Linear" };
        var chain = Chain(true, Frame(1, p0), Frame(1, new PolygonSave { Name = "P", X = 80 }));

        At(chain, 0, 0.5, p0).X.ShouldBe(40f, 0.001f);
    }
}
