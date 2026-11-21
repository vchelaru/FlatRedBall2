using AnimationEditor.Core.Keyframes;
using FlatRedBall2.Animation;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Editing.Tests;

public class FrameDetailEditTests : IDisposable
{
    private readonly EditingTestGraph _g = new();
    private readonly AnimationChainListSave _acls = new();
    private readonly AnimationChainSave _walk = new() { Name = "Walk" };
    private readonly AARectSave _rect2 = new() { Name = "B", ScaleX = 1, ScaleY = 1 };
    private readonly AARectSave _rect3 = new() { Name = "C", ScaleX = 1, ScaleY = 1 };
    private readonly CircleSave _circle = new() { Name = "Foot", Radius = 2 };
    private readonly PolygonSave _poly = new() { Name = "Poly" };

    public FrameDetailEditTests()
    {
        for (int i = 0; i < 3; i++)
            _walk.Frames.Add(new AnimationFrameSave { TextureName = "a.png", FrameLength = 0.1f });
        foreach (var f in _walk.Frames)
        {
            f.ShapesSave = new ShapesSave();
            // Same name on every frame so rect "A" forms one run.
            f.ShapesSave.Add(new AARectSave { Name = "A", ScaleX = 1, ScaleY = 1 });
        }
        var shapes = _walk.Frames[1].ShapesSave!;
        shapes.Add(_rect2);
        shapes.Add(_rect3);
        shapes.Add(_circle);
        foreach (var (x, y) in new[] { (0f, 0f), (4f, 0f), (4f, 4f) })
            _poly.Points.Add(new Vector2Save { X = x, Y = y });
        shapes.Add(_poly);
        _acls.AnimationChains.Add(_walk);
        _g.ProjectManager.AnimationChainListSave = _acls;
        _g.ProjectManager.FileName = Path.Combine(_g.Root, "hero.achx");
    }

    public void Dispose() => _g.Dispose();

    private AnimationFrameSave F(int i) => _walk.Frames[i];

    private int UndoDepth()
    {
        int n = 0;
        while (_g.Undo.CanUndo) { _g.Undo.Undo(); n++; }
        return n;
    }

    // ---- events ----

    [Fact]
    public void AddEvent_AppendsWithData_AddressedByIndex_OneUndoStepEach()
    {
        FrameDetailEdit.AddFrameEvent(_g.Commands, _walk, F(1), "Step", null).Success.ShouldBeTrue();

        var r = FrameDetailEdit.AddFrameEvent(_g.Commands, _walk, F(1), "Hit", "heavy");

        r.Success.ShouldBeTrue();
        r.ChangedItems.ShouldBe(["Walk/1/event/1"]);
        F(1).Events.Select(e => (e.Name, e.Data)).ShouldBe([("Step", (string?)null), ("Hit", "heavy")]);
        UndoDepth().ShouldBe(2);
        F(1).Events.ShouldBeEmpty();
    }

    [Fact]
    public void AddEvent_EmptyName_Fails()
    {
        var r = FrameDetailEdit.AddFrameEvent(_g.Commands, _walk, F(0), "  ", null);

        r.Success.ShouldBeFalse();
        r.HasError(EditErrorCode.EmptyName).ShouldBeTrue();
        F(0).Events.ShouldBeEmpty();
    }

    [Fact]
    public void SetEvent_ChangesNameAndData_OmittedPartKept_EmptyDataClears()
    {
        FrameDetailEdit.AddFrameEvent(_g.Commands, _walk, F(0), "Step", "left");

        FrameDetailEdit.SetFrameEvent(_g.Commands, _walk, F(0), 0, "Land", null).Success.ShouldBeTrue();
        F(0).Events[0].Name.ShouldBe("Land");
        F(0).Events[0].Data.ShouldBe("left");

        var r = FrameDetailEdit.SetFrameEvent(_g.Commands, _walk, F(0), 0, null, "");
        r.ChangedItems.ShouldBe(["Walk/0/event/0"]);
        F(0).Events[0].Data.ShouldBeNull();
    }

