using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Holding Shift while moving a shape in the Preview locks the drag to one axis (#1820, same
/// convention as #1022's frame drags), for every shape type and for a multi-selection.
/// </summary>
public class ShapeShiftDragScenarioTests
{
    private static async Task<(AnimationEditorHarness Editor, AnimationFrameSave Frame)> OpenAsync(string addMenuItem)
    {
        var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.RightClickRow(walk.Frames[0]);
        editor.PickTreeMenuItem(addMenuItem);
        return (editor, walk.Frames[0]);
    }

    [AvaloniaFact]
    public async Task ShiftDragCircle_HorizontalLarger_KeepsYFixed()
    {
        var (editor, frame) = await OpenAsync("Add Circle");
        using var _ = editor;
        CircleSave circle = frame.ShapesSave!.CircleSaves.Single();
        circle.X = 0; circle.Y = 5; circle.Radius = 10;

        editor.Drag(editor.PreviewPointAt(0, 5), editor.PreviewPointAt(12, 9), RawInputModifiers.Shift);

        (circle.X, circle.Y).ShouldBe((12f, 5f));
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task ShiftDragCircle_VerticalLarger_KeepsXFixed()
    {
        var (editor, frame) = await OpenAsync("Add Circle");
        using var _ = editor;
        CircleSave circle = frame.ShapesSave!.CircleSaves.Single();
        circle.X = 3; circle.Y = 5; circle.Radius = 10;

        editor.Drag(editor.PreviewPointAt(3, 5), editor.PreviewPointAt(7, 17), RawInputModifiers.Shift);

        (circle.X, circle.Y).ShouldBe((3f, 17f));
    }

    [AvaloniaFact]
    public async Task DragCircleWithoutShift_MovesFreely()
    {
        var (editor, frame) = await OpenAsync("Add Circle");
        using var _ = editor;
        CircleSave circle = frame.ShapesSave!.CircleSaves.Single();
        circle.X = 0; circle.Y = 5; circle.Radius = 10;

        editor.Drag(editor.PreviewPointAt(0, 5), editor.PreviewPointAt(12, 9));

        (circle.X, circle.Y).ShouldBe((12f, 9f));
    }

    [AvaloniaFact]
    public async Task ShiftDragRectangle_LocksToTheLargerAxis()
    {
        var (editor, frame) = await OpenAsync("Add AxisAlignedRectangle");
        using var _ = editor;
        AARectSave rect = frame.ShapesSave!.AARectSaves.Single();
        rect.X = 0; rect.Y = 5;

        editor.Drag(editor.PreviewPointAt(0, 5), editor.PreviewPointAt(12, 9), RawInputModifiers.Shift);

        (rect.X, rect.Y).ShouldBe((12f, 5f));
    }

    [AvaloniaFact]
    public async Task ShiftDragPolygon_LocksToTheLargerAxis()
    {
        var (editor, frame) = await OpenAsync("Add Polygon");
        using var _ = editor;
        PolygonSave polygon = frame.ShapesSave!.PolygonSaves.Single();
        polygon.X = 0; polygon.Y = 5;

        editor.Drag(editor.PreviewPointAt(0, 5), editor.PreviewPointAt(4, 17), RawInputModifiers.Shift);

        (polygon.X, polygon.Y).ShouldBe((0f, 17f));
    }

    [AvaloniaFact]
    public async Task ShiftDragOneOfTwoSelectedShapes_LocksBothToTheSameAxis()
    {
        var (editor, frame) = await OpenAsync("Add AxisAlignedRectangle");
        using var _ = editor;
        editor.RightClickRow(frame);
        editor.PickTreeMenuItem("Add Circle");
        AARectSave rect = frame.ShapesSave!.AARectSaves.Single();
        CircleSave circle = frame.ShapesSave!.CircleSaves.Single();
        rect.X = -20; rect.Y = 0;
        circle.X = 20; circle.Y = 5;
        editor.Expand(frame);
        editor.ClickRow(rect);
        editor.ClickRow(circle, RawInputModifiers.Control);

        editor.Drag(editor.PreviewPointAt(-20, 0), editor.PreviewPointAt(-14, 4), RawInputModifiers.Shift);

        (rect.X, rect.Y).ShouldBe((-14f, 0f));
        (circle.X, circle.Y).ShouldBe((26f, 5f));
    }
}
