using AnimationEditor.Core.IO;
using AnimationEditor.Core.Keyframes;
using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>Shape Offset and Size keyframes in the editor: inspector state, commands, sampling (#1478).</summary>
[Collection("SequentialSingletons")]
public class ShapeKeyframeTests
{
    /// <summary>A chain whose every frame holds one rectangle named "A" (1-second frames).</summary>
    private static (AnimationChainSave Chain, AARectSave[] Rects) RectChain(TestServices ctx, int frames)
    {
        var chain = TestHelpers.MakeChain(ctx.Acls, "c", frameCount: frames);
        var rects = new AARectSave[frames];
        for (int i = 0; i < frames; i++)
        {
            chain.Frames[i].FrameLength = 1f;
            rects[i] = new AARectSave { Name = "A", X = i * 10, ScaleX = 8, ScaleY = 8 };
            chain.Frames[i].ShapesSave = new ShapesSave();
            chain.Frames[i].ShapesSave!.Add(rects[i]);
        }
        return (chain, rects);
    }

    private static KeyframeGroupState State(ShapeKeyframeGroup group, AnimationChainSave chain, params ShapeSave[] shapes) =>
        KeyframeInspectorState.ForShapes(group, shapes, _ => chain);

    // ---- Inspector state -----------------------------------------------------------------------

    [Fact]
    public void State_FirstAppearance_IsAlwaysAKeyframe_AndCannotBeToggled()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var (chain, rects) = RectChain(ctx, 2);
        rects[0].SkipOffsetKeyframe = true;

        var state = State(ShapeKeyframeGroup.Offset, chain, rects[0]);

        Assert.True(state.IsKeyframe);
        Assert.False(state.CanToggle);
    }

    [Fact]
    public void State_LaterAppearance_FollowsTheSkipFlag_PerGroup()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var (chain, rects) = RectChain(ctx, 3);
        rects[1].SkipOffsetKeyframe = true;

        Assert.False(State(ShapeKeyframeGroup.Offset, chain, rects[1]).IsKeyframe);
        Assert.True(State(ShapeKeyframeGroup.Size, chain, rects[1]).IsKeyframe);
        Assert.True(State(ShapeKeyframeGroup.Offset, chain, rects[1]).CanToggle);
    }

    [Fact]
    public void State_ShapeAfterANameGap_StartsANewRun_AndCannotBeToggled()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var (chain, rects) = RectChain(ctx, 3);
        chain.Frames[1].ShapesSave!.Remove(rects[1]);
        rects[2].SkipOffsetKeyframe = true;

        var state = State(ShapeKeyframeGroup.Offset, chain, rects[2]);

        Assert.True(state.IsKeyframe);
        Assert.False(state.CanToggle);
    }

    [Fact]
    public void State_KeyframeOff_HidesCurves_AndMakesFieldsReadOnly()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var (chain, rects) = RectChain(ctx, 2);
        rects[1].SkipSizeKeyframe = true;

        var state = State(ShapeKeyframeGroup.Size, chain, rects[1]);

        Assert.False(state.CurvesVisible);
        Assert.False(state.FieldsEditable);
    }

    [Fact]
    public void State_CurveOutsideTheEight_ShowsNothingSelected_WithTheStoredNameAsNote()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var (chain, rects) = RectChain(ctx, 2);
        rects[1].OffsetInterpolation = "Cubic";
        rects[1].OffsetEasing = "In";

        var state = State(ShapeKeyframeGroup.Offset, chain, rects[1]);

        Assert.Null(state.Curve);
        Assert.Equal("Cubic In", state.CurveNote);
    }

    [Fact]
    public void State_StoredQuadraticOut_SelectsEaseOut()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var (chain, rects) = RectChain(ctx, 2);
        rects[0].SizeInterpolation = "Quadratic";
        rects[0].SizeEasing = "Out";

        Assert.Equal(KeyframeCurveChoice.EaseOut, State(ShapeKeyframeGroup.Size, chain, rects[0]).Curve);
    }

    [Fact]
    public void State_MixedSelection_IsNull()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var (chain, rects) = RectChain(ctx, 3);
        rects[2].SkipOffsetKeyframe = true;

        var state = State(ShapeKeyframeGroup.Offset, chain, rects[1], rects[2]);

        Assert.Null(state.IsKeyframe);
        Assert.True(state.CurvesVisible);
    }

    [Fact]
    public void DisplayShapeOffset_KeyframeOff_ShowsTheValueInEffectAtTheStartOfTheFrame()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var (chain, rects) = RectChain(ctx, 3);
        rects[0].X = 0; rects[0].OffsetInterpolation = "Linear";
        rects[1].X = 999; rects[1].SkipOffsetKeyframe = true;
        rects[2].X = 100;

        var shown = KeyframeInspectorState.DisplayShapeOffset(rects[1], chain);

        Assert.Equal(50f, shown.X, 3); // halfway through the 2-second glide
    }

    [Fact]
    public void DisplayShapeSize_Keyframe_ShowsItsOwnStoredSize()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var (chain, rects) = RectChain(ctx, 2);
        rects[1].ScaleX = 33; rects[1].ScaleY = 44;

        var shown = KeyframeInspectorState.DisplayShapeSize(rects[1], chain);

        Assert.Equal((33f, 44f), shown);
    }

    // ---- Commands ------------------------------------------------------------------------------

    [Fact]
    public void SetShapeKeyframe_Off_WritesOnlyThatGroupsFlag_AndUndoRestoresIt()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var (_, rects) = RectChain(ctx, 3);

        ctx.AppCommands.SetShapeKeyframe(new[] { rects[1] }, ShapeKeyframeGroup.Offset, false);

        Assert.True(rects[1].SkipOffsetKeyframe);
        Assert.False(rects[1].SkipSizeKeyframe);
        Assert.Equal("Disable Shape Offset Keyframe", ctx.UndoManager.UndoHistory.Single().Description);

        ctx.UndoManager.Undo();
        Assert.False(rects[1].SkipOffsetKeyframe);
    }

    [Fact]
    public void SetShapeKeyframe_Size_OnCircle_WritesTheSizeFlag()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "c", frameCount: 2);
        var circles = new[] { new CircleSave { Name = "C" }, new CircleSave { Name = "C" } };
        for (int i = 0; i < 2; i++)
        {
            chain.Frames[i].ShapesSave = new ShapesSave();
            chain.Frames[i].ShapesSave!.Add(circles[i]);
        }

        ctx.AppCommands.SetShapeKeyframe(new[] { circles[1] }, ShapeKeyframeGroup.Size, false);

        Assert.True(circles[1].SkipSizeKeyframe);
        Assert.Equal("Disable Shape Size Keyframe", ctx.UndoManager.UndoHistory.Single().Description);
    }

    [Fact]
    public void SetShapeKeyframe_FirstAppearance_IsNeverWritten()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var (_, rects) = RectChain(ctx, 2);

        ctx.AppCommands.SetShapeKeyframe(new[] { rects[0] }, ShapeKeyframeGroup.Offset, false);

        Assert.False(rects[0].SkipOffsetKeyframe);
        Assert.False(ctx.UndoManager.CanUndo);
    }

    [Fact]
    public void SetShapeKeyframe_MultiSelect_IsOneUndoStep()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var (_, rects) = RectChain(ctx, 3);

        ctx.AppCommands.SetShapeKeyframe(rects, ShapeKeyframeGroup.Size, false);

        Assert.False(rects[0].SkipSizeKeyframe);
        Assert.True(rects[1].SkipSizeKeyframe);
        Assert.True(rects[2].SkipSizeKeyframe);
        Assert.Single(ctx.UndoManager.UndoHistory);
    }

    [Fact]
    public void SetShapeKeyframe_DirectlyAfterAFieldEdit_DoesNotMergeIntoThatUndoStep()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var (_, rects) = RectChain(ctx, 2);

        ctx.AppCommands.SetRectPropsBulk(new[] { rects[1] }, null, NumericEdit.Set(5m), null, null, null);
        ctx.AppCommands.SetShapeKeyframe(new[] { rects[1] }, ShapeKeyframeGroup.Offset, false);

        Assert.Equal(2, ctx.UndoManager.UndoHistory.Count);
    }

    [Fact]
    public void SetShapeKeyframe_Size_OnPolygon_IsIgnored()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "c", frameCount: 2);
        var polys = new[] { new PolygonSave { Name = "P" }, new PolygonSave { Name = "P" } };
        for (int i = 0; i < 2; i++)
        {
            chain.Frames[i].ShapesSave = new ShapesSave();
            chain.Frames[i].ShapesSave!.Add(polys[i]);
        }

        ctx.AppCommands.SetShapeKeyframe(new[] { polys[1] }, ShapeKeyframeGroup.Size, false);

        Assert.False(ctx.UndoManager.CanUndo);
    }

    [Fact]
    public void SetShapeKeyframe_KeepsTheStoredCurveWhileOff()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var (_, rects) = RectChain(ctx, 2);
        rects[1].OffsetInterpolation = "Cubic";
        rects[1].OffsetEasing = "In";

        ctx.AppCommands.SetShapeKeyframe(new[] { rects[1] }, ShapeKeyframeGroup.Offset, false);
        ctx.AppCommands.SetShapeKeyframe(new[] { rects[1] }, ShapeKeyframeGroup.Offset, true);

        Assert.Equal("Cubic", rects[1].OffsetInterpolation);
        Assert.Equal("In", rects[1].OffsetEasing);
    }

    [Fact]
    public void SetShapeKeyframeCurve_WritesTheFixedPair_ToTheChosenGroupOnly()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var (_, rects) = RectChain(ctx, 2);

        ctx.AppCommands.SetShapeKeyframeCurve(new[] { rects[1] }, ShapeKeyframeGroup.Size, KeyframeCurveChoice.Overshoot);

        Assert.Equal("Back", rects[1].SizeInterpolation);
        Assert.Equal("Out", rects[1].SizeEasing);
        Assert.Null(rects[1].OffsetInterpolation);
        Assert.Equal("Set Shape Size Curve: Overshoot", ctx.UndoManager.UndoHistory.Single().Description);

        ctx.UndoManager.Undo();
        Assert.Null(rects[1].SizeInterpolation);
        Assert.Null(rects[1].SizeEasing);
    }

    [Fact]
    public void SetShapeKeyframeCurve_SkipsShapesThatAreNotKeyframes()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var (_, rects) = RectChain(ctx, 3);
        rects[2].SkipOffsetKeyframe = true;
        rects[2].OffsetInterpolation = "Cubic";

        ctx.AppCommands.SetShapeKeyframeCurve(rects, ShapeKeyframeGroup.Offset, KeyframeCurveChoice.Bounce);

        Assert.Equal("Bounce", rects[0].OffsetInterpolation);
        Assert.Equal("Bounce", rects[1].OffsetInterpolation);
        Assert.Equal("Cubic", rects[2].OffsetInterpolation);
        Assert.Single(ctx.UndoManager.UndoHistory);
    }

    [Fact]
    public void LockedChain_ShapeKeyframeEditsAreSkipped()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var (chain, rects) = RectChain(ctx, 2);
        chain.IsLocked = true;

        ctx.AppCommands.SetShapeKeyframe(new[] { rects[1] }, ShapeKeyframeGroup.Offset, false);
        ctx.AppCommands.SetShapeKeyframeCurve(new[] { rects[1] }, ShapeKeyframeGroup.Offset, KeyframeCurveChoice.Linear);

        Assert.False(rects[1].SkipOffsetKeyframe);
        Assert.Null(rects[1].OffsetInterpolation);
    }

    [Fact]
    public void EditingShapeFields_NeverNormalizesAStoredCurve()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var (_, rects) = RectChain(ctx, 2);
        rects[1].OffsetInterpolation = "Wobble";
        rects[1].OffsetEasing = "Sideways";
        rects[1].SizeInterpolation = "Cubic";

        ctx.AppCommands.SetRectPropsBulk(new[] { rects[1] }, null, NumericEdit.Set(5m), null, NumericEdit.Set(9m), null);
        ctx.UndoManager.Undo();

        Assert.Equal("Wobble", rects[1].OffsetInterpolation);
        Assert.Equal("Sideways", rects[1].OffsetEasing);
        Assert.Equal("Cubic", rects[1].SizeInterpolation);
    }

    // ---- Sampling and cloning ------------------------------------------------------------------

    [Fact]
    public void SampleShape_GlidesByTheStoredCurves()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var (chain, rects) = RectChain(ctx, 2);
        rects[0].X = 0; rects[0].OffsetInterpolation = "Linear";
        rects[1].X = 100;

        var value = KeyframeSampler.SampleShape(chain, 0, 0.5, rects[0]);

        Assert.Equal(50f, value.X, 3);
    }

    [Fact]
    public void IsAnimated_TrueWhenOnlyAShapeHasACurve()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var (chain, rects) = RectChain(ctx, 2);
        Assert.False(KeyframeSampler.IsAnimated(chain));

        rects[0].SizeInterpolation = "Linear";

        Assert.True(KeyframeSampler.IsAnimated(chain));
    }

    [Fact]
    public void CloneShape_CopiesEveryKeyframeField()
    {
        var rect = new AARectSave
        {
            Name = "R", SkipOffsetKeyframe = true, OffsetInterpolation = "Cubic", OffsetEasing = "InOut",
            SkipSizeKeyframe = true, SizeInterpolation = "Odd", SizeEasing = "Odder",
        };
        var circle = new CircleSave { Name = "C", SizeInterpolation = "Linear", OffsetEasing = "In" };
        var poly = new PolygonSave { Name = "P", SkipOffsetKeyframe = true, OffsetInterpolation = "Back" };

        var r = (AARectSave)AnimationCloneHelper.CloneShape(rect)!;
        var c = (CircleSave)AnimationCloneHelper.CloneShape(circle)!;
        var p = (PolygonSave)AnimationCloneHelper.CloneShape(poly)!;

        Assert.True(r.SkipOffsetKeyframe);
        Assert.Equal("Cubic", r.OffsetInterpolation);
        Assert.Equal("InOut", r.OffsetEasing);
        Assert.True(r.SkipSizeKeyframe);
        Assert.Equal("Odd", r.SizeInterpolation);
        Assert.Equal("Odder", r.SizeEasing);
        Assert.Equal("Linear", c.SizeInterpolation);
        Assert.Equal("In", c.OffsetEasing);
        Assert.True(p.SkipOffsetKeyframe);
        Assert.Equal("Back", p.OffsetInterpolation);
    }
}
