using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>Redo of a delete while something inside what it deletes is selected.</summary>
public class RedoDeleteSelectionScenarioTests
{
    [AvaloniaFact]
    public async Task RedoOfAFrameDelete_WithAShapeOfThatFrameSelected_DeselectsTheShape()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        await editor.OpenAsync(path);
        var walk = editor.ChainNamed("Walk");
        var frame = walk.Frames[0];
        editor.Expand(walk);
        editor.RightClickRow(frame);
        editor.PickTreeMenuItem("Add AxisAlignedRectangle");
        var rect = frame.ShapesSave!.Shapes.Single();
        editor.ClickRow(frame);
        editor.Press(Key.Delete);
        editor.Press(Key.Z, RawInputModifiers.Control);
        editor.Expand(walk);
        editor.Expand(frame);
        editor.ClickRow(rect);

        editor.Press(Key.Y, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(100));

        walk.Frames.ShouldNotContain(frame);
        editor.Services.SelectedState.SelectedShape.ShouldBeNull();
        editor.Services.SelectedState.SelectedFrame.ShouldNotBe(frame);
        editor.Services.SelectedState.SelectedChain.ShouldBeSameAs(walk);
        editor.Press(Key.C, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(100));
        editor.ProblemText.ShouldBeNull();
    }

    // Only a command, an undo or a redo deselects; a tab switch or a save must not add selection changes of its own.
    [AvaloniaFact]
    public async Task TabSwitch_RaisesNoExtraSelectionChange()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)), AnimationEditorHarness.Chain("Run", "sheet.png", (16, 0, 16, 16)));
        await editor.OpenAsync(editor.WriteAchx("other.achx", AnimationEditorHarness.Chain("Bite", "sheet.png", (0, 0, 16, 16))));
        await editor.OpenAsync(hero);
        editor.ClickRow(editor.ChainNamed("Walk"));
        editor.ClickRow(editor.ChainNamed("Run"), RawInputModifiers.Control);
        int events = 0;
        editor.Services.SelectedState.SelectionChanged += () => events++;

        editor.ClickTab("other.achx");
        editor.Wait(TimeSpan.FromMilliseconds(100));
        events.ShouldBe(2, "to the other tab");
        events = 0;
        editor.ClickTab("hero.achx");
        editor.Wait(TimeSpan.FromMilliseconds(100));
        events.ShouldBe(2, "and back");
    }
}