    [Fact]
    public void SetEvent_SameValues_NoOp_NoUndoStep()
    {
        FrameDetailEdit.AddFrameEvent(_g.Commands, _walk, F(0), "Step", "left");

        var r = FrameDetailEdit.SetFrameEvent(_g.Commands, _walk, F(0), 0, "Step", "left");

        r.Success.ShouldBeTrue();
        r.ChangedItems.ShouldBeEmpty();
        UndoDepth().ShouldBe(1);
    }

    [Fact]
    public void SetEvent_NeedsNameOrData_AndIndexInRange()
    {
        FrameDetailEdit.AddFrameEvent(_g.Commands, _walk, F(0), "Step", null);

        FrameDetailEdit.SetFrameEvent(_g.Commands, _walk, F(0), 0, null, null).Success.ShouldBeFalse();
        FrameDetailEdit.SetFrameEvent(_g.Commands, _walk, F(0), 0, " ", null).HasError(EditErrorCode.EmptyName).ShouldBeTrue();
        FrameDetailEdit.SetFrameEvent(_g.Commands, _walk, F(0), 5, "X", null).HasError(EditErrorCode.IndexOutOfRange).ShouldBeTrue();
    }

    [Fact]
    public void DeleteEvent_RemovesIt_OutOfRangeFails()
    {
        FrameDetailEdit.AddFrameEvent(_g.Commands, _walk, F(0), "A", null);
        FrameDetailEdit.AddFrameEvent(_g.Commands, _walk, F(0), "B", null);

        var r = FrameDetailEdit.DeleteFrameEvent(_g.Commands, _walk, F(0), 0);

        r.Success.ShouldBeTrue();
        r.ChangedItems.ShouldBe(["Walk/0/event/0"]);
        F(0).Events.Select(e => e.Name).ShouldBe(["B"]);
        FrameDetailEdit.DeleteFrameEvent(_g.Commands, _walk, F(0), 3).HasError(EditErrorCode.IndexOutOfRange).ShouldBeTrue();
    }

    [Fact]
    public void EventOps_OnLockedChain_FailLocked_AndChangeNothing()
    {
        FrameDetailEdit.AddFrameEvent(_g.Commands, _walk, F(0), "A", null);
        _walk.IsLocked = true;

        FrameDetailEdit.AddFrameEvent(_g.Commands, _walk, F(0), "B", null).HasError(EditErrorCode.Locked).ShouldBeTrue();
        FrameDetailEdit.SetFrameEvent(_g.Commands, _walk, F(0), 0, "Z", null).HasError(EditErrorCode.Locked).ShouldBeTrue();
        FrameDetailEdit.DeleteFrameEvent(_g.Commands, _walk, F(0), 0).HasError(EditErrorCode.Locked).ShouldBeTrue();
        F(0).Events.Select(e => e.Name).ShouldBe(["A"]);
    }

    [Fact]
    public void EventOps_DoNotTouchSelection()
    {
        FrameDetailEdit.AddFrameEvent(_g.Commands, _walk, F(0), "A", null);

        _g.SelectedState.SelectedFrame.ShouldBeNull();
        _g.SelectedState.SelectedChain.ShouldBeNull();
    }

    // ---- frame keyframes ----

    [Fact]
    public void FrameKeyframe_Off_ThenOn_OneStepEach_NoOpWhenAlreadySo()
    {
        var off = FrameDetailEdit.SetFrameKeyframe(_g.Commands, _g.ObjectFinder, _walk, F(1), KeyframeGroup.Offset, false);

        off.ChangedItems.ShouldBe(["Walk/1"]);
        F(1).SkipOffsetKeyframe.ShouldBeTrue();
        FrameDetailEdit.SetFrameKeyframe(_g.Commands, _g.ObjectFinder, _walk, F(1), KeyframeGroup.Offset, false)
            .ChangedItems.ShouldBeEmpty();
        FrameDetailEdit.SetFrameKeyframe(_g.Commands, _g.ObjectFinder, _walk, F(1), KeyframeGroup.Offset, true).Success.ShouldBeTrue();
        F(1).SkipOffsetKeyframe.ShouldBeFalse();
        UndoDepth().ShouldBe(2);
    }

