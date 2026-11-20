using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>#1832: the add-frame ghost follows the command key with the pointer resting, no move needed.</summary>
public class AddFrameGhostKeyScenarioTests
{
    [AvaloniaFact]
    public async Task CtrlPressedThenReleased_WithPointerResting_ShowsThenClearsGhost()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 128, 128);
        await editor.OpenAsync(editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16))));
        editor.ClickRow(editor.ChainNamed("Walk"));
        editor.Hover(editor.WireframePointAt(70, 40));
        editor.Wireframe.AddFrameGhost.ShouldBeNull();

        editor.Window.KeyPress(Key.LeftCtrl, RawInputModifiers.Control, PhysicalKey.None, null);
        editor.Layout();
        editor.Wireframe.AddFrameGhost.ShouldNotBeNull();

        editor.Window.KeyRelease(Key.LeftCtrl, RawInputModifiers.None, PhysicalKey.None, null);
        editor.Layout();
        editor.Wireframe.AddFrameGhost.ShouldBeNull();
    }

    /// <summary>
    /// With the pointer resting, the toolkit re-raises PointerMoved carrying the modifiers of the last
    /// real pointer event, not the live keyboard state; it must not undo the key's effect.
    /// </summary>
    [AvaloniaFact]
    public async Task StalePointerMoveAfterKeyChange_DoesNotUndoTheGhost()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 128, 128);
        await editor.OpenAsync(editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16))));
        editor.ClickRow(editor.ChainNamed("Walk"));
        Avalonia.Point point = editor.WireframePointAt(70, 40);
        editor.Hover(point);

        editor.Window.KeyPress(Key.LeftCtrl, RawInputModifiers.Control, PhysicalKey.None, null);
        editor.Window.MouseMove(point, RawInputModifiers.None);
        editor.Layout();
        editor.Wireframe.AddFrameGhost.ShouldNotBeNull();

        editor.Window.KeyRelease(Key.LeftCtrl, RawInputModifiers.None, PhysicalKey.None, null);
        editor.Window.MouseMove(point, RawInputModifiers.Control);
        editor.Layout();
        editor.Wireframe.AddFrameGhost.ShouldBeNull();
    }
}
