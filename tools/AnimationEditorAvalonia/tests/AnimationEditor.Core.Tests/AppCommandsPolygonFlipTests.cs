using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>Flip Horizontal / Flip Vertical on polygons (issue #1286).</summary>
[Collection("SequentialSingletons")]
public class AppCommandsPolygonFlipTests
{
    // Closed outline whose bounds are x 0..10, y 0..4, so the flip axes are x = 5 and y = 2.
    private static PolygonSave AddLShape(TestServices ctx, AnimationFrameSave frame)
    {
        ctx.AppCommands.AddPolygon(frame);
        var polygon = frame.ShapesSave!.PolygonSaves.Last();
        polygon.X = 7;
        polygon.Y = 9;
        polygon.Points = new List<Vector2Save>
        {
            new() { X = 0, Y = 0 }, new() { X = 10, Y = 0 }, new() { X = 10, Y = 1 },
            new() { X = 2, Y = 1 }, new() { X = 2, Y = 4 }, new() { X = 0, Y = 4 },
            new() { X = 0, Y = 0 },
        };
        return polygon;
    }

    private static (TestServices Ctx, List<PolygonSave> Polygons) Polygons(int count)
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", count);
        var polygons = new List<PolygonSave>();
        foreach (var frame in chain.Frames)
        {
            frame.ShapesSave = null;
            polygons.Add(AddLShape(ctx, frame));
        }
        ctx.UndoManager.Clear();
        ctx.SelectedState.SelectedPolygon = polygons[0];
        return (ctx, polygons);
    }

    private static IEnumerable<(float X, float Y)> Outline(PolygonSave polygon) =>
        polygon.Points.Select(p => (p.X, p.Y));

    [Fact]
    public void FlipPolygonHorizontally_MirrorsPointsAboutTheirBoundsCenter_KeepsOriginAndClosingPoint()
    {
        var (ctx, polygons) = Polygons(1);
        var polygon = polygons[0];

        ctx.AppCommands.FlipPolygonHorizontally(polygon);

        Outline(polygon).ShouldBe(new[]
        {
            (10f, 0f), (0f, 0f), (0f, 1f), (8f, 1f), (8f, 4f), (10f, 4f), (10f, 0f),
        });
        polygon.X.ShouldBe(7f);
        polygon.Y.ShouldBe(9f);
        PolygonVertices.IsClosed(polygon).ShouldBeTrue();
    }

    [Fact]
    public void FlipPolygonVertically_MirrorsPointsAboutTheirBoundsCenter()
    {
        var (ctx, polygons) = Polygons(1);
        var polygon = polygons[0];

        ctx.AppCommands.FlipPolygonVertically(polygon);

        Outline(polygon).ShouldBe(new[]
        {
            (0f, 4f), (10f, 4f), (10f, 3f), (2f, 3f), (2f, 0f), (0f, 0f), (0f, 4f),
        });
        polygon.X.ShouldBe(7f);
        polygon.Y.ShouldBe(9f);
    }

    [Fact]
    public void FlipPolygonHorizontally_Twice_RestoresTheOutline()
    {
        var (ctx, polygons) = Polygons(1);
        var original = Outline(polygons[0]).ToList();

        ctx.AppCommands.FlipPolygonHorizontally(polygons[0]);
        ctx.AppCommands.FlipPolygonHorizontally(polygons[0]);

        Outline(polygons[0]).ShouldBe(original);
    }

    [Fact]
    public void FlipPolygonHorizontally_IsOneUndoStepWithADescriptiveLabel_AndUndoRedoRoundTrips()
    {
        var (ctx, polygons) = Polygons(1);
        var original = Outline(polygons[0]).ToList();

        ctx.AppCommands.FlipPolygonHorizontally(polygons[0]);
        var flipped = Outline(polygons[0]).ToList();

        ctx.UndoManager.UndoHistory.Count.ShouldBe(1);
        ctx.UndoManager.UndoHistory[^1].Description.ShouldStartWith("Flip Horizontal");
        ctx.UndoManager.Undo();
        Outline(polygons[0]).ShouldBe(original);
        ctx.UndoManager.Redo();
        Outline(polygons[0]).ShouldBe(flipped);
    }

    [Fact]
    public void FlipPolygonHorizontally_OnASelection_FlipsEverySelectedPolygonAboutItsOwnCenter_InOneUndoStep()
    {
        var (ctx, polygons) = Polygons(3);
        foreach (var point in polygons[1].Points) point.X += 100; // bounds x 100..110
        ctx.SelectedState.SelectedNodes = polygons.Cast<object>().ToList();

        ctx.AppCommands.FlipPolygonHorizontally(polygons[0]);

        Outline(polygons[0]).First().ShouldBe((10f, 0f));
        Outline(polygons[1]).First().ShouldBe((110f, 0f));
        Outline(polygons[2]).First().ShouldBe((10f, 0f));
        ctx.UndoManager.UndoHistory.Count.ShouldBe(1);
        ctx.UndoManager.UndoHistory[^1].Description.ShouldBe("Flip Horizontal 3 Polygons");
        ctx.UndoManager.Undo();
        Outline(polygons[2]).First().ShouldBe((0f, 0f));
    }

    [Fact]
    public void FlipPolygonHorizontally_OnAPolygonOutsideTheSelection_FlipsOnlyThatPolygon()
    {
        var (ctx, polygons) = Polygons(2);
        ctx.SelectedState.SelectedPolygon = polygons[1];

        ctx.AppCommands.FlipPolygonHorizontally(polygons[0]);

        Outline(polygons[0]).First().ShouldBe((10f, 0f));
        Outline(polygons[1]).First().ShouldBe((0f, 0f));
    }

    [Fact]
    public void FlipPolygonHorizontally_InALockedChain_ChangesNothing()
    {
        var (ctx, polygons) = Polygons(1);
        ctx.ObjectFinder.GetAnimationChainContaining(
            ctx.ObjectFinder.GetAnimationFrameContaining(polygons[0])!)!.IsLocked = true;

        ctx.AppCommands.FlipPolygonHorizontally(polygons[0]);

        Outline(polygons[0]).First().ShouldBe((0f, 0f));
        ctx.UndoManager.UndoHistory.Count.ShouldBe(0);
    }

    [Fact]
    public void TreeMenu_PolygonNode_OffersFlipHorizontalAndVertical_ThatFlipTheOutline()
    {
        var (ctx, polygons) = Polygons(1);
        var actions = new TreeMenuActions(
            Copy: () => { }, Cut: () => { }, Paste: () => { }, Duplicate: () => { }, Delete: () => { },
            Rename: () => { }, AddAnimation: () => { }, DuplicateChainFlip: (_, _) => { });

        var items = TreeMenuPlanBuilder.Build(
            polygons[0], ctx.AppCommands, ctx.SelectedState, ctx.ObjectFinder, ctx.ProjectManager, actions);

        var flipH = items.Single(i => i.Header == "Flip Horizontal");
        var flipV = items.Single(i => i.Header == "Flip Vertical");
        flipH.OnClick!();
        Outline(polygons[0]).First().ShouldBe((10f, 0f));
        flipV.OnClick!();
        Outline(polygons[0]).First().ShouldBe((10f, 4f));
    }
}