    [Fact]
    public void FrameKeyframe_ColorGroup_SetsColorFlag()
    {
        FrameDetailEdit.SetFrameKeyframe(_g.Commands, _g.ObjectFinder, _walk, F(2), KeyframeGroup.Color, false).Success.ShouldBeTrue();

        F(2).SkipColorKeyframe.ShouldBeTrue();
        F(2).SkipOffsetKeyframe.ShouldBeFalse();
    }

    [Fact]
    public void FrameKeyframe_FirstFrame_CannotBeTurnedOff_ButOnIsNoOp()
    {
        FrameDetailEdit.SetFrameKeyframe(_g.Commands, _g.ObjectFinder, _walk, F(0), KeyframeGroup.Offset, false)
            .Success.ShouldBeFalse();
        var on = FrameDetailEdit.SetFrameKeyframe(_g.Commands, _g.ObjectFinder, _walk, F(0), KeyframeGroup.Offset, true);

        on.Success.ShouldBeTrue();
        on.ChangedItems.ShouldBeEmpty();
        F(0).SkipOffsetKeyframe.ShouldBeFalse();
    }

    [Fact]
    public void FrameKeyframeCurve_StoresPickerPair_OneStep_NoOpWhenSame()
    {
        var r = FrameDetailEdit.SetFrameKeyframeCurve(_g.Commands, _g.ObjectFinder, _walk, F(1), KeyframeGroup.Offset, KeyframeCurveChoice.EaseIn);

        r.ChangedItems.ShouldBe(["Walk/1"]);
        (F(1).OffsetInterpolation, F(1).OffsetEasing).ShouldBe(("Quadratic", "In"));
        FrameDetailEdit.SetFrameKeyframeCurve(_g.Commands, _g.ObjectFinder, _walk, F(1), KeyframeGroup.Offset, KeyframeCurveChoice.EaseIn)
            .ChangedItems.ShouldBeEmpty();
        UndoDepth().ShouldBe(1);
    }

    [Fact]
    public void FrameKeyframeCurve_Snap_ClearsStoredPair()
    {
        FrameDetailEdit.SetFrameKeyframeCurve(_g.Commands, _g.ObjectFinder, _walk, F(1), KeyframeGroup.Color, KeyframeCurveChoice.Bounce);

        FrameDetailEdit.SetFrameKeyframeCurve(_g.Commands, _g.ObjectFinder, _walk, F(1), KeyframeGroup.Color, KeyframeCurveChoice.Snap);

        F(1).ColorInterpolation.ShouldBeNull();
        F(1).ColorEasing.ShouldBeNull();
    }

    [Fact]
    public void FrameKeyframeCurve_OnNonKeyframe_FailsWithAdvice()
    {
        FrameDetailEdit.SetFrameKeyframe(_g.Commands, _g.ObjectFinder, _walk, F(1), KeyframeGroup.Offset, false);

        var r = FrameDetailEdit.SetFrameKeyframeCurve(_g.Commands, _g.ObjectFinder, _walk, F(1), KeyframeGroup.Offset, KeyframeCurveChoice.Linear);

        r.Success.ShouldBeFalse();
        r.Errors[0].ShouldContain("keyframe");
        F(1).OffsetInterpolation.ShouldBeNull();
    }

    [Fact]
    public void FrameKeyframeOps_OnLockedChain_FailLocked()
    {
        _walk.IsLocked = true;

        FrameDetailEdit.SetFrameKeyframe(_g.Commands, _g.ObjectFinder, _walk, F(1), KeyframeGroup.Offset, false)
            .HasError(EditErrorCode.Locked).ShouldBeTrue();
        FrameDetailEdit.SetFrameKeyframeCurve(_g.Commands, _g.ObjectFinder, _walk, F(1), KeyframeGroup.Offset, KeyframeCurveChoice.Linear)
            .HasError(EditErrorCode.Locked).ShouldBeTrue();
        F(1).SkipOffsetKeyframe.ShouldBeFalse();
    }

