using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Collision shapes on a frame: adding through the frame's context menu, editing in the
/// inspector, renaming inline with F2, deleting with the keyboard, and undo.
/// </summary>
public class ShapeScenarioTests
{
    [AvaloniaFact]
    public async Task AddAxisAlignedRectangle_FromTheFrameMenu_AddsAndSelectsIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        AnimationFrameSave frame = walk.Frames[0];
        editor.Expand(walk);

        editor.RightClickRow(frame);
        editor.PickTreeMenuItem("Add AxisAlignedRectangle");

        AARectSave rect = frame.ShapesSave!.AARectSaves.ShouldHaveSingleItem();
        editor.Services.SelectedState.SelectedRectangle.ShouldBeSameAs(rect);
        editor.Control<Control>("PropRectPanel").IsVisible.ShouldBeTrue();
        editor.NodeFor(rect).Header.ShouldBe(rect.Name);
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task AddCircle_ThenTypingARadius_UpdatesTheCircle_AndUndoRestoresIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        AnimationFrameSave frame = walk.Frames[0];
        editor.Expand(walk);
        editor.RightClickRow(frame);
        editor.PickTreeMenuItem("Add Circle");
        CircleSave circle = frame.ShapesSave!.CircleSaves.ShouldHaveSingleItem();
        float radiusBefore = circle.Radius;

        editor.TypeNumber("PropCircleRadius", "12");

        circle.Radius.ShouldBe(12);
        editor.Press(Key.Z, RawInputModifiers.Control);
        circle.Radius.ShouldBe(radiusBefore);
    }

    [AvaloniaFact]
    public async Task DeleteKey_OnARectangle_RemovesIt_AndUndoBringsItBack()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        AnimationFrameSave frame = walk.Frames[0];
        editor.Expand(walk);
        editor.RightClickRow(frame);
        editor.PickTreeMenuItem("Add AxisAlignedRectangle");
        AARectSave rect = frame.ShapesSave!.AARectSaves.Single();
        editor.ClickRow(rect);

        editor.Press(Key.Delete);

        frame.ShapesSave!.AARectSaves.ShouldBeEmpty();
        editor.Press(Key.Z, RawInputModifiers.Control);
        frame.ShapesSave!.AARectSaves.ShouldBe(new[] { rect });
    }

    [AvaloniaFact]
    public async Task F2_OnARectangle_RenamesItInline()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        AnimationFrameSave frame = walk.Frames[0];
        editor.Expand(walk);
        editor.RightClickRow(frame);
        editor.PickTreeMenuItem("Add AxisAlignedRectangle");
        AARectSave rect = frame.ShapesSave!.AARectSaves.Single();
        editor.ClickRow(rect);

        editor.Press(Key.F2);
        editor.Wait(TimeSpan.FromMilliseconds(50));
        editor.NodeFor(rect).IsEditing.ShouldBeTrue();
        editor.Type("Hitbox");
        editor.Press(Key.Enter);

        rect.Name.ShouldBe("Hitbox");
        editor.NodeFor(rect).Header.ShouldBe("Hitbox");
        editor.Press(Key.Z, RawInputModifiers.Control);
        rect.Name.ShouldNotBe("Hitbox");
    }

    [AvaloniaFact]
    public async Task RectangleXField_TypingAValue_MovesTheRectangle_AndSaves()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        AnimationFrameSave frame = walk.Frames[0];
        editor.Expand(walk);
        editor.RightClickRow(frame);
        editor.PickTreeMenuItem("Add AxisAlignedRectangle");
        AARectSave rect = frame.ShapesSave!.AARectSaves.Single();

        editor.TypeNumber("PropRectX", "5");
        editor.Press(Key.S, RawInputModifiers.Control);

        rect.X.ShouldBe(5);
        AnimationEditorHarness.ReadSaved(path).AnimationChains.Single().Frames.Single()
            .ShapesSave!.AARectSaves.Single().X.ShouldBe(5);
    }
}
