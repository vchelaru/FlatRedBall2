using AnimationEditor.Core;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Editing.Tests;

public class ShapeEditTests : IDisposable
{
    private readonly EditingTestGraph _g = new();
    private readonly AnimationChainListSave _acls = new();
    private readonly AnimationChainSave _walk = new() { Name = "Walk" };
    private readonly AnimationFrameSave _f0 = new() { TextureName = "a.png", FrameLength = 0.1f, RelativeX = 3, RelativeY = 4 };
    private readonly AnimationFrameSave _f1 = new() { TextureName = "b.png", FrameLength = 0.1f };
    private readonly AARectSave _rect = new() { Name = "Hitbox", X = 1, Y = 2, ScaleX = 4, ScaleY = 6 };
    private readonly CircleSave _circle = new() { Name = "Foot", X = 0, Y = -8, Radius = 3 };
    private readonly PolygonSave _poly = new() { Name = "Poly", X = 5, Y = 6 };

    public ShapeEditTests()
    {
        _f1.ShapesSave = new ShapesSave();
        _f1.ShapesSave.Add(_rect);
        _f1.ShapesSave.Add(_circle);
        _f1.ShapesSave.Add(_poly);
        foreach (var (x, y) in new[] { (0f, 0f), (4f, 0f), (4f, 4f), (0f, 0f) })
            _poly.Points.Add(new Vector2Save { X = x, Y = y });
        _walk.Frames.Add(_f0);
        _walk.Frames.Add(_f1);
        _acls.AnimationChains.Add(_walk);
        _g.ProjectManager.AnimationChainListSave = _acls;
        _g.ProjectManager.FileName = Path.Combine(_g.Root, "hero.achx");
    }

    public void Dispose() => _g.Dispose();

    private EditResult Add(AnimationFrameSave frame, string type, string? name = null) =>
        ShapeEdit.AddShape(_g.Commands, _g.ObjectFinder, frame, type, name);

    private int UndoDepth()
    {
        int n = 0;
        while (_g.Undo.CanUndo) { _g.Undo.Undo(); n++; }
        return n;
    }

    // ---- add ----

    [Fact]
    public void AddRect_DefaultsAtFrameOffset_NamedAndAddressed_OneUndoStep()
    {
        var r = Add(_f0, "rect");

        r.Success.ShouldBeTrue();
        r.ChangedItems.ShouldBe(["Walk/0/rect/AxisAlignedRectangleInstance"]);
        var rect = _f0.ShapesSave!.AARectSaves.Single();
        (rect.X, rect.Y, rect.ScaleX, rect.ScaleY).ShouldBe((3f, 4f, 8f, 8f));
        UndoDepth().ShouldBe(1);
        _f0.ShapesSave.ShouldBeNull();
    }

    [Fact]
    public void AddCircle_Defaults()
    {
        var r = Add(_f0, "circle", "Sensor");

        r.ChangedItems.ShouldBe(["Walk/0/circle/Sensor"]);
        var c = _f0.ShapesSave!.CircleSaves.Single();
        (c.X, c.Y, c.Radius).ShouldBe((3f, 4f, 8f));
    }

    [Fact]
    public void AddPolygon_DefaultsToClosedSquare()
    {
        var r = Add(_f0, "polygon", "Area");

        r.ChangedItems.ShouldBe(["Walk/0/polygon/Area"]);
        var p = _f0.ShapesSave!.PolygonSaves.Single();
        (p.X, p.Y).ShouldBe((3f, 4f));
        p.Points.Count.ShouldBe(5);
    }

    [Fact]
    public void Add_WithoutName_PicksAUniqueDefaultWithinTheFrame()
    {
        Add(_f0, "circle").Success.ShouldBeTrue();
        var second = Add(_f0, "circle");

        second.Success.ShouldBeTrue();
        _f0.ShapesSave!.CircleSaves.Select(c => c.Name).Distinct().Count().ShouldBe(2);
    }

