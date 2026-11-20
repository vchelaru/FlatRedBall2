using Avalonia.Input;
using Avalonia.Headless.XUnit;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>Ctrl+click in the preview toggles shapes in and out of the selection (#1821).</summary>
public class PreviewShapeMultiSelectScenarioTests
{
    private static async Task<(AnimationEditorHarness Editor, AARectSave Rect, CircleSave Circle)> OpenAsync()
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
        return (editor, rect, circle);
    }

    [AvaloniaFact]
    public async Task CtrlClick_AddsASecondShape_AndCtrlClickAgainRemovesIt()
    {
        var (editor, rect, circle) = await OpenAsync();
        using var _ = editor;
        var selected = editor.Services.SelectedState;

        editor.ClickAt(editor.PreviewPointAt(20, 5), RawInputModifiers.Control);
        selected.SelectedShapes.ShouldBe(new object[] { rect, circle });

        editor.ClickAt(editor.PreviewPointAt(20, 5), RawInputModifiers.Control);
        selected.SelectedShapes.ShouldBe(new object[] { rect });
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task CtrlClick_KeepsTreeRowsInSync()
    {
        var (editor, rect, circle) = await OpenAsync();
        using var _ = editor;

        editor.ClickAt(editor.PreviewPointAt(20, 5), RawInputModifiers.Control);

        editor.AnimTree.SelectedItems!.Count.ShouldBe(2);
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task PlainClick_ReplacesTheMultiSelection()
    {
        var (editor, rect, circle) = await OpenAsync();
        using var _ = editor;
        editor.ClickAt(editor.PreviewPointAt(20, 5), RawInputModifiers.Control);

        editor.ClickAt(editor.PreviewPointAt(20, 5));

        editor.Services.SelectedState.SelectedShapes.ShouldBe(new object[] { circle });
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task CtrlClick_OnEmptySpace_KeepsTheSelection()
    {
        var (editor, rect, _) = await OpenAsync();
        using var _ = editor;

        editor.ClickAt(editor.PreviewPointAt(0, 40), RawInputModifiers.Control);

        editor.Services.SelectedState.SelectedShapes.ShouldBe(new object[] { rect });
        editor.ThrowIfErrorShown();
    }
}
