using AnimationEditor.App.Controls;
using AnimationEditor.App.Models;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Ctrl-modified mouse gestures run as macOS would (#1247): the window is built with the ⌘
/// command modifier, so ⌘ takes over every gesture Ctrl drives on Windows and Linux, and
/// Control+click (a right-click on macOS) no longer adds frames.
/// </summary>
public class CommandModifierScenarioTests
{
    private static async Task<(AnimationEditorHarness editor, AnimationChainSave walk)> OpenMacEditorAsync()
    {
        AnimationEditorHarness editor = new AnimationEditorHarness(commandModifier: CommandModifier.Meta);
        editor.WritePng("sheet.png", 128, 128);
        await editor.OpenAsync(editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16))));
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.ClickRow(walk);
        return (editor, walk);
    }

    [AvaloniaFact]
    public async Task WireframeClick_MacCommandHeld_AddsAFrame()
    {
        (AnimationEditorHarness editor, AnimationChainSave walk) = await OpenMacEditorAsync();
        using (editor)
        {
            editor.ClickAt(editor.WireframePointAt(70, 40), RawInputModifiers.Meta);

            walk.Frames.Count.ShouldBe(3);
        }
    }

    [AvaloniaFact]
    public async Task WireframeClick_MacControlHeld_DoesNotAddAFrame()
    {
        (AnimationEditorHarness editor, AnimationChainSave walk) = await OpenMacEditorAsync();
        using (editor)
        {
            editor.ClickAt(editor.WireframePointAt(70, 40), RawInputModifiers.Control);

            walk.Frames.Count.ShouldBe(2);
        }
    }

    [AvaloniaFact]
    public async Task AddFrameGhost_MacCommandKeyPressedAndReleased_ShowsThenHides()
    {
        (AnimationEditorHarness editor, _) = await OpenMacEditorAsync();
        using (editor)
        {
            editor.Hover(editor.WireframePointAt(70, 40));

            editor.Window.KeyPress(Key.LeftCtrl, RawInputModifiers.Control, PhysicalKey.None, null);
            editor.Wireframe.AddFrameGhost.ShouldBeNull();
            editor.Window.KeyRelease(Key.LeftCtrl, RawInputModifiers.None, PhysicalKey.None, null);

            editor.Window.KeyPress(Key.LWin, RawInputModifiers.Meta, PhysicalKey.None, null);
            editor.Wireframe.AddFrameGhost.ShouldNotBeNull();
            editor.Window.KeyRelease(Key.LWin, RawInputModifiers.None, PhysicalKey.None, null);
            editor.Wireframe.AddFrameGhost.ShouldBeNull();
        }
    }

    [AvaloniaFact]
    public async Task AddFrameGhost_MacHoverWithCommandHeld_Shows()
    {
        (AnimationEditorHarness editor, _) = await OpenMacEditorAsync();
        using (editor)
        {
            editor.Window.MouseMove(editor.WireframePointAt(70, 40), RawInputModifiers.Meta);

            editor.Wireframe.AddFrameGhost.ShouldNotBeNull();
        }
    }

    [AvaloniaFact]
    public async Task TreePress_MacCommandOnAFrameInAMultiSelection_ReachesTheTreeView()
    {
        (AnimationEditorHarness editor, AnimationChainSave walk) = await OpenMacEditorAsync();
        using (editor)
        {
            editor.Expand(walk);
            // Last click on a different row than the ⌘-press, so the press isn't read as a double-click.
            editor.ClickRow(walk.Frames[1]);
            editor.ClickRow(walk.Frames[0], RawInputModifiers.Shift);
            editor.AnimTree.SelectedItems.Count.ShouldBe(2);

            // A plain press on a multi-selected frame is held back as a possible multi-frame drag,
            // leaving the selection alone until release. A ⌘-press must instead go straight to the
            // TreeView's own selection handling (which toggles on a real Mac, and which the
            // headless platform, knowing no ⌘, treats as a plain click that selects one row).
            editor.Window.MouseDown(editor.RowHeaderPoint(walk.Frames[1]), MouseButton.Left, RawInputModifiers.Meta);

            editor.AnimTree.SelectedItems.Count.ShouldBe(1);
            editor.Window.MouseUp(editor.RowHeaderPoint(walk.Frames[1]), MouseButton.Left, RawInputModifiers.Meta);
        }
    }

    [AvaloniaFact]
    public async Task ShortcutsList_Mac_ShowsCommandSymbolInsteadOfCtrl()
    {
        (AnimationEditorHarness editor, _) = await OpenMacEditorAsync();
        using (editor)
        {
            List<HotkeyEntryVm> rows = editor.Control<ItemsControl>("ShortcutsList").ItemsSource!
                .Cast<HotkeyCategoryVm>().SelectMany(group => group.Hotkeys).ToList();

            rows.ShouldContain(row => row.Gesture == "⌘+Z");
            rows.ShouldNotContain(row => row.Gesture.Contains("Ctrl"));
        }
    }

    [AvaloniaFact]
    public async Task MagicWandTooltip_Mac_SaysCommandClick()
    {
        (AnimationEditorHarness editor, _) = await OpenMacEditorAsync();
        using (editor)
        {
            string tip = (string)ToolTip.GetTip(editor.Control<ToggleButton>("MagicWandToggle"))!;

            tip.ShouldContain("⌘+click");
            tip.ShouldNotContain("Ctrl");
        }
    }
}
