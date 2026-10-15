using System.Numerics;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

/// <summary>
/// Polygon point interpolation (#1484): point i of a polygon glides to point i of the same-named polygon in the
/// next Points keyframe. A changed point count snaps. Points are their own keyframe group, independent of Offset.
/// </summary>
public class PolygonPointsKeyframeEvaluatorTests
{
    private static PolygonSave Poly(string name, float[] xy, string? pointsType = null, string? pointsEasing = null,
        bool skipPoints = false, string? offsetType = null, float x = 0)
    {
        var poly = new PolygonSave
        {
            Name = name, X = x, PointsInterpolation = pointsType, PointsEasing = pointsEasing,
            SkipPointsKeyframe = skipPoints, OffsetInterpolation = offsetType,
        };
        for (int i = 0; i < xy.Length; i += 2) poly.Points.Add(new Vector2Save { X = xy[i], Y = xy[i + 1] });
        return poly;
    }

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

    private static IReadOnlyList<Vector2> At(AnimationChainSave chain, int frame, double elapsed, PolygonSave poly)
        => KeyframeEvaluator.EvaluateShapePoints(chain, frame, elapsed, poly);

    private static void ShouldBePoints(IReadOnlyList<Vector2> actual, params float[] xy)
    {
        actual.Count.ShouldBe(xy.Length / 2);
        for (int i = 0; i < actual.Count; i++)
        {
            actual[i].X.ShouldBe(xy[i * 2], 0.001f);
            actual[i].Y.ShouldBe(xy[i * 2 + 1], 0.001f);
        }
    }

    [Fact]
    public void DefaultSnap_ReturnsOwnPointsAtAnyElapsed()
    {
        var p0 = Poly("P", [0, 0, 10, 0, 10, 10]);
        var chain = Chain(true, Frame(1, p0), Frame(1, Poly("P", [100, 100, 200, 100, 200, 200])));

        ShouldBePoints(At(chain, 0, 0.9, p0), 0, 0, 10, 0, 10, 10);
    }

    [Fact]
    public void Linear_GlidesEachPointToTheSameIndexInTheNextFrame()
    {
        var p0 = Poly("P", [0, 0, 10, 0, 10, 10], "Linear");
        var chain = Chain(true, Frame(1, p0), Frame(1, Poly("P", [100, 20, 110, 20, 110, 30])));

        ShouldBePoints(At(chain, 0, 0.5, p0), 50, 10, 60, 10, 60, 20);
    }

    [Fact]
    public void Linear_ArrivesExactlyOnTheNextKeyframesPoints()
    {
        var p0 = Poly("P", [0, 0, 10, 0, 10, 10], "Linear");
        var p1 = Poly("P", [100, 20, 110, 20, 110, 30]);
        var chain = Chain(true, Frame(1, p0), Frame(1, p1));

        ShouldBePoints(At(chain, 1, 0, p1), 100, 20, 110, 20, 110, 30);
    }

    [Fact]
    public void RewoundPolygon_GlidesSlotToSlot()
    {
        // Same triangle with a different start point: no re-wind detection, slot i goes to slot i.
        var p0 = Poly("P", [0, 0, 10, 0, 10, 10], "Linear");
        var chain = Chain(true, Frame(1, p0), Frame(1, Poly("P", [10, 0, 10, 10, 0, 0])));

        ShouldBePoints(At(chain, 0, 0.5, p0), 5, 0, 10, 5, 5, 5);
    }

    [Fact]
    public void ChangedPointCount_Snaps_HoldingTheFirstFramesPoints()
    {
        var p0 = Poly("P", [0, 0, 10, 0, 10, 10], "Linear");
        var chain = Chain(true, Frame(1, p0), Frame(1, Poly("P", [0, 0, 10, 0, 10, 10, 0, 10])));

        ShouldBePoints(At(chain, 0, 0.5, p0), 0, 0, 10, 0, 10, 10);
    }

    [Fact]
    public void ChangedPointCount_TakesTheNewPointsWhenTheNextFramePlays()
    {
        var p1 = Poly("P", [0, 0, 10, 0, 10, 10, 0, 10]);
        var chain = Chain(true, Frame(1, Poly("P", [0, 0, 10, 0, 10, 10], "Linear")), Frame(1, p1));

        ShouldBePoints(At(chain, 1, 0, p1), 0, 0, 10, 0, 10, 10, 0, 10);
    }

    [Fact]
    public void GlideStopsWhereTheNameIsMissing()
    {
        var p0 = Poly("P", [0, 0, 10, 0, 10, 10], "Linear");
        var chain = Chain(false, Frame(1, p0), Frame(1), Frame(1, Poly("P", [100, 100, 110, 100, 110, 110])));

        ShouldBePoints(At(chain, 0, 0.5, p0), 0, 0, 10, 0, 10, 10);
    }