    [Theory]
    [InlineData("Hitbox")]   // same type
    [InlineData("Foot")]     // another type
    public void Add_DuplicateNameInFrame_Fails(string name)
    {
        var r = Add(_f1, "polygon", name);

        r.HasError(EditErrorCode.DuplicateName).ShouldBeTrue();
        _f1.ShapesSave!.Shapes.Count.ShouldBe(3);
        _g.Undo.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void Add_SameNameInAnotherFrame_IsFine()
    {
        Add(_f0, "rect", "Hitbox").Success.ShouldBeTrue();
    }

    [Theory]
    [InlineData("  ", EditErrorCode.EmptyName)]
    [InlineData("a/b", EditErrorCode.InvalidAddress)]
    public void Add_BadName_Fails(string name, EditErrorCode code)
    {
        var r = Add(_f0, "rect", name);

        r.Success.ShouldBeFalse();
        if (code != EditErrorCode.Unspecified) r.HasError(code).ShouldBeTrue();
        _f0.ShapesSave.ShouldBeNull();
    }

    [Fact]
    public void Add_UnknownType_Fails()
    {
        var r = Add(_f0, "cube", "X");

        r.Success.ShouldBeFalse();
        r.Errors[0].ShouldContain("rect|circle|polygon");
    }

    [Fact]
    public void Add_LockedChain_FailsWithLocked()
    {
        _walk.IsLocked = true;

        var r = Add(_f0, "rect", "X");

        r.HasError(EditErrorCode.Locked).ShouldBeTrue();
        _f0.ShapesSave.ShouldBeNull();
    }

    // ---- delete / duplicate ----

    [Fact]
    public void Delete_RemovesOnlyThatShape_OneUndoStep()
    {
        var r = ShapeEdit.DeleteShape(_g.Commands, _g.ObjectFinder, _circle);

        r.Success.ShouldBeTrue();
        r.ChangedItems.ShouldBe(["Walk/1/circle/Foot"]);
        _f1.ShapesSave!.Shapes.ShouldBe([_rect, _poly]);
        UndoDepth().ShouldBe(1);
        _f1.ShapesSave!.Shapes.Count.ShouldBe(3);
    }

    [Fact]
    public void Delete_LockedChain_FailsWithLocked()
    {
        _walk.IsLocked = true;

        var r = ShapeEdit.DeleteShape(_g.Commands, _g.ObjectFinder, _rect);

        r.HasError(EditErrorCode.Locked).ShouldBeTrue();
        _f1.ShapesSave!.Shapes.Count.ShouldBe(3);
    }

    [Fact]
    public void Duplicate_CopiesWithUniqueName_AndReportsTheCopy()
    {
        var r = ShapeEdit.DuplicateShape(_g.Commands, _g.ObjectFinder, _rect);

        r.Success.ShouldBeTrue();
        var copy = _f1.ShapesSave!.AARectSaves.Last();
        copy.ShouldNotBeSameAs(_rect);
        copy.Name.ShouldNotBe("Hitbox");
        (copy.X, copy.ScaleX, copy.ScaleY).ShouldBe((1f, 4f, 6f));
        r.ChangedItems.ShouldBe([$"Walk/1/rect/{copy.Name}"]);
        UndoDepth().ShouldBe(1);
    }

    [Fact]
    public void Duplicate_LockedChain_FailsWithLocked()
    {
        _walk.IsLocked = true;

        ShapeEdit.DuplicateShape(_g.Commands, _g.ObjectFinder, _rect).HasError(EditErrorCode.Locked).ShouldBeTrue();
        _f1.ShapesSave!.Shapes.Count.ShouldBe(3);
    }

    // ---- set ----

    [Fact]
    public void SetRect_GivenFieldsChange_OthersStay_OneUndoStep()
    {
        var r = ShapeEdit.SetRect(_g.Commands, _g.ObjectFinder, _rect, x: 9, y: null, scaleX: null, scaleY: 12);

        r.Success.ShouldBeTrue();
        r.ChangedItems.ShouldBe(["Walk/1/rect/Hitbox"]);
        (_rect.X, _rect.Y, _rect.ScaleX, _rect.ScaleY).ShouldBe((9f, 2f, 4f, 12f));
        UndoDepth().ShouldBe(1);
        (_rect.X, _rect.ScaleY).ShouldBe((1f, 6f));
    }

    [Fact]
    public void SetRect_SameValues_IsNoOpWithoutUndoStep()
    {
        var r = ShapeEdit.SetRect(_g.Commands, _g.ObjectFinder, _rect, 1, 2, 4, 6);

        r.Success.ShouldBeTrue();
        r.ChangedItems.ShouldBeEmpty();
        _g.Undo.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void SetRect_NothingGiven_Fails()
    {
        ShapeEdit.SetRect(_g.Commands, _g.ObjectFinder, _rect, null, null, null, null).Success.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    public void SetRect_NonPositiveScale_Fails(float scale)
    {
        var r = ShapeEdit.SetRect(_g.Commands, _g.ObjectFinder, _rect, null, null, scale, null);

        r.Success.ShouldBeFalse();
        r.Errors[0].ToLowerInvariant().ShouldContain("scale");
        _rect.ScaleX.ShouldBe(4f);
    }

    [Fact]
    public void SetRect_NonFinitePosition_Fails()
    {
        ShapeEdit.SetRect(_g.Commands, _g.ObjectFinder, _rect, float.PositiveInfinity, null, null, null).Success.ShouldBeFalse();
    }

    [Fact]
    public void SetRect_LockedChain_FailsWithLocked()
    {
        _walk.IsLocked = true;

        ShapeEdit.SetRect(_g.Commands, _g.ObjectFinder, _rect, 9, null, null, null).HasError(EditErrorCode.Locked).ShouldBeTrue();
        _rect.X.ShouldBe(1f);
    }

    [Fact]
    public void SetCircle_ChangesFields_OneUndoStep()
    {
        var r = ShapeEdit.SetCircle(_g.Commands, _g.ObjectFinder, _circle, x: null, y: 5, radius: 7);

        r.ChangedItems.ShouldBe(["Walk/1/circle/Foot"]);
        (_circle.X, _circle.Y, _circle.Radius).ShouldBe((0f, 5f, 7f));
        UndoDepth().ShouldBe(1);
        _circle.Radius.ShouldBe(3f);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-2f)]
    public void SetCircle_NonPositiveRadius_Fails(float radius)
    {
        ShapeEdit.SetCircle(_g.Commands, _g.ObjectFinder, _circle, null, null, radius).Success.ShouldBeFalse();
        _circle.Radius.ShouldBe(3f);
    }

    [Fact]
    public void SetCircle_LockedChain_FailsWithLocked()
    {
        _walk.IsLocked = true;

        ShapeEdit.SetCircle(_g.Commands, _g.ObjectFinder, _circle, null, null, 9).HasError(EditErrorCode.Locked).ShouldBeTrue();
    }

    [Fact]
    public void SetPolygon_OriginAndPoints_OneUndoStep()
    {
        var points = new (float X, float Y)[] { (0, 0), (8, 0), (8, 8), (0, 8) };

        var r = ShapeEdit.SetPolygon(_g.Commands, _g.ObjectFinder, _poly, x: 1, y: null, points);

        r.ChangedItems.ShouldBe(["Walk/1/polygon/Poly"]);
        (_poly.X, _poly.Y).ShouldBe((1f, 6f));
        _poly.Points.Select(p => (p.X, p.Y)).ToArray().ShouldBe(points);
        UndoDepth().ShouldBe(1);
        _poly.Points.Count.ShouldBe(4);
        (_poly.X, _poly.Points[1].X).ShouldBe((5f, 4f));
    }

    [Fact]
    public void SetPolygon_OriginOnly_KeepsPoints()
    {
        ShapeEdit.SetPolygon(_g.Commands, _g.ObjectFinder, _poly, 7, 8, null).Success.ShouldBeTrue();

        (_poly.X, _poly.Y, _poly.Points.Count).ShouldBe((7f, 8f, 4));
    }

    [Fact]
    public void SetPolygon_FewerThanThreePoints_Fails()
    {
        var r = ShapeEdit.SetPolygon(_g.Commands, _g.ObjectFinder, _poly, null, null, new (float, float)[] { (0, 0), (1, 1) });

        r.Success.ShouldBeFalse();
        r.Errors[0].ShouldContain("3");
        _poly.Points.Count.ShouldBe(4);
    }

    [Fact]
    public void SetPolygon_SamePoints_IsNoOp()
    {
        var same = _poly.Points.Select(p => (p.X, p.Y)).ToArray();

        var r = ShapeEdit.SetPolygon(_g.Commands, _g.ObjectFinder, _poly, null, null, same);

        r.ChangedItems.ShouldBeEmpty();
        _g.Undo.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void SetPolygon_LockedChain_FailsWithLocked()
    {
        _walk.IsLocked = true;

        ShapeEdit.SetPolygon(_g.Commands, _g.ObjectFinder, _poly, 1, null, null).HasError(EditErrorCode.Locked).ShouldBeTrue();
    }

    [Fact]
    public void SetCommon_MovesAnyType_OneUndoStepEach()
    {
        foreach (ShapeSave shape in new ShapeSave[] { _rect, _circle, _poly })
        {
            var r = ShapeEdit.SetCommon(_g.Commands, _g.ObjectFinder, shape, 10, 20);
            r.Success.ShouldBeTrue();
            (shape.X, shape.Y).ShouldBe((10f, 20f));
        }
        UndoDepth().ShouldBe(3);
        (_rect.X, _circle.Y, _poly.X).ShouldBe((1f, -8f, 5f));
    }

    [Fact]
    public void SetCommon_NothingGiven_Fails_AndLockedFails()
    {
        ShapeEdit.SetCommon(_g.Commands, _g.ObjectFinder, _rect, null, null).Success.ShouldBeFalse();
        _walk.IsLocked = true;
        ShapeEdit.SetCommon(_g.Commands, _g.ObjectFinder, _rect, 1, null).HasError(EditErrorCode.Locked).ShouldBeTrue();
    }
}
