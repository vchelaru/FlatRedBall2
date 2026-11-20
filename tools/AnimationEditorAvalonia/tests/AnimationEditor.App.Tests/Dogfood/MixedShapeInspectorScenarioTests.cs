using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>A rectangle and a circle selected together show one SHAPES panel with only the shared fields (#1825).</summary>
public class MixedShapeInspectorScenarioTests
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
        rect.Name = "Body"; circle.Name = "Head";
        rect.X = -20; rect.Y = 5;
        circle.X = 20; circle.Y = 5;
        editor.Expand(frame);
        editor.ClickRow(rect);
        editor.ClickRow(circle, RawInputModifiers.Control);
        return (editor, rect, circle);
    }

    [AvaloniaFact]
    public async Task RectangleAndCircle_ShowSharedPanel_WithMixedX_AndCommonY()
    {
        var (editor, _, _) = await OpenAsync();
        using var _e = editor;

        editor.Control<Control>("PropShapesPanel").IsVisible.ShouldBeTrue();
        editor.Control<Control>("PropRectPanel").IsVisible.ShouldBeFalse();
        editor.Control<Control>("PropCirclePanel").IsVisible.ShouldBeFalse();
        var x = editor.Control<NumericUpDown>("PropShapesX");
        x.Value.ShouldBeNull();
        x.PlaceholderText.ShouldBe("(mixed)");
        editor.Control<NumericUpDown>("PropShapesY").Value.ShouldBe(5m);
        // Same frame: one name on both would collide, so the box is disabled like the same-type panels.
        editor.Control<TextBox>("PropShapesName").IsEnabled.ShouldBeFalse();
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task TypingX_SetsBothShapes_AndOneUndoRestoresBoth()
    {
        var (editor, rect, circle) = await OpenAsync();
        using var _e = editor;

        editor.TypeNumber("PropShapesX", "7");

        rect.X.ShouldBe(7f);
        circle.X.ShouldBe(7f);
        rect.Y.ShouldBe(5f);
        editor.Press(Key.Z, RawInputModifiers.Control);
        rect.X.ShouldBe(-20f);
        circle.X.ShouldBe(20f);
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task TypingY_SetsBothShapes()
    {
        var (editor, rect, circle) = await OpenAsync();
        using var _e = editor;

        editor.TypeNumber("PropShapesY", "9");

        rect.Y.ShouldBe(9f);
        circle.Y.ShouldBe(9f);
        rect.X.ShouldBe(-20f);
        circle.X.ShouldBe(20f);
    }

    [AvaloniaFact]
    public async Task SingleTypeSelection_StillShowsItsOwnPanel()
    {
        var (editor, rect, _) = await OpenAsync();
        using var _e = editor;

        editor.ClickRow(rect);

        editor.Control<Control>("PropShapesPanel").IsVisible.ShouldBeFalse();
        editor.Control<Control>("PropRectPanel").IsVisible.ShouldBeTrue();
    }
}
