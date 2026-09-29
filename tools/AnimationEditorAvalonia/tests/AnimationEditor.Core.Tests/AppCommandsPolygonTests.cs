using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.Utilities;
using AnimationEditor.Core.ViewModels;
using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests;

[Collection("SequentialSingletons")]
public class AppCommandsPolygonTests
{
    private static (TestServices Ctx, AnimationFrameSave Frame) FrameWithNoShapes()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 1);
        var frame = chain.Frames[0];
        frame.ShapesSave = null;
        ctx.SelectedState.SelectedFrame = frame;
        return (ctx, frame);
    }

    private static PolygonSave AddPolygon(TestServices ctx, AnimationFrameSave frame)
    {
        ctx.AppCommands.AddPolygon(frame);
        return frame.ShapesSave!.PolygonSaves.Last();
    }

    [Fact]
    public void AddPolygon_AddsClosed16PixelSquareAtFrameOffset_AndSelectsIt()
    {
        var (ctx, frame) = FrameWithNoShapes();
        frame.RelativeX = 5;
        frame.RelativeY = -3;

        var polygon = AddPolygon(ctx, frame);

        polygon.X.ShouldBe(5f);
        polygon.Y.ShouldBe(-3f);
        PolygonVertices.IsClosed(polygon).ShouldBeTrue("new polygons follow the FRB1 closed-outline convention");
        PolygonVertices.Count(polygon).ShouldBe(4);
        polygon.Points.Select(p => (p.X, p.Y)).ShouldBe(new[] { (-8f, -8f), (8f, -8f), (8f, 8f), (-8f, 8f), (-8f, -8f) });
        ctx.SelectedState.SelectedPolygon.ShouldBe(polygon);
        ctx.UndoManager.UndoHistory[^1].Description.ShouldBe("Add Polygon 'PolygonInstance'");
    }

    [Fact]
    public void AddPolygon_ThenUndo_OnAFrameWithNoShapes_LeavesShapesSaveNull()
    {
        var (ctx, frame) = FrameWithNoShapes();
        AddPolygon(ctx, frame);

        ctx.UndoManager.Undo();

        frame.ShapesSave.ShouldBeNull();
    }

    [Fact]
    public void AddPolygon_NextToARectangleNamedLikeIt_PicksAnUnusedName()
    {
        var (ctx, frame) = FrameWithNoShapes();
        frame.ShapesSave = new ShapesSave();
        frame.ShapesSave.Shapes.Add(new AARectSave { Name = "PolygonInstance" });

        var polygon = AddPolygon(ctx, frame);

        polygon.Name.ShouldNotBe("PolygonInstance");
    }

    [Fact]
    public void DeleteShapes_PolygonAndCircle_RemovesBothInOneUndoStep()
    {
        var (ctx, frame) = FrameWithNoShapes();
        ctx.AppCommands.AddCircle(frame);
        var circle = frame.ShapesSave!.CircleSaves.Single();
        var polygon = AddPolygon(ctx, frame);

        ctx.AppCommands.DeleteShapes(new object[] { circle, polygon });
        frame.ShapesSave!.Shapes.ShouldBeEmpty();
        ctx.UndoManager.Undo();

        frame.ShapesSave!.Shapes.ShouldBe(new object[] { circle, polygon });
    }

    [Fact]
    public void MovePolygonVertex_FirstVertex_MovesClosingPoint_AndUndoRestoresBoth()
    {
        var (ctx, frame) = FrameWithNoShapes();
        var polygon = AddPolygon(ctx, frame);

        ctx.AppCommands.MovePolygonVertex(polygon, 0, -20, -12);
        polygon.Points[^1].X.ShouldBe(-20f);
        ctx.UndoManager.Undo();

        polygon.Points[0].X.ShouldBe(-8f);
        polygon.Points[^1].X.ShouldBe(-8f);
        polygon.Points[^1].Y.ShouldBe(-8f);
    }

    [Fact]
    public void InsertPolygonVertex_ThenUndo_RemovesIt()
    {
        var (ctx, frame) = FrameWithNoShapes();
        var polygon = AddPolygon(ctx, frame);

        ctx.AppCommands.InsertPolygonVertex(polygon, 1, 0, -12);
        PolygonVertices.Count(polygon).ShouldBe(5);
        PolygonVertices.Get(polygon, 1).ShouldBe((0f, -12f));
        ctx.UndoManager.Undo();

        PolygonVertices.Count(polygon).ShouldBe(4);
    }

    [Fact]
    public void DeletePolygonVertex_Triangle_RefusesAndRecordsNothing()
    {
        var (ctx, frame) = FrameWithNoShapes();
        var polygon = AddPolygon(ctx, frame);
        ctx.AppCommands.DeletePolygonVertex(polygon, 0).ShouldBeTrue();
        int undoCount = ctx.UndoManager.UndoHistory.Count;

        ctx.AppCommands.DeletePolygonVertex(polygon, 0).ShouldBeFalse();

        PolygonVertices.Count(polygon).ShouldBe(3);
        ctx.UndoManager.UndoHistory.Count.ShouldBe(undoCount);
    }

    [Fact]
    public void CommitPolygonPoints_AfterALiveDrag_UndoRestoresTheSnapshot()
    {
        var (ctx, frame) = FrameWithNoShapes();
        var polygon = AddPolygon(ctx, frame);
        var before = PolygonVertices.CopyPoints(polygon);
        PolygonVertices.Set(polygon, 2, 30, 30); // the preview mutates live while dragging

        ctx.AppCommands.CommitPolygonPoints(polygon, before, PolygonVertexEdit.Move(2));
        ctx.UndoManager.Undo();

        PolygonVertices.Get(polygon, 2).ShouldBe((8f, 8f));
        ctx.UndoManager.Redo();
        PolygonVertices.Get(polygon, 2).ShouldBe((30f, 30f));
    }

    [Fact]
    public void SetPolygonProps_RenamesAndMovesOrigin_WithoutTouchingPoints()
    {
        var (ctx, frame) = FrameWithNoShapes();
        var polygon = AddPolygon(ctx, frame);

        ctx.AppCommands.SetPolygonProps(frame, polygon, "Hurtbox", 4, 6);

        polygon.Name.ShouldBe("Hurtbox");
        polygon.X.ShouldBe(4f);
        polygon.Y.ShouldBe(6f);
        PolygonVertices.Get(polygon, 0).ShouldBe((-8f, -8f));
    }

    [Fact]
    public void PasteShapes_Polygon_CopyOwnsItsPoints()
    {
        var (ctx, frame) = FrameWithNoShapes();
        var polygon = AddPolygon(ctx, frame);

        ctx.AppCommands.PasteShapes(frame, new object[] { polygon });
        var copy = frame.ShapesSave!.PolygonSaves.Last();
        PolygonVertices.Set(copy, 1, 99, 99);

        copy.ShouldNotBeSameAs(polygon);
        copy.Name.ShouldNotBe(polygon.Name);
        PolygonVertices.Get(polygon, 1).ShouldBe((8f, -8f));
    }

    [Fact]
    public void DuplicateShape_Polygon_AddsIndependentCopy()
    {
        var (ctx, frame) = FrameWithNoShapes();
        var polygon = AddPolygon(ctx, frame);

        var copy = ctx.AppCommands.DuplicateShape(polygon).ShouldBeOfType<PolygonSave>();

        copy.Points.ShouldNotBeSameAs(polygon.Points);
        copy.Points.Count.ShouldBe(5);
        frame.ShapesSave!.PolygonSaves.Count().ShouldBe(2);
    }

    [Fact]
    public void SelectedPolygon_Set_ClearsOtherShapeSelection()
    {
        var (ctx, frame) = FrameWithNoShapes();
        ctx.AppCommands.AddCircle(frame);
        var polygon = AddPolygon(ctx, frame);
        ctx.SelectedState.SelectedCircle = frame.ShapesSave!.CircleSaves.Single();

        ctx.SelectedState.SelectedPolygon = polygon;

        ctx.SelectedState.SelectedCircle.ShouldBeNull();
        ctx.SelectedState.SelectedShape.ShouldBe(polygon);
    }

    [Fact]
    public void TreeMenu_FrameNode_OffersAddPolygon_AndPolygonNodeOffersDelete()
    {
        var (ctx, frame) = FrameWithNoShapes();
        var polygon = AddPolygon(ctx, frame);
        var actions = new TreeMenuActions(
            Copy: () => { }, Cut: () => { }, Paste: () => { }, Duplicate: () => { }, Delete: () => { },
            Rename: () => { }, AddAnimation: () => { }, DuplicateChainFlip: (_, _) => { });

        var frameItems = TreeMenuPlanBuilder.Build(
            frame, ctx.AppCommands, ctx.SelectedState, ctx.ObjectFinder, ctx.ProjectManager, actions);
        var polygonItems = TreeMenuPlanBuilder.Build(
            polygon, ctx.AppCommands, ctx.SelectedState, ctx.ObjectFinder, ctx.ProjectManager, actions);

        frameItems.ShouldContain(i => i.Header == "Add Polygon");
        polygonItems.ShouldContain(i => i.Header == "Delete Polygon");
        polygonItems.ShouldContain(i => i.Header == "Rename…");
    }

    [Fact]
    public void BuildFrameNode_Polygon_AddsPolygonChild()
    {
        var (ctx, frame) = FrameWithNoShapes();
        var polygon = AddPolygon(ctx, frame);

        var node = TreeBuilder.BuildFrameNode(frame);

        node.Children.Single().Data.ShouldBe(polygon);
        node.Children.Single().Kind.ShouldBe(NodeKind.PolygonShape);
        node.Children.Single().Header.ShouldBe(polygon.Name);
    }
}
