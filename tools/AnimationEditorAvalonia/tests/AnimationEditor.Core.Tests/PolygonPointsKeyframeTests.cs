using AnimationEditor.Core.IO;
using AnimationEditor.Core.Utilities;
using AnimationEditor.Core.Keyframes;
using AnimationEditor.Core.ViewModels;
using FlatRedBall2.AnimationEditorCommon;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>Polygon Points as their own keyframe group in the editor: inspector state, commands, sampling, lanes (#1484).</summary>
[Collection("SequentialSingletons")]
public class PolygonPointsKeyframeTests
{
    private static readonly double[] Widths = { 20, 30, 40 };

    private static PolygonSave Triangle(float dx = 0)
    {
        var poly = new PolygonSave { Name = "P" };
        poly.Points.Add(new Vector2Save { X = dx, Y = 0 });
        poly.Points.Add(new Vector2Save { X = dx + 10, Y = 0 });
        poly.Points.Add(new Vector2Save { X = dx + 10, Y = 10 });
        return poly;
    }

    /// <summary>A chain whose every frame holds one polygon named "P" (1-second frames); point 0 of frame i is at x = i*10.</summary>
    private static (AnimationChainSave Chain, PolygonSave[] Polys) PolyChain(TestServices ctx, int frames)
    {
        var chain = TestHelpers.MakeChain(ctx.Acls, "c", frameCount: frames);
        var polys = new PolygonSave[frames];
        for (int i = 0; i < frames; i++)
        {
            chain.Frames[i].FrameLength = 1f;
            polys[i] = Triangle(i * 10);
            chain.Frames[i].ShapesSave = new ShapesSave();
            chain.Frames[i].ShapesSave!.Add(polys[i]);
        }
        return (chain, polys);
    }

    /// <summary>Makes every polygon's points identical, so the Points group has nothing that differs.</summary>
    private static void MakeIdentical(PolygonSave[] polys)
    {
        foreach (var p in polys)
            for (int i = 0; i < p.Points.Count; i++)
                p.Points[i].X = polys[0].Points[i].X;
    }

    private static KeyframeGroupState State(AnimationChainSave chain, params ShapeSave[] shapes) =>
        KeyframeInspectorState.ForShapes(ShapeKeyframeGroup.Points, shapes, _ => chain);

    // ---- Inspector state -----------------------------------------------------------------------

    [Fact]
    public void HasGroup_Points_IsPolygonsOnly()
    {
        Assert.True(KeyframeInspectorState.HasGroup(ShapeKeyframeGroup.Points, new PolygonSave()));
        Assert.False(KeyframeInspectorState.HasGroup(ShapeKeyframeGroup.Points, new AARectSave()));
        Assert.False(KeyframeInspectorState.HasGroup(ShapeKeyframeGroup.Points, new CircleSave()));
        Assert.False(KeyframeInspectorState.HasGroup(ShapeKeyframeGroup.Size, new PolygonSave()));
    }

    [Fact]
    public void State_FirstAppearance_IsAlwaysAKeyframe_AndCannotBeToggled()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var (chain, polys) = PolyChain(ctx, 2);
        polys[0].SkipPointsKeyframe = true;

        var state = State(chain, polys[0]);

        Assert.True(state.IsKeyframe);
        Assert.False(state.CanToggle);
    }

    [Fact]
    public void State_LaterAppearance_FollowsThePointsSkipFlag_NotTheOffsetFlag()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var (chain, polys) = PolyChain(ctx, 3);
        polys[1].SkipOffsetKeyframe = true;

        Assert.True(State(chain, polys[1]).IsKeyframe);

        polys[1].SkipPointsKeyframe = true;
        Assert.False(State(chain, polys[1]).IsKeyframe);
        Assert.True(State(chain, polys[1]).CanToggle);
    }

    [Fact]
    public void State_ReadsTheStoredPointsCurve()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var (chain, polys) = PolyChain(ctx, 2);
        polys[0].PointsInterpolation = "Back";
        polys[0].PointsEasing = "Out";

        Assert.Equal(KeyframeCurveChoice.Overshoot, State(chain, polys[0]).Curve);
    }

    [Fact]
    public void DisplayShapePoints_KeyframeOff_ShowsTheValueInEffectAtTheStartOfTheFrame()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var (chain, polys) = PolyChain(ctx, 3);
        polys[0].PointsInterpolation = "Linear";
        polys[1].SkipPointsKeyframe = true;
        polys[1].Points[0].X = 999;

        var shown = KeyframeInspectorState.DisplayShapePoints(polys[1], chain);

        Assert.Equal(10f, shown[0].X, 3); // half way from x=0 (frame 0) to x=20 (frame 2)
    }

    [Fact]
    public void DisplayShapePoints_KeyframeOn_ShowsItsOwnPoints()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var (chain, polys) = PolyChain(ctx, 3);
        polys[0].PointsInterpolation = "Linear";

        var shown = KeyframeInspectorState.DisplayShapePoints(polys[1], chain);

        Assert.Equal(10f, shown[0].X, 3);
        Assert.Equal(3, shown.Count);
    }

    // ---- Commands ------------------------------------------------------------------------------

    [Fact]
    public void SetShapeKeyframe_Points_Off_WritesOnlyThePointsFlag_AndUndoRestoresIt()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var (_, polys) = PolyChain(ctx, 3);

        ctx.AppCommands.SetShapeKeyframe(new[] { polys[1] }, ShapeKeyframeGroup.Points, false);

        Assert.True(polys[1].SkipPointsKeyframe);
        Assert.False(polys[1].SkipOffsetKeyframe);
        Assert.Equal("Disable Shape Points Keyframe", ctx.UndoManager.UndoHistory.Single().Description);

        ctx.UndoManager.Undo();
        Assert.False(polys[1].SkipPointsKeyframe);
    }

    [Fact]
    public void SetShapeKeyframe_Points_FirstAppearance_IsNeverWritten()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var (_, polys) = PolyChain(ctx, 2);

        ctx.AppCommands.SetShapeKeyframe(new[] { polys[0] }, ShapeKeyframeGroup.Points, false);

        Assert.False(polys[0].SkipPointsKeyframe);
        Assert.False(ctx.UndoManager.CanUndo);
    }

    [Fact]
    public void SetShapeKeyframe_Points_OnARectangle_IsIgnored()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "c", frameCount: 2);
        var rects = new[] { new AARectSave { Name = "R" }, new AARectSave { Name = "R" } };
        for (int i = 0; i < 2; i++)
        {
            chain.Frames[i].ShapesSave = new ShapesSave();
            chain.Frames[i].ShapesSave!.Add(rects[i]);
        }

        ctx.AppCommands.SetShapeKeyframe(new[] { rects[1] }, ShapeKeyframeGroup.Points, false);

        Assert.False(ctx.UndoManager.CanUndo);
    }

    [Fact]
    public void SetShapeKeyframeCurve_Points_WritesTheFixedPair_ToThePointsGroupOnly_AndUndoRestoresIt()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var (_, polys) = PolyChain(ctx, 2);

        ctx.AppCommands.SetShapeKeyframeCurve(new[] { polys[1] }, ShapeKeyframeGroup.Points, KeyframeCurveChoice.Overshoot);

        Assert.Equal("Back", polys[1].PointsInterpolation);
        Assert.Equal("Out", polys[1].PointsEasing);
        Assert.Null(polys[1].OffsetInterpolation);
        Assert.Equal("Set Shape Points Curve: Overshoot", ctx.UndoManager.UndoHistory.Single().Description);

        ctx.UndoManager.Undo();
        Assert.Null(polys[1].PointsInterpolation);
        Assert.Null(polys[1].PointsEasing);
    }

    [Fact]
    public void SetShapeKeyframeCurve_Points_SkipsShapesThatAreNotPointsKeyframes()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var (_, polys) = PolyChain(ctx, 3);
        polys[2].SkipPointsKeyframe = true;
        polys[2].PointsInterpolation = "Cubic";

        ctx.AppCommands.SetShapeKeyframeCurve(polys, ShapeKeyframeGroup.Points, KeyframeCurveChoice.Bounce);

        Assert.Equal("Bounce", polys[0].PointsInterpolation);
        Assert.Equal("Bounce", polys[1].PointsInterpolation);
        Assert.Equal("Cubic", polys[2].PointsInterpolation);
    }

    [Fact]
    public void UndoingAnOffsetEdit_NeverTouchesTheStoredPointsCurve()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var (_, polys) = PolyChain(ctx, 2);
        polys[1].PointsInterpolation = "Wobble";
        polys[1].PointsEasing = "Sideways";
        polys[1].SkipPointsKeyframe = true;

        ctx.AppCommands.SetPolygonPropsBulk(new[] { polys[1] }, null, NumericEdit.Set(5m), null);
        ctx.UndoManager.Undo();

        Assert.Equal("Wobble", polys[1].PointsInterpolation);
        Assert.Equal("Sideways", polys[1].PointsEasing);
        Assert.True(polys[1].SkipPointsKeyframe);
    }

    [Fact]
    public void LockedChain_PointsKeyframeEditsAreSkipped()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var (chain, polys) = PolyChain(ctx, 2);
        chain.IsLocked = true;

        ctx.AppCommands.SetShapeKeyframe(new[] { polys[1] }, ShapeKeyframeGroup.Points, false);
        ctx.AppCommands.SetShapeKeyframeCurve(new[] { polys[1] }, ShapeKeyframeGroup.Points, KeyframeCurveChoice.Linear);

        Assert.False(polys[1].SkipPointsKeyframe);
        Assert.Null(polys[1].PointsInterpolation);
    }

    // ---- Sampling and cloning ------------------------------------------------------------------

    [Fact]
    public void SampleShapePoints_GlidesByTheStoredCurve()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var (chain, polys) = PolyChain(ctx, 2);
        polys[0].PointsInterpolation = "Linear";

        var points = KeyframeSampler.SampleShapePoints(chain, 0, 0.5, polys[0]);

        Assert.Equal(5f, points[0].X, 3);
        Assert.Equal(15f, points[1].X, 3);
    }

    [Fact]
    public void IsAnimated_TrueWhenOnlyAPolygonHasAPointsCurve()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var (chain, polys) = PolyChain(ctx, 2);
        Assert.False(KeyframeSampler.IsAnimated(chain));

        polys[0].PointsInterpolation = "Linear";

        Assert.True(KeyframeSampler.IsAnimated(chain));
    }

    [Fact]
    public void CloneShape_CopiesEveryPointsKeyframeField()
    {
        var poly = Triangle();
        poly.SkipPointsKeyframe = true;
        poly.PointsInterpolation = "Odd";
        poly.PointsEasing = "Odder";

        var copy = (PolygonSave)AnimationCloneHelper.CloneShape(poly)!;

        Assert.True(copy.SkipPointsKeyframe);
        Assert.Equal("Odd", copy.PointsInterpolation);
        Assert.Equal("Odder", copy.PointsEasing);
        Assert.Equal(3, copy.Points.Count);
    }

    // ---- Timeline lanes ------------------------------------------------------------------------

    private static KeyframeLane? Points(AnimationChainSave chain, ShapeSave shape, double[]? widths = null) =>
        KeyframeLaneBuilder.BuildShape(chain, shape, ShapeKeyframeGroup.Points, widths ?? Widths);

    [Fact]
    public void Lane_PointsDiffer_ShowsWithADiamondOnEveryKeyframe()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var (chain, polys) = PolyChain(ctx, 3);

        var lane = Points(chain, polys[0])!;

        Assert.Equal(new double[] { 0, 20, 50 }, lane.KeyframeXs);
    }

    [Fact]
    public void Lane_PointsIdenticalAndNothingGlides_ReturnsNull()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var (chain, polys) = PolyChain(ctx, 3);
        MakeIdentical(polys);

        Assert.Null(Points(chain, polys[0]));
    }

    [Fact]
    public void Lane_AGlidingSpanShowsTheLane_EvenIfEveryPointIsTheSame()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var (chain, polys) = PolyChain(ctx, 3);
        MakeIdentical(polys);
        polys[1].PointsInterpolation = "Linear";

        Assert.NotNull(Points(chain, polys[0]));
    }

    [Fact]
    public void Lane_ChangedPointCount_CountsAsADifference()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var (chain, polys) = PolyChain(ctx, 3);
        MakeIdentical(polys);
        polys[2].Points.Add(new Vector2Save { X = 0, Y = 10 });

        Assert.NotNull(Points(chain, polys[0]));
    }

    [Fact]
    public void Lane_SpanBetweenDifferentPointCounts_IsDrawnAsSnap_BecausePlaybackSnapsThere()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var (chain, polys) = PolyChain(ctx, 3);
        polys[0].PointsInterpolation = "Linear";
        polys[1].PointsInterpolation = "Linear";
        polys[2].Points.Add(new Vector2Save { X = 0, Y = 10 });

        var lane = Points(chain, polys[0])!;

        Assert.False(lane.Spans[0].Curve.IsSnap);  // 3 -> 3 points glides
        Assert.True(lane.Spans[1].Curve.IsSnap);   // 3 -> 4 points snaps
    }

    [Fact]
    public void Lane_GroupsAreIndependent_OffsetInUseDoesNotMakePointsInUse()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var (chain, polys) = PolyChain(ctx, 3);
        MakeIdentical(polys);
        for (int i = 0; i < 3; i++) polys[i].X = i * 5;

        Assert.NotNull(KeyframeLaneBuilder.BuildShape(chain, polys[0], ShapeKeyframeGroup.Offset, Widths));
        Assert.Null(Points(chain, polys[0]));
    }

    [Fact]
    public void Lane_SkippedPointsFrame_HasNoDiamond_AndTheSpanRunsThroughIt()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var (chain, polys) = PolyChain(ctx, 3);
        polys[1].SkipPointsKeyframe = true;

        var lane = Points(chain, polys[0])!;

        Assert.Equal(new double[] { 0, 50 }, lane.KeyframeXs);
        Assert.Equal(50, lane.Spans[0].EndX);
    }

    [Fact]
    public void Lane_ARectangleHasNoPointsLane()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "c", frameCount: 3);
        var rects = new AARectSave[3];
        for (int i = 0; i < 3; i++)
        {
            chain.Frames[i].ShapesSave = new ShapesSave();
            rects[i] = new AARectSave { Name = "R", X = i * 10 };
            chain.Frames[i].ShapesSave!.Add(rects[i]);
        }

        Assert.Null(Points(chain, rects[0]));
    }

    [Fact]
    public void Plan_OnePolygonSelected_ShowsItsOffsetAndPointsLanes()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var (chain, polys) = PolyChain(ctx, 3);
        for (int i = 0; i < 3; i++) polys[i].X = i * 5;

        var plan = TimelineLanePlanner.Build(chain, new object[] { polys[1] }, Widths);

        Assert.Equal("P Offset", plan.FirstLabel);
        Assert.Equal("P Points", plan.SecondLabel);
        Assert.NotNull(plan.First);
        Assert.Equal(new double[] { 0, 20, 50 }, plan.Second!.KeyframeXs);
    }

    [Fact]
    public void Plan_OneRectangleSelected_StillShowsOffsetAndSize()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "c", frameCount: 3);
        var rects = new AARectSave[3];
        for (int i = 0; i < 3; i++)
        {
            chain.Frames[i].ShapesSave = new ShapesSave();
            rects[i] = new AARectSave { Name = "R", X = i * 10, ScaleX = 4 + i };
            chain.Frames[i].ShapesSave!.Add(rects[i]);
        }

        var plan = TimelineLanePlanner.Build(chain, new object[] { rects[1] }, Widths);

        Assert.Equal("R Offset", plan.FirstLabel);
        Assert.Equal("R Size", plan.SecondLabel);
    }

    [Fact]
    public void VertexCount_OfADisplayedList_HidesTheRepeatedClosingPoint()
    {
        var open = new[] { new System.Numerics.Vector2(0, 0), new(1, 0), new(1, 1) };
        var closed = new[] { new System.Numerics.Vector2(0, 0), new(1, 0), new(1, 1), new(0, 0) };

        Assert.Equal(3, PolygonVertices.Count(open));
        Assert.Equal(3, PolygonVertices.Count(closed));
    }
}