    // ---- shape keyframes ----

    [Fact]
    public void ShapeKeyframe_Offset_OffAndOn_AddressedByShape()
    {
        var shape = F(1).ShapesSave!.AARectSaves.First(s => s.Name == "A");

        var r = FrameDetailEdit.SetShapeKeyframe(_g.Commands, _g.ObjectFinder, shape, ShapeKeyframeGroup.Offset, false);

        r.ChangedItems.ShouldBe(["Walk/1/rect/A"]);
        shape.SkipOffsetKeyframe.ShouldBeTrue();
        FrameDetailEdit.SetShapeKeyframe(_g.Commands, _g.ObjectFinder, shape, ShapeKeyframeGroup.Offset, false).ChangedItems.ShouldBeEmpty();
        FrameDetailEdit.SetShapeKeyframe(_g.Commands, _g.ObjectFinder, shape, ShapeKeyframeGroup.Offset, true).Success.ShouldBeTrue();
        shape.SkipOffsetKeyframe.ShouldBeFalse();
        UndoDepth().ShouldBe(2);
    }

    [Fact]
    public void ShapeKeyframe_SizeOnPolygon_AndPointsOnRect_Fail()
    {
        FrameDetailEdit.SetShapeKeyframe(_g.Commands, _g.ObjectFinder, _poly, ShapeKeyframeGroup.Size, false).Success.ShouldBeFalse();
        FrameDetailEdit.SetShapeKeyframe(_g.Commands, _g.ObjectFinder, _rect2, ShapeKeyframeGroup.Points, false).Success.ShouldBeFalse();
        FrameDetailEdit.SetShapeKeyframe(_g.Commands, _g.ObjectFinder, _poly, ShapeKeyframeGroup.Points, false).Success.ShouldBeTrue();
        _poly.SkipPointsKeyframe.ShouldBeTrue();
    }

    [Fact]
    public void ShapeKeyframe_FirstOfRun_CannotBeTurnedOff()
    {
        var first = F(0).ShapesSave!.AARectSaves.Single();

        FrameDetailEdit.SetShapeKeyframe(_g.Commands, _g.ObjectFinder, first, ShapeKeyframeGroup.Size, false).Success.ShouldBeFalse();
        FrameDetailEdit.SetShapeKeyframe(_g.Commands, _g.ObjectFinder, first, ShapeKeyframeGroup.Size, true).ChangedItems.ShouldBeEmpty();
    }

    [Fact]
    public void ShapeKeyframeCurve_StoresPair_NoOpWhenSame_FailsOnNonKeyframe()
    {
        var shape = F(2).ShapesSave!.AARectSaves.Single();

        var r = FrameDetailEdit.SetShapeKeyframeCurve(_g.Commands, _g.ObjectFinder, shape, ShapeKeyframeGroup.Size, KeyframeCurveChoice.Overshoot);

        r.ChangedItems.ShouldBe(["Walk/2/rect/A"]);
        (shape.SizeInterpolation, shape.SizeEasing).ShouldBe(("Back", "Out"));
        FrameDetailEdit.SetShapeKeyframeCurve(_g.Commands, _g.ObjectFinder, shape, ShapeKeyframeGroup.Size, KeyframeCurveChoice.Overshoot)
            .ChangedItems.ShouldBeEmpty();
        FrameDetailEdit.SetShapeKeyframe(_g.Commands, _g.ObjectFinder, shape, ShapeKeyframeGroup.Offset, false);
        FrameDetailEdit.SetShapeKeyframeCurve(_g.Commands, _g.ObjectFinder, shape, ShapeKeyframeGroup.Offset, KeyframeCurveChoice.Linear)
            .Success.ShouldBeFalse();
    }

