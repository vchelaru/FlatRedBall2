using AnimationEditor.Core;
using FlatRedBall2.AnimationEditorCommon;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Deleting a multi-selection of shapes is one undo step, drops every deleted shape from the
/// selection, and one Undo restores all of them and the selection (#1824).
/// </summary>
[Collection("SequentialSingletons")]
public class AppCommandsDeleteShapesSelectionTests
{
    private static (TestServices Ctx, AnimationFrameSave A, AnimationFrameSave B, AARectSave Rect, CircleSave Circle, PolygonSave Poly)
        Setup()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Run", 2);
        var rect = new AARectSave { Name = "Rect" };
        var circle = new CircleSave { Name = "Circle", Radius = 10 };
        var poly = new PolygonSave { Name = "Poly" };
        chain.Frames[0].ShapesSave!.Add(rect);
        chain.Frames[0].ShapesSave!.Add(circle);
        chain.Frames[1].ShapesSave!.Add(poly);
        return (ctx, chain.Frames[0], chain.Frames[1], rect, circle, poly);
    }

    [Fact]
    public void DeleteShapes_DropsEveryDeletedShapeFromTheSelection()
    {
        var (ctx, _, _, rect, circle, poly) = Setup();
        ctx.SelectedState.SelectedNodes = new List<object> { rect, circle };

        ctx.AppCommands.DeleteShapes(ctx.SelectedState.SelectedShapes);

        Assert.Empty(ctx.SelectedState.SelectedShapes);
        Assert.Empty(ctx.SelectedState.SelectedNodes);
        Assert.Null(ctx.SelectedState.SelectedShape);
    }

    [Fact]
    public void DeleteShapes_KeepsSelectedNodesThatWereNotDeleted()
    {
        var (ctx, a, _, rect, circle, _) = Setup();
        ctx.SelectedState.SelectedNodes = new List<object> { a, rect, circle };

        ctx.AppCommands.DeleteShapes(new object[] { rect, circle });

        Assert.Equal(new object[] { a }, ctx.SelectedState.SelectedNodes);
    }

    [Fact]
    public void DeleteShapes_AcrossFramesAndTypes_OneUndoRestoresAllAndTheWholeSelection()
    {
        var (ctx, a, b, rect, circle, poly) = Setup();
        ctx.SelectedState.SelectedNodes = new List<object> { circle, poly, rect };
        int before = ctx.UndoManager.UndoHistory.Count;

        ctx.AppCommands.DeleteShapes(ctx.SelectedState.SelectedShapes);

        Assert.Equal(before + 1, ctx.UndoManager.UndoHistory.Count);
        Assert.Empty(a.ShapesSave!.Shapes);
        Assert.Empty(b.ShapesSave!.Shapes);

        ctx.UndoManager.Undo();

        Assert.Equal(new object[] { rect, circle }, a.ShapesSave!.Shapes.ToArray());
        Assert.Equal(new object[] { poly }, b.ShapesSave!.Shapes.ToArray());
        Assert.Equal(new object[] { circle, poly, rect }, ctx.SelectedState.SelectedNodes);
        Assert.Same(circle, ctx.SelectedState.SelectedShape);
    }

    [Fact]
    public void DeleteShapes_RedoRemovesAllAgainAndClearsTheSelection()
    {
        var (ctx, a, b, rect, circle, poly) = Setup();
        ctx.SelectedState.SelectedNodes = new List<object> { rect, poly };
        ctx.AppCommands.DeleteShapes(ctx.SelectedState.SelectedShapes);
        ctx.UndoManager.Undo();

        ctx.UndoManager.Redo();

        Assert.Equal(new object[] { circle }, a.ShapesSave!.Shapes.ToArray());
        Assert.Empty(b.ShapesSave!.Shapes);
        Assert.Empty(ctx.SelectedState.SelectedShapes);
    }

    [Fact]
    public void DeleteSelection_MixedShapesAndFrame_UndoRestoresTheSelectionToo()
    {
        var (ctx, a, b, rect, _, poly) = Setup();
        ctx.SelectedState.SelectedNodes = new List<object> { rect, poly, a };

        ctx.AppCommands.DeleteSelection(new List<AnimationChainSave>(), new[] { a }, new object[] { rect, poly });
        Assert.DoesNotContain(rect, ctx.SelectedState.SelectedNodes);
        ctx.UndoManager.Undo();

        Assert.Equal(new object[] { rect, poly, a }, ctx.SelectedState.SelectedNodes);
    }
}