    [Fact]
    public void SkippedFrame_IsGlidedAcross_OverTheSummedFrameLengths()
    {
        var p0 = Poly("P", [0, 0, 10, 0, 10, 10], "Linear");
        var p1 = Poly("P", [999, 999, 999, 999, 999, 999], skipPoints: true);
        var chain = Chain(false, Frame(1, p0), Frame(1, p1), Frame(2, Poly("P", [40, 40, 50, 40, 50, 50])));

        // The glide spans frames 0 and 1 (2s in all); frame 1 at 0s is half way.
        ShouldBePoints(At(chain, 1, 0, p1), 20, 20, 30, 20, 30, 30);
    }

    [Fact]
    public void SkippedFrame_WithADifferentCount_DoesNotBreakTheGlideBetweenItsNeighbours()
    {
        var p0 = Poly("P", [0, 0, 10, 0, 10, 10], "Linear");
        var p1 = Poly("P", [5, 5, 6, 6, 7, 7, 8, 8], skipPoints: true);
        var chain = Chain(false, Frame(1, p0), Frame(1, p1), Frame(1, Poly("P", [20, 0, 30, 0, 30, 10])));

        ShouldBePoints(At(chain, 1, 0, p1), 10, 0, 20, 0, 20, 10);
    }

    [Fact]
    public void FirstFrameOfARun_IsAlwaysAKeyframe_EvenWhenFlaggedSkipped()
    {
        var p0 = Poly("P", [0, 0, 10, 0, 10, 10], "Linear", skipPoints: true);
        var chain = Chain(false, Frame(1, p0), Frame(1, Poly("P", [10, 0, 20, 0, 20, 10])));

        ShouldBePoints(At(chain, 0, 0.5, p0), 5, 0, 15, 0, 15, 10);
    }

    [Fact]
    public void LoopingChain_LastKeyframeGlidesBackToTheFirst()
    {
        var p1 = Poly("P", [100, 0, 110, 0, 110, 10], "Linear");
        var chain = Chain(true, Frame(1, Poly("P", [0, 0, 10, 0, 10, 10])), Frame(1, p1));

        ShouldBePoints(At(chain, 1, 0.5, p1), 50, 0, 60, 0, 60, 10);
    }

    [Fact]
    public void NonLoopingChain_LastKeyframeHolds()
    {
        var p1 = Poly("P", [100, 0, 110, 0, 110, 10], "Linear");
        var chain = Chain(false, Frame(1, Poly("P", [0, 0, 10, 0, 10, 10])), Frame(1, p1));

        ShouldBePoints(At(chain, 1, 0.9, p1), 100, 0, 110, 0, 110, 10);
    }

    [Fact]
    public void BackCurve_SwingsPastTheEndPoints()
    {
        var p0 = Poly("P", [0, 0, 10, 0, 10, 10], "Back", "Out");
        var chain = Chain(false, Frame(1, p0), Frame(1, Poly("P", [100, 0, 110, 0, 110, 10])));

        At(chain, 0, 0.6, p0)[0].X.ShouldBeGreaterThan(100f);
    }

    [Fact]
    public void PointsAreIndependentOfOffset()
    {
        var p0 = Poly("P", [0, 0, 10, 0, 10, 10], offsetType: "Linear");
        var chain = Chain(false, Frame(1, p0), Frame(1, Poly("P", [100, 0, 110, 0, 110, 10], x: 50)));

        ShouldBePoints(At(chain, 0, 0.5, p0), 0, 0, 10, 0, 10, 10);
        KeyframeEvaluator.EvaluateShape(chain, 0, 0.5, p0).X.ShouldBe(25f, 0.001f);
    }

    [Fact]
    public void PolygonWithoutAName_Snaps()
    {
        var p0 = Poly("", [0, 0, 10, 0, 10, 10], "Linear");
        var chain = Chain(false, Frame(1, p0), Frame(1, Poly("", [100, 0, 110, 0, 110, 10])));

        ShouldBePoints(At(chain, 0, 0.5, p0), 0, 0, 10, 0, 10, 10);
    }

    [Fact]
    public void NameThatChangesKind_Snaps()
    {
        var p0 = Poly("P", [0, 0, 10, 0, 10, 10], "Linear");
        var chain = Chain(false, Frame(1, p0), Frame(1, new AARectSave { Name = "P" }));

        ShouldBePoints(At(chain, 0, 0.5, p0), 0, 0, 10, 0, 10, 10);
    }

    [Fact]
    public void ListOverload_MatchesTheChainOverload()
    {
        var p0 = Poly("P", [0, 0, 10, 0, 10, 10], "Linear");
        var chain = Chain(false, Frame(1, p0), Frame(1, Poly("P", [100, 20, 110, 20, 110, 30])));
        var frames = SaveKeyframeFrame.Create(chain);

        var points = KeyframeEvaluator.EvaluateShapePoints(frames, chain.Loop, 0, 0.5, SaveKeyframeShape.Create(p0));

        ShouldBePoints(points, 50, 10, 60, 10, 60, 20);
    }
}
