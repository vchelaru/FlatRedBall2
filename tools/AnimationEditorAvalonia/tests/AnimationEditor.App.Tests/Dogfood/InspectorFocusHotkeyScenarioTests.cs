using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Window-level hotkeys that act on the selection stand down while any Inspector control has
/// keyboard focus, so a key meant for the field never deletes or reorders the selected item.
/// </summary>
public class InspectorFocusHotkeyScenarioTests
{
    [AvaloniaFact]
    public async Task Delete_WithAnInspectorCheckBoxFocused_KeepsTheSelectedChain()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        await editor.OpenAsync(editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16))));
        editor.ClickRow(editor.ChainNamed("Walk"));
        Focus(editor, editor.Control<CheckBox>("PropChainLoop"));

        editor.Press(Key.Delete);

        editor.Project.AnimationChains.Select(chain => chain.Name).ShouldBe(new[] { "Walk" });
    }

    [AvaloniaFact]
    public async Task Delete_WithAnInspectorToggleButtonFocused_KeepsTheSelectedFrame()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        await editor.OpenAsync(editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16))));
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);
        Focus(editor, editor.Control<ToggleButton>("PropFlipH"));

        editor.Press(Key.Delete);

        walk.Frames.Count.ShouldBe(2);
    }

    [AvaloniaFact]
    public async Task Delete_WithAnInspectorButtonFocused_KeepsTheSelectedFrame()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        await editor.OpenAsync(editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16))));
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);
        Focus(editor, editor.Control<Button>("PropTextureBrowseBtn"));

        editor.Press(Key.Delete);

        walk.Frames.Count.ShouldBe(2);
    }

    [AvaloniaFact]
    public async Task AltDown_WithTheModeComboFocused_DoesNotReorderTheSelectedFrame()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        await editor.OpenAsync(editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16))));
        AnimationChainSave walk = editor.ChainNamed("Walk");
        AnimationFrameSave first = walk.Frames[0];
        editor.Expand(walk);
        editor.ClickRow(first);
        Focus(editor, editor.Control<ComboBox>("PropColorMode"));

        editor.Press(Key.Down, RawInputModifiers.Alt);

        walk.Frames.IndexOf(first).ShouldBe(0);
    }

    [AvaloniaFact]
    public async Task Space_WithTheModeComboFocused_DoesNotTogglePlayback()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        await editor.OpenAsync(editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16))));
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);
        bool wasPlaying = editor.Preview.IsPlaying;
        Focus(editor, editor.Control<ComboBox>("PropColorMode"));

        editor.Press(Key.Space);

        editor.Preview.IsPlaying.ShouldBe(wasPlaying);
    }

    // Keyboard focus, as Tab would give it: a click would also toggle or press the control.
    private static void Focus(AnimationEditorHarness editor, Control control)
    {
        control.BringIntoView();
        editor.Layout();
        control.Focus(NavigationMethod.Tab).ShouldBeTrue($"{control.Name} should take focus");
        editor.Layout();
    }
}
