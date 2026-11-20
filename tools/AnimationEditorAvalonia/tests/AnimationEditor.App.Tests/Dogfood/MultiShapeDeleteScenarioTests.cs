using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>Delete removes every multi-selected shape as one undo step; one Undo restores them and the selection (#1824).</summary>
public class MultiShapeDeleteScenarioTests
{
    private static async Task<(AnimationEditorHarness Editor, AnimationFrameSave Frame, AARectSave Rect, CircleSave Circle)> OpenAsync()
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
        return (editor, frame, rect, circle);
    }

    private static void AssertDeletedThenOneUndoRestores(
        AnimationEditorHarness editor, AnimationFrameSave frame, AARectSave rect, CircleSave circle, int undoCountBefore)
    {
        var selected = editor.Services.SelectedState;
        frame.ShapesSave!.Shapes.ShouldBeEmpty();
        selected.SelectedShapes.ShouldBeEmpty();
        editor.UndoLabels.Count.ShouldBe(undoCountBefore + 1);
        editor.UndoLabels[^1].ShouldBe("Delete 2 Shapes");

        editor.Press(Key.Z, RawInputModifiers.Control);

        frame.ShapesSave!.AARectSaves.ShouldBe(new[] { rect });
        frame.ShapesSave!.CircleSaves.ShouldBe(new[] { circle });
        selected.SelectedShapes.ShouldBe(new object[] { rect, circle }, ignoreOrder: true);
        editor.AnimTree.SelectedItems!.Count.ShouldBe(2);
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task DeleteKey_WithTwoTreeShapesSelected_RemovesBoth_AndOneUndoRestoresBothAndSelection()
    {
        var (editor, frame, rect, circle) = await OpenAsync();
        using var _ = editor;
        editor.ClickRow(rect);
        editor.ClickRow(circle, RawInputModifiers.Control);
        int before = editor.UndoLabels.Count;

        editor.Press(Key.Delete);

        AssertDeletedThenOneUndoRestores(editor, frame, rect, circle, before);
    }

    [AvaloniaFact]
    public async Task TreeContextMenuDelete_WithTwoShapesSelected_RemovesBoth_AndOneUndoRestoresBothAndSelection()
    {
        var (editor, frame, rect, circle) = await OpenAsync();
        using var _ = editor;
        editor.ClickRow(rect);
        editor.ClickRow(circle, RawInputModifiers.Control);
        int before = editor.UndoLabels.Count;

        editor.RightClickRow(circle);
        editor.PickTreeMenuItem("Delete 2 Shapes");

        AssertDeletedThenOneUndoRestores(editor, frame, rect, circle, before);
    }

    [AvaloniaFact]
    public async Task DeleteKey_AfterPreviewCtrlClickSelection_RemovesBoth_AndOneUndoRestoresBothAndSelection()
    {
        var (editor, frame, rect, circle) = await OpenAsync();
        using var _ = editor;
        editor.ClickRow(rect);
        editor.ClickAt(editor.PreviewPointAt(20, 5), RawInputModifiers.Control);
        editor.Services.SelectedState.SelectedShapes.ShouldBe(new object[] { rect, circle });
        int before = editor.UndoLabels.Count;

        editor.Press(Key.Delete);

        AssertDeletedThenOneUndoRestores(editor, frame, rect, circle, before);
    }

    [AvaloniaFact]
    public async Task DeleteKey_WithShapesOnTwoFrames_RemovesBoth_AndOneUndoRestoresBoth()
    {
        var editor = new AnimationEditorHarness();
        using var _ = editor;
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain(
            "Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.RightClickRow(walk.Frames[0]);
        editor.PickTreeMenuItem("Add AxisAlignedRectangle");
        editor.RightClickRow(walk.Frames[1]);
        editor.PickTreeMenuItem("Add Circle");
        AARectSave rect = walk.Frames[0].ShapesSave!.AARectSaves.Single();
        CircleSave circle = walk.Frames[1].ShapesSave!.CircleSaves.Single();
        editor.Expand(walk.Frames[0]);
        editor.Expand(walk.Frames[1]);
        editor.ClickRow(rect);
        editor.ClickRow(circle, RawInputModifiers.Control);

        editor.Press(Key.Delete);

        walk.Frames[0].ShapesSave!.Shapes.ShouldBeEmpty();
        walk.Frames[1].ShapesSave!.Shapes.ShouldBeEmpty();
        editor.Services.SelectedState.SelectedShapes.ShouldBeEmpty();
        editor.Press(Key.Z, RawInputModifiers.Control);
        walk.Frames[0].ShapesSave!.AARectSaves.ShouldBe(new[] { rect });
        walk.Frames[1].ShapesSave!.CircleSaves.ShouldBe(new[] { circle });
        editor.Services.SelectedState.SelectedShapes.ShouldBe(new object[] { rect, circle }, ignoreOrder: true);
    }
}
