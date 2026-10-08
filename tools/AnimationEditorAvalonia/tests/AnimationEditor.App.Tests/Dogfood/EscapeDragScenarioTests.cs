using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Escape during any drag in the preview (shape, polygon vertex, frame and whole-animation
/// offset, guide) puts everything back and leaves no undo entry. The wireframe's drags are in
/// <see cref="QaScenarioTests"/>.
/// </summary>
public class EscapeDragScenarioTests
{
    private static async Task<(AnimationEditorHarness Editor, AnimationChainSave Chain, AnimationFrameSave Frame)> OpenAsync(int frames = 1)
    {
        var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        var regions = Enumerable.Range(0, frames).Select(i => (i * 16, 0, 16, 16)).ToArray();
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", regions));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        return (editor, walk, walk.Frames[0]);
    }

    /// <summary>Presses at <paramref name="from"/>, drags to <paramref name="to"/>, presses Escape, then releases.</summary>
    private static void DragThenEscape(AnimationEditorHarness editor, Point from, Point to)
    {
        editor.Window.MouseMove(from, RawInputModifiers.None);
        editor.Window.MouseDown(from, MouseButton.Left, RawInputModifiers.None);
        editor.Window.MouseMove(to, RawInputModifiers.LeftMouseButton);
        editor.Press(Key.Escape);
        editor.Window.MouseMove(new Point(to.X + 6, to.Y + 6), RawInputModifiers.LeftMouseButton);
        editor.Window.MouseUp(to, MouseButton.Left, RawInputModifiers.None);
        editor.Layout();
    }

    [AvaloniaFact]
    public async Task EscapeDuringAVertexDrag_PutsTheVertexBack_WithNoUndoEntry()
    {
        var (editor, _, frame) = await OpenAsync();
        using var _ = editor;
        editor.RightClickRow(frame);
        editor.PickTreeMenuItem("Add Polygon");
        PolygonSave polygon = frame.ShapesSave!.PolygonSaves.Single();
        int undoCount = editor.UndoLabels.Count;

        DragThenEscape(editor, editor.PreviewPointAt(8, 8), editor.PreviewPointAt(20, 14));

        PolygonVertices.Get(polygon, 2).ShouldBe((8f, 8f));
        editor.UndoLabels.Count.ShouldBe(undoCount);
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task EscapeDuringAnInsertedVertexDrag_RemovesTheInsertedVertex()
    {
        var (editor, _, frame) = await OpenAsync();
        using var _ = editor;
        editor.RightClickRow(frame);
        editor.PickTreeMenuItem("Add Polygon");
        PolygonSave polygon = frame.ShapesSave!.PolygonSaves.Single();
        int undoCount = editor.UndoLabels.Count;

        DragThenEscape(editor, editor.PreviewPointAt(0, -8), editor.PreviewPointAt(0, -14));

        PolygonVertices.Count(polygon).ShouldBe(4);
        editor.UndoLabels.Count.ShouldBe(undoCount);
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task EscapeDuringAShapeDrag_PutsTheRectangleBack_WithNoUndoEntry()
    {
        var (editor, _, frame) = await OpenAsync();
        using var _ = editor;
        editor.RightClickRow(frame);
        editor.PickTreeMenuItem("Add AxisAlignedRectangle");
        AARectSave rect = frame.ShapesSave!.AARectSaves.Single();
        (float x, float y) = (rect.X, rect.Y);
        int undoCount = editor.UndoLabels.Count;

        DragThenEscape(editor, editor.PreviewPointAt(x, y), editor.PreviewPointAt(x + 12, y + 9));

        (rect.X, rect.Y).ShouldBe((x, y));
        editor.UndoLabels.Count.ShouldBe(undoCount);
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task EscapeDuringAFrameOffsetDrag_PutsTheOffsetBack_WithNoUndoEntry()
    {
        var (editor, _, frame) = await OpenAsync();
        using var _ = editor;
        editor.ClickRow(frame);
        (float x, float y) = (frame.RelativeX, frame.RelativeY);
        int undoCount = editor.UndoLabels.Count;

        DragThenEscape(editor, editor.PreviewPointAt(x, y), editor.PreviewPointAt(x + 12, y + 9));

        (frame.RelativeX, frame.RelativeY).ShouldBe((x, y));
        editor.UndoLabels.Count.ShouldBe(undoCount);
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task EscapeDuringAWholeAnimationDrag_PutsEveryFrameBack_WithNoUndoEntry()
    {
        var (editor, walk, _) = await OpenAsync(frames: 2);
        using var _ = editor;
        editor.ClickRow(walk);
        var starts = walk.Frames.Select(f => (f.RelativeX, f.RelativeY)).ToArray();
        int undoCount = editor.UndoLabels.Count;

        DragThenEscape(editor, editor.PreviewPointAt(0, 0), editor.PreviewPointAt(12, 9));

        walk.Frames.Select(f => (f.RelativeX, f.RelativeY)).ShouldBe(starts);
        editor.UndoLabels.Count.ShouldBe(undoCount);
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task EscapeDuringANewGuideDrag_RemovesTheGuide()
    {
        var (editor, walk, _) = await OpenAsync();
        using var _ = editor;
        editor.ClickRow(walk);
        Point ruler = editor.PointIn(editor.Preview, 2, 120);

        DragThenEscape(editor, ruler, editor.PointIn(editor.Preview, 60, 140));

        editor.Preview.HGuideCount.ShouldBe(0);
        editor.ThrowIfErrorShown();
    }
}
