using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Dragging one of several multi-selected shapes in the preview moves them all together (#1324).
/// </summary>
public class MultiShapeDragScenarioTests
{
    private static async Task<(AnimationEditorHarness Editor, AARectSave Rect, CircleSave Circle)> OpenWithTwoSelectedShapesAsync()
    {
        var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        AnimationFrameSave frame = walk.Frames[0];
        editor.Expand(walk);
        editor.RightClickRow(frame);
        editor.PickTreeMenuItem("Add AxisAlignedRectangle");
        editor.RightClickRow(frame);
        editor.PickTreeMenuItem("Add Circle");
        AARectSave rect = frame.ShapesSave!.AARectSaves.Single();
        CircleSave circle = frame.ShapesSave!.CircleSaves.Single();
        rect.X = -20; rect.Y = 0;
        circle.X = 20; circle.Y = 5;
        editor.Expand(frame);
        editor.ClickRow(rect);
        editor.ClickRow(circle, RawInputModifiers.Control);
        editor.Services.SelectedState.SelectedShapes.Count.ShouldBe(2);
        return (editor, rect, circle);
    }

    [AvaloniaFact]
    public async Task DragOneOfTwoSelectedShapes_MovesBoth_AsOneUndoStep()
    {
        var (editor, rect, circle) = await OpenWithTwoSelectedShapesAsync();
        using var _ = editor;
        int undoCount = editor.UndoLabels.Count;

        editor.Drag(editor.PreviewPointAt(-20, 0), editor.PreviewPointAt(-14, 4));

        (rect.X, rect.Y).ShouldBe((-14f, 4f));
        (circle.X, circle.Y).ShouldBe((26f, 9f));
        editor.Services.SelectedState.SelectedShapes.Count.ShouldBe(2);
        editor.UndoLabels.Count.ShouldBe(undoCount + 1);
        editor.UndoLabels.ShouldContain("Move 2 Shapes");

        editor.Press(Key.Z, RawInputModifiers.Control);

        (rect.X, rect.Y).ShouldBe((-20f, 0f));
        (circle.X, circle.Y).ShouldBe((20f, 5f));
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task ClickWithoutDragOnOneOfTwoSelectedShapes_SelectsOnlyThatShape()
    {
        var (editor, rect, _) = await OpenWithTwoSelectedShapesAsync();
        using var _ = editor;

        editor.ClickAt(editor.PreviewPointAt(-20, 0));

        editor.Services.SelectedState.SelectedShapes.ShouldBe(new object[] { rect });
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task EscapeDuringAMultiShapeDrag_PutsEveryShapeBack_WithNoUndoEntry()
    {
        var (editor, rect, circle) = await OpenWithTwoSelectedShapesAsync();
        using var _ = editor;
        int undoCount = editor.UndoLabels.Count;
        Point from = editor.PreviewPointAt(-20, 0);
        Point to = editor.PreviewPointAt(-8, 9);

        editor.Window.MouseMove(from, RawInputModifiers.None);
        editor.Window.MouseDown(from, MouseButton.Left, RawInputModifiers.None);
        editor.Window.MouseMove(to, RawInputModifiers.LeftMouseButton);
        editor.Press(Key.Escape);
        editor.Window.MouseUp(to, MouseButton.Left, RawInputModifiers.None);
        editor.Layout();

        (rect.X, rect.Y).ShouldBe((-20f, 0f));
        (circle.X, circle.Y).ShouldBe((20f, 5f));
        editor.UndoLabels.Count.ShouldBe(undoCount);
        editor.ThrowIfErrorShown();
    }
}
