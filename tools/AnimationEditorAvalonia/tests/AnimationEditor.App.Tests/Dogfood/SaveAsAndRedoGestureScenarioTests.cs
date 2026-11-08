using AnimationEditor.App.Controls;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// #1688: Save As owns Ctrl/Cmd+Shift+S in the hotkey registry, and Redo shows the platform-preferred gesture
/// (Cmd+Shift+Z on macOS, Ctrl+Y elsewhere) in the in-window menu and the native bar alike. Only AppKit's
/// key-equivalent dispatch needs a real Mac.
/// </summary>
public class SaveAsAndRedoGestureScenarioTests
{
    private static async Task<AnimationEditorHarness> OpenHeroAsync(CommandModifier? commandModifier = null)
    {
        var editor = new AnimationEditorHarness(commandModifier: commandModifier);
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        return editor;
    }

    [AvaloniaFact]
    public async Task OnMac_RedoShowsCmdShiftZ_AndSaveAsShowsCmdShiftS_InTheNativeBar()
    {
        using var editor = await OpenHeroAsync(CommandModifier.Meta);
        NativeMenu bar = NativeMenuTestSupport.Build(editor);

        NativeMenuTestSupport.Find(bar, "Edit", "Redo").Gesture.ShouldBe(new KeyGesture(Key.Z, KeyModifiers.Meta | KeyModifiers.Shift));
        NativeMenuTestSupport.Find(bar, "File", "Save As…").Gesture.ShouldBe(new KeyGesture(Key.S, KeyModifiers.Meta | KeyModifiers.Shift));
        NativeMenuParity.Differences(editor.Control<Menu>("MainMenu"), bar, CommandModifier.Meta).ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task OnWindows_RedoKeepsCtrlY_AndSaveAsShowsCtrlShiftS_InTheMenu()
    {
        using var editor = await OpenHeroAsync();

        editor.Control<MenuItem>("MenuRedo").InputGesture.ShouldBe(new KeyGesture(Key.Y, KeyModifiers.Control));
        editor.Control<MenuItem>("MenuSaveAs").InputGesture.ShouldBe(new KeyGesture(Key.S, KeyModifiers.Control | KeyModifiers.Shift));
    }

    [AvaloniaFact]
    public async Task CtrlShiftS_RunsSaveAs_AndCtrlSStillSavesInPlace()
    {
        using var editor = await OpenHeroAsync();
        string copy = Path.Combine(editor.ProjectFolder, "hero-copy.achx");

        editor.Dialogs.AnswerNextSaveFile(copy);
        editor.Press(Key.S, RawInputModifiers.Control | RawInputModifiers.Shift);
        editor.Wait(TimeSpan.FromMilliseconds(100));

        File.Exists(copy).ShouldBeTrue("Ctrl+Shift+S opens the Save As dialog and writes the chosen file");

        editor.Press(Key.S, RawInputModifiers.Control);   // no dialog answer queued: a dialog here would be unanswered
        editor.Wait(TimeSpan.FromMilliseconds(100));
        editor.Dialogs.ThrowIfUnanswered();
    }

    [AvaloniaFact]
    public async Task OnMac_CmdShiftS_RunsSaveAs_AndCmdShiftZ_Redoes()
    {
        using var editor = await OpenHeroAsync(CommandModifier.Meta);
        string copy = Path.Combine(editor.ProjectFolder, "hero-copy.achx");

        editor.Dialogs.AnswerNextSaveFile(copy);
        editor.Press(Key.S, RawInputModifiers.Meta | RawInputModifiers.Shift);
        editor.Wait(TimeSpan.FromMilliseconds(100));
        File.Exists(copy).ShouldBeTrue();

        editor.ClickRow(editor.ChainNamed("Walk"));
        editor.Press(Key.Delete);
        editor.Press(Key.Z, RawInputModifiers.Meta);
        editor.Services.ProjectManager.AnimationChainListSave!.AnimationChains.Count.ShouldBe(1);

        editor.Press(Key.Z, RawInputModifiers.Meta | RawInputModifiers.Shift);

        editor.Services.ProjectManager.AnimationChainListSave!.AnimationChains.Count.ShouldBe(0);
    }
}
