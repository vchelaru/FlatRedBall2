using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
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
