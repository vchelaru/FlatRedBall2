using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// A vertex edit on one of several selected polygons applies to the matching vertex of every
/// other selected polygon with the same vertex count (issue #1255).
/// </summary>
[Collection("SequentialSingletons")]
public class AppCommandsPolygonMultiSelectTests
{
    // One polygon per frame of one chain, each a 16px square offset by its frame index so the
    // copies differ slightly, the way a hitbox that follows the sprite does.
    private static (TestServices Ctx, AnimationChainSave Chain, List<PolygonSave> Polygons) SelectedPolygons(int count)
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", count);
        var polygons = new List<PolygonSave>();
        foreach (var frame in chain.Frames)
        {
            frame.ShapesSave = null;
            ctx.AppCommands.AddPolygon(frame);
            polygons.Add(frame.ShapesSave!.PolygonSaves.Single());
        }
        for (int i = 0; i < polygons.Count; i++)
        {
            polygons[i].X = i * 10;
            foreach (var point in polygons[i].Points) point.X += i;
        }
        ctx.UndoManager.Clear();
        ctx.SelectedState.SelectedPolygon = polygons[0];
        ctx.SelectedState.SelectedNodes = polygons.Cast<object>().ToList();
        return (ctx, chain, polygons);
    }

    [Fact]
    public void MovePolygonVertex_AppliesTheSameDeltaToEverySelectedPolygon_InOneUndoEntry()
    {
        var (ctx, _, polygons) = SelectedPolygons(3);

        ctx.AppCommands.MovePolygonVertex(polygons[0], 1, 8, -12); // vertex 1 was (8, -8)

        PolygonVertices.Get(polygons[0], 1).ShouldBe((8f, -12f));
        PolygonVertices.Get(polygons[1], 1).ShouldBe((9f, -12f));
        PolygonVertices.Get(polygons[2], 1).ShouldBe((10f, -12f));
        ctx.UndoManager.UndoHistory.Count.ShouldBe(1);
        ctx.UndoManager.UndoHistory[^1].Description.ShouldBe("Move Vertex 2 of 3 Polygons");

        ctx.UndoManager.Undo();

        PolygonVertices.Get(polygons[1], 1).ShouldBe((9f, -8f));
        PolygonVertices.Get(polygons[2], 1).ShouldBe((10f, -8f));
    }

    [Fact]
    public void MovePolygonVertex_TypedKeystrokes_CoalesceAndUndoRestoresEveryPolygon()
    {
        var (ctx, _, polygons) = SelectedPolygons(2);

        ctx.AppCommands.MovePolygonVertex(polygons[0], 1, 8, -1);
        ctx.AppCommands.MovePolygonVertex(polygons[0], 1, 8, -12);

        PolygonVertices.Get(polygons[1], 1).ShouldBe((9f, -12f));
        ctx.UndoManager.UndoHistory.Count.ShouldBe(1);
        ctx.UndoManager.Undo();
        PolygonVertices.Get(polygons[1], 1).ShouldBe((9f, -8f));
    }

    [Fact]
    public void MovePolygonVertex_SkipsADifferentVertexCount_AndReportsIt()
    {
        var (ctx, _, polygons) = SelectedPolygons(3);
        PolygonVertices.Insert(polygons[2], 1, 0, -10);
        var notices = new List<string>();
        ctx.AppCommands.Notified += notices.Add;

        ctx.AppCommands.MovePolygonVertex(polygons[0], 1, 8, -12);

        PolygonVertices.Get(polygons[1], 1).ShouldBe((9f, -12f));
        PolygonVertices.Get(polygons[2], 1).ShouldBe((0f, -10f));
        notices.ShouldBe(new[] { "Moved vertex on 2 of 3 polygons, 1 skipped: different vertex count" });
    }

    [Fact]
    public void MovePolygonVertex_SkipsAPolygonInALockedChain()
    {
        var (ctx, _, polygons) = SelectedPolygons(2);
        var other = TestHelpers.MakeChain(ctx.Acls, "Locked", 1);
        other.Frames[0].ShapesSave = null;
        ctx.AppCommands.AddPolygon(other.Frames[0]);
        var locked = other.Frames[0].ShapesSave!.PolygonSaves.Single();
        other.IsLocked = true;
        ctx.SelectedState.SelectedNodes = new List<object> { polygons[0], polygons[1], locked };

        ctx.AppCommands.MovePolygonVertex(polygons[0], 1, 8, -12);

        PolygonVertices.Get(polygons[1], 1).ShouldBe((9f, -12f));
        PolygonVertices.Get(locked, 1).ShouldBe((8f, -8f));
    }

    [Fact]
    public void MovePolygonVertex_OnAPolygonOutsideTheMultiSelection_LeavesTheSelectionAlone()
    {
        var (ctx, _, polygons) = SelectedPolygons(3);
        ctx.SelectedState.SelectedNodes = new List<object> { polygons[1], polygons[2] };

        ctx.AppCommands.MovePolygonVertex(polygons[0], 1, 8, -12);

        PolygonVertices.Get(polygons[1], 1).ShouldBe((9f, -8f));
    }

    [Fact]
    public void InsertPolygonVertex_InsertsAtTheSameIndex_AtEachPolygonsOwnEdgeMidpointPlusTheOffset()
    {
        var (ctx, _, polygons) = SelectedPolygons(2);

        // Edge 0 -> 1 of polygons[0] runs (-8,-8) to (8,-8); its midpoint is (0,-8).
        ctx.AppCommands.InsertPolygonVertex(polygons[0], 1, 0, -12);

        PolygonVertices.Count(polygons[1]).ShouldBe(5);
        PolygonVertices.Get(polygons[1], 1).ShouldBe((1f, -12f));
        ctx.UndoManager.UndoHistory[^1].Description.ShouldBe("Add Vertex to 2 Polygons");
    }

    [Fact]
    public void DeletePolygonVertex_DeletesTheSameIndexOnEverySelectedPolygon()
    {
        var (ctx, _, polygons) = SelectedPolygons(2);

        ctx.AppCommands.DeletePolygonVertex(polygons[0], 1).ShouldBeTrue();

        PolygonVertices.Count(polygons[1]).ShouldBe(3);
        PolygonVertices.Get(polygons[1], 1).ShouldBe((9f, 8f));
        ctx.UndoManager.UndoHistory[^1].Description.ShouldBe("Delete Vertex from 2 Polygons");
        ctx.UndoManager.Undo();
        PolygonVertices.Count(polygons[1]).ShouldBe(4);
    }

    [Fact]
    public void CommitPolygonPoints_LiveVertexDrag_AppliesTheDragDeltaToEverySelectedPolygon()
    {
        var (ctx, _, polygons) = SelectedPolygons(2);
        var before = PolygonVertices.CopyPoints(polygons[0]);
        PolygonVertices.Set(polygons[0], 2, 12, 20); // vertex 2 was (8, 8)

        ctx.AppCommands.CommitPolygonPoints(polygons[0], before, PolygonVertexEdit.Move(2));

        PolygonVertices.Get(polygons[1], 2).ShouldBe((13f, 20f));
        ctx.UndoManager.UndoHistory.Count.ShouldBe(1);
        ctx.UndoManager.Undo();
        PolygonVertices.Get(polygons[0], 2).ShouldBe((8f, 8f));
        PolygonVertices.Get(polygons[1], 2).ShouldBe((9f, 8f));
    }

    [Fact]
    public void CommitPolygonPoints_LiveMidpointInsert_InsertsOnEverySelectedPolygonWithTheDragOffset()
    {
        var (ctx, _, polygons) = SelectedPolygons(2);
        var before = PolygonVertices.CopyPoints(polygons[0]);
        PolygonVertices.Insert(polygons[0], 1, 0, -14); // edge midpoint (0,-8), dragged 6 down

        ctx.AppCommands.CommitPolygonPoints(polygons[0], before, PolygonVertexEdit.Insert(1));

        PolygonVertices.Count(polygons[1]).ShouldBe(5);
        PolygonVertices.Get(polygons[1], 1).ShouldBe((1f, -14f));
        ctx.UndoManager.Undo();
        PolygonVertices.Count(polygons[1]).ShouldBe(4);
    }
}
