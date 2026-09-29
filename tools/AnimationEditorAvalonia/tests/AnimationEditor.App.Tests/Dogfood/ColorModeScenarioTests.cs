using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FlatRedBall2.Animation;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// The frame color Mode combo behaves like the R/G/B/A fields (#1248): blank shows the inherited
/// value as a placeholder, Inherit (or Delete) clears it back to blank, and Multiply/Add are explicit.
/// </summary>
public class ColorModeScenarioTests
{
    [AvaloniaFact]
    public async Task Mode_UnsetAfterAnEarlierMultiply_ShowsMultiplyAsPlaceholder_AndOffersNoNone()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        AnimationChainSave fixture = AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16));
        fixture.Frames[0].ColorOperation = ColorOperation.Multiply;
        await editor.OpenAsync(editor.WriteAchx("hero.achx", fixture));
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);

        editor.ClickRow(walk.Frames[1]);

        ComboBox mode = editor.Control<ComboBox>("PropColorMode");
        mode.SelectedIndex.ShouldBe(-1);
        mode.PlaceholderText.ShouldBe("Multiply");
        // The inherited mode is greyed exactly like an inherited channel's placeholder.
        TextBlock modeGhost = mode.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Name == "PlaceholderTextBlock");
        TextBlock redGhost = editor.Control<NumericUpDown>("PropRed").GetVisualDescendants().OfType<TextBlock>().Single(block => block.Name == "PART_Placeholder");
        (modeGhost.Foreground, modeGhost.Opacity).ShouldBe((redGhost.Foreground, redGhost.Opacity));
        mode.Items.OfType<ComboBoxItem>().Select(item => item.Content).ShouldBe(new object[] { "Inherit", "Multiply", "Add" });
    }

    [AvaloniaFact]
    public async Task Mode_PickingInherit_ClearsAnExplicitMode_BackToTheInheritedValue()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        AnimationChainSave fixture = AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16));
        fixture.Frames[0].ColorOperation = ColorOperation.Multiply;
        fixture.Frames[1].ColorOperation = ColorOperation.Add;
        string path = editor.WriteAchx("hero.achx", fixture);
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[1]);

        editor.PickComboItem("PropColorMode", "Inherit");

        walk.Frames[1].ColorOperation.ShouldBeNull();
        ComboBox mode = editor.Control<ComboBox>("PropColorMode");
        mode.SelectedIndex.ShouldBe(-1);
        mode.PlaceholderText.ShouldBe("Multiply");
        editor.UndoLabels.ShouldBe(new[] { "Set Frame Color Mode" });
        AnimationEditorHarness.ReadSaved(path).AnimationChains.Single().Frames[1].ColorOperation.ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task Mode_PickingInheritWhenAlreadyInherited_AddsNoUndoStep_AndStaysBlank()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        await editor.OpenAsync(editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16))));
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);

        editor.PickComboItem("PropColorMode", "Inherit");

        ComboBox mode = editor.Control<ComboBox>("PropColorMode");
        mode.SelectedIndex.ShouldBe(-1);
        mode.PlaceholderText.ShouldBe("None", "nothing earlier sets a mode, so no tint applies");
        editor.UndoManager.CanUndo.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task Mode_DeleteKeyOnTheCombo_ClearsTheMode_LikeDeletingANumericField()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        AnimationChainSave fixture = AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16));
        fixture.Frames[0].ColorOperation = ColorOperation.Add;
        await editor.OpenAsync(editor.WriteAchx("hero.achx", fixture));
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);
        editor.Control<ComboBox>("PropColorMode").Focus();
        editor.Layout();

        editor.Press(Key.Delete);

        walk.Frames[0].ColorOperation.ShouldBeNull();
        editor.Control<ComboBox>("PropColorMode").PlaceholderText.ShouldBe("None");
    }

    [AvaloniaFact]
    public async Task Mode_PickingInheritOnAMixedSelection_ClearsEveryFrame_AsOneUndoStep()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        AnimationChainSave fixture = AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16));
        fixture.Frames[0].ColorOperation = ColorOperation.Multiply;
        fixture.Frames[1].ColorOperation = ColorOperation.Add;
        await editor.OpenAsync(editor.WriteAchx("hero.achx", fixture));
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);
        editor.ClickRow(walk.Frames[1], RawInputModifiers.Shift);
        editor.Control<ComboBox>("PropColorMode").PlaceholderText.ShouldBe("(mixed)");

        editor.PickComboItem("PropColorMode", "Inherit");

        walk.Frames.Select(frame => frame.ColorOperation).ShouldBe(new ColorOperation?[] { null, null });
        editor.UndoLabels.ShouldBe(new[] { "Set Frame Color Mode" });
    }
}