    [Fact]
    public void ShapeKeyframeOps_OnLockedChain_FailLocked()
    {
        var shape = F(2).ShapesSave!.AARectSaves.Single();
        _walk.IsLocked = true;

        FrameDetailEdit.SetShapeKeyframe(_g.Commands, _g.ObjectFinder, shape, ShapeKeyframeGroup.Offset, false)
            .HasError(EditErrorCode.Locked).ShouldBeTrue();
        FrameDetailEdit.SetShapeKeyframeCurve(_g.Commands, _g.ObjectFinder, shape, ShapeKeyframeGroup.Offset, KeyframeCurveChoice.Linear)
            .HasError(EditErrorCode.Locked).ShouldBeTrue();
    }

    // ---- reorder shape ----

    [Fact]
    public void ReorderShape_MovesWithinItsTypeGroup_FinalPosition_OneStep()
    {
        // frame 1 rects in order: A, B, C
        var r = FrameDetailEdit.ReorderShape(_g.Commands, _g.ObjectFinder, _rect3, 0);

        r.ChangedItems.ShouldBe(["Walk/1/rect/C"]);
        F(1).ShapesSave!.AARectSaves.Select(s => s.Name).ShouldBe(["C", "A", "B"]);
        FrameDetailEdit.ReorderShape(_g.Commands, _g.ObjectFinder, _rect3, 2).Success.ShouldBeTrue();
        F(1).ShapesSave!.AARectSaves.Select(s => s.Name).ShouldBe(["A", "B", "C"]);
        UndoDepth().ShouldBe(2);
    }

    [Fact]
    public void ReorderShape_ToCurrentPosition_NoOp_OutOfRangeFails_LockedFails()
    {
        FrameDetailEdit.ReorderShape(_g.Commands, _g.ObjectFinder, _rect2, 1).ChangedItems.ShouldBeEmpty();
        FrameDetailEdit.ReorderShape(_g.Commands, _g.ObjectFinder, _rect2, 3).HasError(EditErrorCode.IndexOutOfRange).ShouldBeTrue();
        FrameDetailEdit.ReorderShape(_g.Commands, _g.ObjectFinder, _circle, 1).HasError(EditErrorCode.IndexOutOfRange).ShouldBeTrue();
        _g.Undo.CanUndo.ShouldBeFalse();
        _walk.IsLocked = true;
        FrameDetailEdit.ReorderShape(_g.Commands, _g.ObjectFinder, _rect2, 0).HasError(EditErrorCode.Locked).ShouldBeTrue();
    }

    // ---- clear color ----

    [Fact]
    public void ClearColor_ClearsChosenParts_InOneUndoStep()
    {
        F(1).Red = 10; F(1).Green = 20; F(1).Blue = 30; F(1).Alpha = 40; F(1).ColorOperation = ColorOperation.Add;

        var r = FrameDetailEdit.ClearFrameColor(_g.Commands, _walk, F(1), new FrameColorClear(Green: true, Alpha: true, Operation: true));

        r.ChangedItems.ShouldBe(["Walk/1"]);
        (F(1).Red, F(1).Green, F(1).Blue, F(1).Alpha, F(1).ColorOperation).ShouldBe((10, (int?)null, 30, (int?)null, (ColorOperation?)null));
        UndoDepth().ShouldBe(1);
        (F(1).Green, F(1).Alpha, F(1).ColorOperation).ShouldBe((20, 40, ColorOperation.Add));
    }

    [Fact]
    public void ClearColor_AlreadyInherited_NoOp_NothingChosen_Fails_Locked_Fails()
    {
        FrameDetailEdit.ClearFrameColor(_g.Commands, _walk, F(1), new FrameColorClear(Red: true)).ChangedItems.ShouldBeEmpty();
        _g.Undo.CanUndo.ShouldBeFalse();
        FrameDetailEdit.ClearFrameColor(_g.Commands, _walk, F(1), default).Success.ShouldBeFalse();
        _walk.IsLocked = true;
        F(1).Red = 5;
        FrameDetailEdit.ClearFrameColor(_g.Commands, _walk, F(1), new FrameColorClear(Red: true)).HasError(EditErrorCode.Locked).ShouldBeTrue();
        F(1).Red.ShouldBe(5);
    }
}
