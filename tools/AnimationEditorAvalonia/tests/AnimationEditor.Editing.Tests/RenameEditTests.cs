using AnimationEditor.Core;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Editing.Tests;

public class RenameEditTests : IDisposable
{
    private readonly EditingTestGraph _g = new();
    private readonly AnimationChainSave _walk = new() { Name = "Walk" };
    private readonly AnimationChainSave _run = new() { Name = "Run" };
    private readonly AnimationFrameSave _frame = new();
    private readonly AARectSave _rect = new() { Name = "Hit", X = 3, Y = 4, ScaleX = 5, ScaleY = 6 };
    private readonly CircleSave _circle = new() { Name = "Ball", X = 7, Y = 8, Radius = 9 };
    private readonly PolygonSave _poly = new() { Name = "Poly", X = 1, Y = 2 };
    private readonly AnimationFrameSave _otherFrame = new();
    private readonly AARectSave _otherRect = new() { Name = "Elsewhere" };

    public RenameEditTests()
    {
        _frame.ShapesSave = new ShapesSave();
        _frame.ShapesSave.Add(_rect);
        _frame.ShapesSave.Add(_circle);
        _frame.ShapesSave.Add(_poly);
        _otherFrame.ShapesSave = new ShapesSave();
        _otherFrame.ShapesSave.Add(_otherRect);
        _walk.Frames.Add(_frame);
        _run.Frames.Add(_otherFrame);
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(_walk);
        acls.AnimationChains.Add(_run);
        _g.ProjectManager.AnimationChainListSave = acls;
    }

    public void Dispose() => _g.Dispose();

    private EditResult Chain(string name) =>
        RenameEdit.RenameChain(_g.ProjectManager, _g.Commands, _walk, name);

    private EditResult Shape(ShapeSave shape, string name) =>
        RenameEdit.RenameShape(_g.Commands, _g.ObjectFinder, shape, name);

    [Fact]
    public void RenameChain_NewName_RenamesAndUndoes()
    {
        var result = Chain("  Stride ");

        result.Success.ShouldBeTrue();
        _walk.Name.ShouldBe("Stride");
        _g.Undo.Undo();
        _walk.Name.ShouldBe("Walk");
    }

    [Fact]
    public void RenameChain_SameName_SucceedsWithoutUndoEntry()
    {
        Chain("Walk").Success.ShouldBeTrue();

        _g.Undo.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void RenameChain_Empty_FailsWithEmptyNameAndKeepsName()
    {
        var result = Chain("   ");

        result.Success.ShouldBeFalse();
        result.HasError(EditErrorCode.EmptyName).ShouldBeTrue();
        _walk.Name.ShouldBe("Walk");
        _g.Undo.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void RenameChain_NameOfAnotherChain_FailsWithDuplicateName()
    {
        var result = Chain("Run");

        result.Success.ShouldBeFalse();
        result.HasError(EditErrorCode.DuplicateName).ShouldBeTrue();
        _walk.Name.ShouldBe("Walk");
        _g.Undo.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void RenameShape_Rect_RenamesKeepingEverythingElseAndUndoes()
    {
        var result = Shape(_rect, "Body");

        result.Success.ShouldBeTrue();
        (_rect.Name, _rect.X, _rect.Y, _rect.ScaleX, _rect.ScaleY).ShouldBe(("Body", 3f, 4f, 5f, 6f));
        _g.Undo.Undo();
        (_rect.Name, _rect.X, _rect.Y, _rect.ScaleX, _rect.ScaleY).ShouldBe(("Hit", 3f, 4f, 5f, 6f));
    }

    [Fact]
    public void RenameShape_Circle_RenamesKeepingEverythingElseAndUndoes()
    {
        Shape(_circle, "Orb").Success.ShouldBeTrue();

        (_circle.Name, _circle.X, _circle.Y, _circle.Radius).ShouldBe(("Orb", 7f, 8f, 9f));
        _g.Undo.Undo();
        _circle.Name.ShouldBe("Ball");
    }

    [Fact]
    public void RenameShape_Polygon_RenamesKeepingEverythingElseAndUndoes()
    {
        Shape(_poly, "Hull").Success.ShouldBeTrue();

        (_poly.Name, _poly.X, _poly.Y).ShouldBe(("Hull", 1f, 2f));
        _g.Undo.Undo();
        _poly.Name.ShouldBe("Poly");
    }

    [Fact]
    public void RenameShape_Empty_FailsWithEmptyName()
    {
        var result = Shape(_rect, "");

        result.HasError(EditErrorCode.EmptyName).ShouldBeTrue();
        _rect.Name.ShouldBe("Hit");
        _g.Undo.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void RenameShape_NameOfAnotherShapeInFrame_FailsAcrossTypes()
    {
        var result = Shape(_rect, "Ball");

        result.HasError(EditErrorCode.DuplicateName).ShouldBeTrue();
        _rect.Name.ShouldBe("Hit");
        _g.Undo.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void RenameShape_NameUsedInAnotherFrame_Succeeds()
    {
        Shape(_rect, "Elsewhere").Success.ShouldBeTrue();

        _rect.Name.ShouldBe("Elsewhere");
    }

    [Fact]
    public void RenameShape_SameName_SucceedsWithoutUndoEntry()
    {
        Shape(_rect, "Hit").Success.ShouldBeTrue();

        _g.Undo.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void RenameOps_DoNotChangeSelection()
    {
        _g.SelectedState.SelectedChain = _run;

        Shape(_rect, "Body");
        Chain("Stride");

        _g.SelectedState.SelectedChain.ShouldBe(_run);
    }
}
