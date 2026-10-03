using AnimationEditor.Core.DragDrop;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>Drag a shape row to reorder it among shapes of its own type (#1285).</summary>
public class ShapeDropResolverTests
{
    private static (ShapesSave Shapes, AARectSave A, AARectSave B, AARectSave C, CircleSave Circle) Build()
    {
        var shapes = new ShapesSave();
        var a = new AARectSave { Name = "A" };
        var b = new AARectSave { Name = "B" };
        var c = new AARectSave { Name = "C" };
        var circle = new CircleSave { Name = "Circle" };
        shapes.Add(a); shapes.Add(b); shapes.Add(c); shapes.Add(circle);
        return (shapes, a, b, c, circle);
    }

    [Fact]
    public void Resolve_UpperHalfOfSameTypeShape_InsertsBeforeIt()
    {
        var (shapes, a, b, c, _) = Build();
        var target = ShapeDropResolver.Resolve(b, FrameRowHalf.Upper, c, shapes);
        Assert.True(target.IsValid);
        Assert.Equal(1, target.InsertIndex);
    }

    [Fact]
    public void Resolve_LowerHalfOfSameTypeShape_InsertsAfterIt()
    {
        var (shapes, a, b, c, _) = Build();
        var target = ShapeDropResolver.Resolve(b, FrameRowHalf.Lower, a, shapes);
        Assert.True(target.IsValid);
        Assert.Equal(2, target.InsertIndex);
    }

    [Fact]
    public void Resolve_OtherTypeInSameFrame_IsCrossType()
    {
        var (shapes, a, _, _, circle) = Build();
        Assert.Equal(ShapeDropOutcome.CrossType, ShapeDropResolver.Resolve(circle, FrameRowHalf.Upper, a, shapes).Outcome);
    }

    [Fact]
    public void Resolve_ShapeFromAnotherFrameOrNonShape_IsNone()
    {
        var (shapes, a, _, _, _) = Build();
        Assert.Equal(ShapeDropOutcome.None, ShapeDropResolver.Resolve(new AARectSave(), FrameRowHalf.Upper, a, shapes).Outcome);
        Assert.Equal(ShapeDropOutcome.None, ShapeDropResolver.Resolve(new AnimationFrameSave(), FrameRowHalf.Upper, a, shapes).Outcome);
        Assert.Equal(ShapeDropOutcome.None, ShapeDropResolver.Resolve(null, FrameRowHalf.Upper, a, shapes).Outcome);
    }

    [Fact]
    public void ApplyDrop_MovesShapeForwardAndBack()
    {
        var (shapes, a, b, c, circle) = Build();
        Assert.Equal(new object[] { b, c, a, circle }, ShapeDropResolver.ApplyDrop(shapes, a, 3));
        Assert.Equal(new object[] { c, a, b, circle }, ShapeDropResolver.ApplyDrop(shapes, c, 0));
    }

    [Fact]
    public void ApplyDrop_OntoOwnEdge_IsNoOp()
    {
        var (shapes, a, b, c, circle) = Build();
        var original = new object[] { a, b, c, circle };
        Assert.Equal(original, ShapeDropResolver.ApplyDrop(shapes, b, 1));
        Assert.Equal(original, ShapeDropResolver.ApplyDrop(shapes, b, 2));
    }

    [Fact]
    public void ApplyDrop_IndexPastTypeGroup_ClampsToGroupEnd()
    {
        var (shapes, a, b, c, circle) = Build();
        Assert.Equal(new object[] { b, c, a, circle }, ShapeDropResolver.ApplyDrop(shapes, a, 99));
    }

    [Fact]
    public void MoveShapeToIndex_ReordersAndUndoRedo()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var frame = TestHelpers.MakeChain(ctx.Acls, "Walk", 1).Frames[0];
        var a = new AARectSave { Name = "A" };
        var b = new AARectSave { Name = "B" };
        var c = new AARectSave { Name = "C" };
        frame.ShapesSave!.Add(a); frame.ShapesSave.Add(b); frame.ShapesSave.Add(c);

        ctx.AppCommands.MoveShapeToIndex(a, frame, 3);
        Assert.Equal(new object[] { b, c, a }, frame.ShapesSave.Shapes);

        ctx.UndoManager.Undo();
        Assert.Equal(new object[] { a, b, c }, frame.ShapesSave.Shapes);
        ctx.UndoManager.Redo();
        Assert.Equal(new object[] { b, c, a }, frame.ShapesSave.Shapes);
    }

    [Fact]
    public void MoveShapeToIndex_NoChange_RecordsNoUndoEntry()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var frame = TestHelpers.MakeChain(ctx.Acls, "Walk", 1).Frames[0];
        var a = new AARectSave { Name = "A" };
        var b = new AARectSave { Name = "B" };
        frame.ShapesSave!.Add(a); frame.ShapesSave.Add(b);

        bool couldUndoBefore = ctx.UndoManager.CanUndo;
        ctx.AppCommands.MoveShapeToIndex(a, frame, 1);

        Assert.Equal(couldUndoBefore, ctx.UndoManager.CanUndo);
    }
}
