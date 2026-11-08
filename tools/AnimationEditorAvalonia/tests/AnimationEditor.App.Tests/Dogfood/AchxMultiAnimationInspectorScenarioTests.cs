using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// With several animations selected the ANIMATION panel edits them all, like the sequence Inspector:
/// Name disabled with a "(multiple ...)" placeholder, flag checkboxes mixed when the animations
/// differ, and a flag click applies to every selected animation as one undo step.
/// </summary>
public class AchxMultiAnimationInspectorScenarioTests
{
    private static async Task<(AnimationEditorHarness Editor, AnimationChainSave A, AnimationChainSave B)> OpenAsync()
    {
        AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("A", "sheet.png", (0, 0, 16, 16)),
            AnimationEditorHarness.Chain("B", "sheet.png", (0, 32, 16, 16)));
        await editor.OpenAsync(path);
        return (editor, editor.ChainNamed("A"), editor.ChainNamed("B"));
    }

    private static void SelectBoth(AnimationEditorHarness editor, AnimationChainSave a, AnimationChainSave b)
    {
        editor.ClickRow(a);
        editor.ClickRow(b, RawInputModifiers.Control);
    }

    [AvaloniaFact]
    public async Task TwoAnimationsWithDifferentLoop_LoopIsMixed_AndNameIsDisabledWithAPlaceholder()
    {
        var (editor, a, b) = await OpenAsync();
        using var _ = editor;
        b.Loop = false;

        SelectBoth(editor, a, b);

        editor.Control<CheckBox>("PropChainLoop").IsChecked.ShouldBeNull();
        TextBox name = editor.Control<TextBox>("PropChainName");
        name.IsEnabled.ShouldBeFalse();
        name.PlaceholderText.ShouldBe("(multiple — same file)");
        (name.Text ?? "").ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task ClickingLoop_OnTwoAnimationsThatLoop_ClearsBoth_AsOneLabelledUndoStep()
    {
        var (editor, a, b) = await OpenAsync();
        using var _ = editor;
        SelectBoth(editor, a, b);
        int undoCount = editor.UndoLabels.Count;

        editor.Click(editor.Control<CheckBox>("PropChainLoop"));

        a.Loop.ShouldBeFalse();
        b.Loop.ShouldBeFalse();
        editor.UndoLabels.Count.ShouldBe(undoCount + 1);
        editor.UndoLabels[^1].ShouldBe("Set Loop on 2 Animations");

        editor.Press(Key.Z, RawInputModifiers.Control);

        a.Loop.ShouldBeTrue();
        b.Loop.ShouldBeTrue();
    }

    [AvaloniaFact]
    public async Task ClickingLoop_WhenMixed_MakesBothTheSame_AndUndoRestoresTheMix()
    {
        var (editor, a, b) = await OpenAsync();
        using var _ = editor;
        b.Loop = false;
        SelectBoth(editor, a, b);
        int undoCount = editor.UndoLabels.Count;

        editor.Click(editor.Control<CheckBox>("PropChainLoop"));

        b.Loop.ShouldBe(a.Loop);
        editor.UndoLabels.Count.ShouldBe(undoCount + 1);

        editor.Press(Key.Z, RawInputModifiers.Control);

        a.Loop.ShouldBeTrue();
        b.Loop.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task ClickingLocked_OnMixedAnimations_AppliesToBoth_AsOneUndoStep()
    {
        var (editor, a, b) = await OpenAsync();
        using var _ = editor;
        b.IsLocked = true;
        SelectBoth(editor, a, b);
        editor.Control<CheckBox>("PropChainLocked").IsChecked.ShouldBeNull();
        int undoCount = editor.UndoLabels.Count;

        editor.Click(editor.Control<CheckBox>("PropChainLocked"));

        b.IsLocked.ShouldBe(a.IsLocked);
        editor.UndoLabels.Count.ShouldBe(undoCount + 1);
    }

    [AvaloniaFact]
    public async Task OneAnimationSelected_NameIsEditable_AndShowsTheName()
    {
        var (editor, a, _) = await OpenAsync();
        using var _2 = editor;

        editor.ClickRow(a);

        TextBox name = editor.Control<TextBox>("PropChainName");
        name.IsEnabled.ShouldBeTrue();
        name.Text.ShouldBe("A");
        name.PlaceholderText.ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task SelectingTwoThenOne_ReenablesTheNameBox()
    {
        var (editor, a, b) = await OpenAsync();
        using var _ = editor;
        SelectBoth(editor, a, b);

        editor.ClickRow(b);

        TextBox name = editor.Control<TextBox>("PropChainName");
        name.IsEnabled.ShouldBeTrue();
        name.Text.ShouldBe("B");
    }
}
