using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using AnimationEditor.App.Controls;
using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Polygon collision shapes: adding one from the frame menu, editing its vertices in the preview
/// (drag a vertex, press an edge midpoint to add one, double-click to delete one) and in the
/// inspector, and the saved file keeping FRB1's repeated closing point.
/// </summary>
public class PolygonScenarioTests
{
    private static async Task<(AnimationEditorHarness Editor, string Path, AnimationFrameSave Frame, PolygonSave Polygon)> OpenWithNewPolygonAsync()
    {
        var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        AnimationFrameSave frame = walk.Frames[0];
        editor.Expand(walk);
        editor.RightClickRow(frame);
        editor.PickTreeMenuItem("Add Polygon");
        PolygonSave polygon = frame.ShapesSave!.PolygonSaves.ShouldHaveSingleItem();
        return (editor, path, frame, polygon);
    }

    [AvaloniaFact]
    public async Task AddPolygon_FromTheFrameMenu_ShowsItsInspector_AndSavesAClosedSquare()
    {
        var (editor, path, _, polygon) = await OpenWithNewPolygonAsync();
        using var _ = editor;

        editor.Services.SelectedState.SelectedPolygon.ShouldBeSameAs(polygon);
        editor.Control<Control>("PropPolygonPanel").IsVisible.ShouldBeTrue();
        editor.NodeFor(polygon).Header.ShouldBe(polygon.Name);
        var saved = AnimationEditorHarness.ReadSaved(path).AnimationChains[0].Frames[0].ShapesSave!.PolygonSaves.Single();
        saved.Points.Select(p => (p.X, p.Y)).ShouldBe(new[] { (-8f, -8f), (8f, -8f), (8f, 8f), (-8f, 8f), (-8f, -8f) });
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task DraggingAVertexInThePreview_MovesIt_AndOneUndoPutsItBack()
    {
        var (editor, _, _, polygon) = await OpenWithNewPolygonAsync();
        using var _ = editor;

        editor.Drag(editor.PreviewPointAt(8, 8), editor.PreviewPointAt(20, 14));

        PolygonVertices.Get(polygon, 2).ShouldBe((20f, 14f));
        editor.UndoLabels[^1].ShouldBe("Move Vertex 3 of Polygon 'PolygonInstance'");
        editor.Press(Key.Z, RawInputModifiers.Control);
        PolygonVertices.Get(polygon, 2).ShouldBe((8f, 8f));
    }

    [AvaloniaFact]
    public async Task DraggingTheFirstVertex_MovesTheSavedClosingPointToo()
    {
        var (editor, path, _, polygon) = await OpenWithNewPolygonAsync();
        using var _ = editor;

        editor.Drag(editor.PreviewPointAt(-8, -8), editor.PreviewPointAt(-12, -10));

        var saved = AnimationEditorHarness.ReadSaved(path).AnimationChains[0].Frames[0].ShapesSave!.PolygonSaves.Single();
        (saved.Points[^1].X, saved.Points[^1].Y).ShouldBe((-12f, -10f));
        saved.Points.Count.ShouldBe(5);
    }

    [AvaloniaFact]
    public async Task PressingAnEdgeMidpoint_AddsAVertexThere_AndDragsIt()
    {
        var (editor, _, _, polygon) = await OpenWithNewPolygonAsync();
        using var _ = editor;

        // The bottom edge runs from vertex 1 (-8,-8) to vertex 2 (8,-8); its midpoint is (0,-8).
        editor.Drag(editor.PreviewPointAt(0, -8), editor.PreviewPointAt(0, -14));

        PolygonVertices.Count(polygon).ShouldBe(5);
        PolygonVertices.Get(polygon, 1).ShouldBe((0f, -14f));
        editor.UndoLabels[^1].ShouldBe("Add Vertex to Polygon 'PolygonInstance'");
    }

    // Two frames with a polygon each, both Ctrl-selected in the tree. Returns the one the preview
    // shows (the one a drag edits) first.
    private static async Task<(AnimationEditorHarness Editor, PolygonSave First, PolygonSave Second)> OpenWithTwoSelectedPolygonsAsync()
    {
        var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        var polygons = new List<PolygonSave>();
        foreach (var frame in walk.Frames)
        {
            editor.RightClickRow(frame);
            editor.PickTreeMenuItem("Add Polygon");
            polygons.Add(frame.ShapesSave!.PolygonSaves.Single());
        }
        editor.ClickRow(polygons[1]);
        editor.ClickRow(polygons[0], RawInputModifiers.Control);
        editor.Services.SelectedState.SelectedPolygons.Count.ShouldBe(2);
        var shown = editor.Services.SelectedState.SelectedPolygon.ShouldNotBeNull();
        return (editor, shown, polygons.Single(p => !ReferenceEquals(p, shown)));
    }

    [AvaloniaFact]
    public async Task DraggingAVertex_WithTwoPolygonsSelected_MovesTheSameVertexOnBoth_InOneUndo()
    {
        var (editor, first, second) = await OpenWithTwoSelectedPolygonsAsync();
        using var _ = editor;

        editor.Drag(editor.PreviewPointAt(8, 8), editor.PreviewPointAt(20, 14));

        PolygonVertices.Get(first, 2).ShouldBe((20f, 14f));
        PolygonVertices.Get(second, 2).ShouldBe((20f, 14f));
        editor.UndoLabels[^1].ShouldBe("Move Vertex 3 of 2 Polygons");
        editor.Press(Key.Z, RawInputModifiers.Control);
        PolygonVertices.Get(second, 2).ShouldBe((8f, 8f));
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task DraggingAVertex_SkipsASelectedPolygonWithADifferentVertexCount_AndSaysSo()
    {
        var (editor, first, second) = await OpenWithTwoSelectedPolygonsAsync();
        using var _ = editor;
        PolygonVertices.Insert(second, 1, 0, -10);

        editor.Drag(editor.PreviewPointAt(8, 8), editor.PreviewPointAt(20, 14));

        PolygonVertices.Get(first, 2).ShouldBe((20f, 14f));
        PolygonVertices.Get(second, 3).ShouldBe((8f, 8f));
        editor.ToastText.ShouldBe("Moved vertex on 1 of 2 polygons, 1 skipped: different vertex count");
    }

    [AvaloniaFact]
    public async Task DoubleClickingAVertex_DeletesIt_ButNeverBelowThree()
    {
        var (editor, _, _, polygon) = await OpenWithNewPolygonAsync();
        using var _ = editor;

        editor.DoubleClickAt(editor.PreviewPointAt(8, 8));
        PolygonVertices.Count(polygon).ShouldBe(3);
        editor.DoubleClickAt(editor.PreviewPointAt(8, -8));

        PolygonVertices.Count(polygon).ShouldBe(3, "a triangle keeps its last three vertices");
    }

    [AvaloniaFact]
    public async Task TypingAVertexCoordinateInTheInspector_MovesThatVertex()
    {
        var (editor, _, _, polygon) = await OpenWithNewPolygonAsync();
        using var _ = editor;

        editor.TypeNumber("PropPolygonVertex1X", "12");

        PolygonVertices.Get(polygon, 1).ShouldBe((12f, -8f));
    }

    [AvaloniaFact]
    public async Task HoveringAVertexInThePreview_HighlightsIt_UntilThePointerLeavesIt()
    {
        var (editor, _, _, _) = await OpenWithNewPolygonAsync();
        using var _ = editor;

        editor.Hover(editor.PreviewPointAt(8, 8));
        HighlightedVertex(editor).ShouldBe(2);

        editor.Hover(editor.PreviewPointAt(0, 0));
        HighlightedVertex(editor).ShouldBe(-1);
    }

    [AvaloniaFact]
    public async Task FocusingAVertexRowInTheInspector_HighlightsThatVertex()
    {
        var (editor, _, _, _) = await OpenWithNewPolygonAsync();
        using var _ = editor;

        editor.TypeNumber("PropPolygonVertex1Y", "-8");

        HighlightedVertex(editor).ShouldBe(1);
    }

    [AvaloniaFact]
    public async Task FocusingAVertexRow_StartsItsHighlightEnlarged_ThenSettles_ButHoverDoesNot()
    {
        var (editor, _, _, _) = await OpenWithNewPolygonAsync();
        using var _ = editor;

        editor.TypeNumber("PropPolygonVertex1Y", "-8");
        PolygonShape(editor).HighlightInflation.ShouldBeGreaterThan(0f);

        editor.Preview.SettleVertexReveal();
        PolygonShape(editor).HighlightInflation.ShouldBe(0f);

        editor.Hover(editor.PreviewPointAt(8, 8));
        PolygonShape(editor).HighlightInflation.ShouldBe(0f);
    }

    [AvaloniaFact]
    public async Task TabbingFromAVertexXToItsY_DoesNotReplayTheReveal()
    {
        var (editor, _, _, _) = await OpenWithNewPolygonAsync();
        using var _ = editor;
        editor.TypeNumber("PropPolygonVertex1X", "8");
        editor.Preview.SettleVertexReveal();

        editor.Press(Key.Tab);

        editor.Control<NumericUpDown>("PropPolygonVertex1Y").IsKeyboardFocusWithin.ShouldBeTrue();
        HighlightedVertex(editor).ShouldBe(1);
        PolygonShape(editor).HighlightInflation.ShouldBe(0f);
    }

    private static PreviewControl.PreviewShapeInfo PolygonShape(AnimationEditorHarness editor) =>
        editor.Preview.GetShapeInfosForTest().Single(s => s.Kind == PreviewControl.PreviewShapeKind.Polygon);

    private static int HighlightedVertex(AnimationEditorHarness editor) => PolygonShape(editor).HighlightedVertex;

    [AvaloniaFact]
    public async Task DraggingAVertexAcrossTheOutline_ShowsTheSelfIntersectionWarning()
    {
        var (editor, _, _, _) = await OpenWithNewPolygonAsync();
        using var _ = editor;
        editor.Control<TextBlock>("PropPolygonWarning").IsVisible.ShouldBeFalse();

        // Pulling the top-right corner past the left edge crosses the outline into a bowtie.
        editor.Drag(editor.PreviewPointAt(8, 8), editor.PreviewPointAt(-14, -2));

        editor.Control<TextBlock>("PropPolygonWarning").IsVisible.ShouldBeTrue();
    }
}
