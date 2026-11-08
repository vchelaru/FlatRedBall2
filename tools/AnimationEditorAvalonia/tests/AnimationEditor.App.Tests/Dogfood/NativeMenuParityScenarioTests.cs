using AnimationEditor.App.Controls;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// The macOS system menu bar (#1687) is a live mirror of the in-window menu: same items, order, nesting, enabled
/// and checked state, with the hotkey-registry gestures on the Mac command key. These tests build the bar headlessly
/// and compare it with the in-window menu, so a new in-window item or gesture that has no native counterpart fails
/// here. Only the AppKit rendering and key-equivalent dispatch need a real Mac.
/// </summary>
public class NativeMenuParityScenarioTests
{
    private static NativeMenu BuildBar(AnimationEditorHarness editor) => NativeMenuTestSupport.Build(editor);

    private static async Task<AnimationEditorHarness> OpenHeroAsync()
    {
        var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (0, 16, 16, 16)));
        await editor.OpenAsync(path);
        return editor;
    }

    private static NativeMenuItem Find(NativeMenu bar, params string[] path) => NativeMenuTestSupport.Find(bar, path);

    // Deliberate exclusions from the native bar: none. Every in-window item, including Settings and About, is mirrored.
    [AvaloniaFact]
    public async Task NativeBar_HasAnItemForEveryInWindowMenuItem_WithMatchingStateAndGestures()
    {
        using var editor = await OpenHeroAsync();
        NativeMenu bar = BuildBar(editor);

        NativeMenuParity.Differences(editor.Control<Menu>("MainMenu"), bar, CommandModifier.Meta).ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task ParityCheck_ReportsAnInWindowItemThatTheNativeBarLacks()
    {
        using var editor = await OpenHeroAsync();
        NativeMenu bar = BuildBar(editor);
        NativeMenu file = Find(bar, "File").Menu!;
        file.Items.Remove(Find(bar, "File", "Save"));

        NativeMenuParity.Differences(editor.Control<Menu>("MainMenu"), bar, CommandModifier.Meta)
            .ShouldContain(difference => difference.Contains("Save"));
    }

    [AvaloniaFact]
    public async Task ParityCheck_ReportsAGestureThatDriftedFromTheInWindowMenu()
    {
        using var editor = await OpenHeroAsync();
        NativeMenu bar = BuildBar(editor);
        Find(bar, "Edit", "Undo").Gesture = new KeyGesture(Key.U, KeyModifiers.Meta);

        NativeMenuParity.Differences(editor.Control<Menu>("MainMenu"), bar, CommandModifier.Meta)
            .ShouldContain(difference => difference.Contains("Undo") && difference.Contains("gesture"));
    }

    [AvaloniaFact]
    public async Task NewInWindowItemAddedLater_AppearsInTheNativeBar()
    {
        using var editor = await OpenHeroAsync();
        NativeMenu bar = BuildBar(editor);
        MenuItem edit = editor.Control<Menu>("MainMenu").Items.OfType<MenuItem>().Single(item => ((string?)item.Header) == "_Edit");

        edit.Items.Add(new MenuItem { Header = "_Brand New" });

        Find(bar, "Edit", "Brand New").ShouldNotBeNull();
    }

    [AvaloniaFact]
    public async Task EditGestures_ComeFromTheHotkeyRegistry_OnTheMacCommandKey()
    {
        using var editor = await OpenHeroAsync();
        NativeMenu bar = BuildBar(editor);

        Find(bar, "Edit", "Undo").Gesture.ShouldBe(new KeyGesture(Key.Z, KeyModifiers.Meta));
        Find(bar, "Edit", "Redo").Gesture.ShouldBe(new KeyGesture(Key.Y, KeyModifiers.Meta));   // the registry's first Redo gesture
        Find(bar, "View", "Wireframe Zoom In").Gesture.ShouldBe(new KeyGesture(Key.OemPlus, KeyModifiers.Meta));
        Find(bar, "View", "Preview Zoom Out").Gesture.ShouldBe(new KeyGesture(Key.OemMinus, KeyModifiers.Meta));
        Find(bar, "Help", "Show Render Diagnostics").Gesture.ShouldBe(editor.Control<MenuItem>("MenuShowDiagnostics").InputGesture);
    }

    [AvaloniaFact]
    public async Task UndoAndCopy_AreGreyedOutLikeTheInWindowMenu()
    {
        using var editor = await OpenHeroAsync();
        NativeMenu bar = BuildBar(editor);
        NativeMenuItem undo = Find(bar, "Edit", "Undo");
        NativeMenuItem redo = Find(bar, "Edit", "Redo");
        undo.IsEnabled.ShouldBeFalse();

        editor.ClickRow(editor.ChainNamed("Run"));
        editor.Press(Key.Delete);
        undo.IsEnabled.ShouldBeTrue();
        undo.IsEnabled.ShouldBe(editor.Control<MenuItem>("MenuUndo").IsEnabled);

        undo.Command!.Execute(null);
        editor.Layout();
        undo.IsEnabled.ShouldBeFalse();
        redo.IsEnabled.ShouldBeTrue();
    }

    [AvaloniaFact]
    public async Task EnableHotReload_ShowsAndTogglesItsCheckedState()
    {
        using var editor = await OpenHeroAsync();
        NativeMenu bar = BuildBar(editor);
        NativeMenuItem hotReload = Find(bar, "Edit", "Enable Hot Reload");
        MenuItem inWindow = editor.Control<MenuItem>("MenuEnableHotReload");
        hotReload.ToggleType.ShouldBe(MenuItemToggleType.CheckBox);
        hotReload.IsChecked.ShouldBe(inWindow.IsChecked == true);

        hotReload.Command!.Execute(null);
        editor.Layout();

        inWindow.IsChecked.ShouldBe(false);
        hotReload.IsChecked.ShouldBeFalse();
        editor.Services.AppCommands.HotReloadWatcher.IsEnabled.ShouldBeFalse();

        hotReload.Command!.Execute(null);
        editor.Layout();
        hotReload.IsChecked.ShouldBeTrue();
        editor.Services.AppCommands.HotReloadWatcher.IsEnabled.ShouldBeTrue();
    }

    [AvaloniaFact]
    public async Task OpenRecent_FollowsTheRecentFilesAfterTheBarIsBuilt()
    {
        using var editor = await OpenHeroAsync();
        NativeMenu bar = BuildBar(editor);
        editor.WritePng("sheet2.png", 64, 64);
        string second = editor.WriteAchx("second.achx", AnimationEditorHarness.Chain("Idle", "sheet2.png", (0, 0, 16, 16)));

        await editor.OpenAsync(second);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

        NativeMenu recent = Find(bar, "File", "Open Recent").Menu!;
        recent.Items.OfType<NativeMenuItem>().Select(item => item.Header).ShouldContain("second.achx");
    }

    [AvaloniaFact]
    public async Task ExportSubmenu_AndNewSequenceFile_ArePresent()
    {
        using var editor = await OpenHeroAsync();
        NativeMenu bar = BuildBar(editor);

        Find(bar, "File", "New Sequence File…").ShouldNotBeNull();
        NativeMenu export = Find(bar, "File", "Export").Menu!;
        export.Items.OfType<NativeMenuItem>().Select(item => item.Header)
            .ShouldBe(new[] { "PixiJS…", "Godot SpriteFrames…", "Animated GIF…", "Animated WebP…" });
    }

    [AvaloniaFact]
    public async Task ActivatingAnItem_RunsTheInWindowItemsClickHandler()
    {
        using var editor = await OpenHeroAsync();
        NativeMenu bar = BuildBar(editor);

        int clicks = 0;
        editor.Control<MenuItem>("MenuReloadFromDisk").Click += (_, _) => clicks++;
        Find(bar, "Edit", "Reload from Disk").Command!.Execute(null);

        clicks.ShouldBe(1);
    }

    [AvaloniaFact]
    public async Task OpeningTheFileMenu_RefreshesCloseProjectFolderEnabled()
    {
        using var editor = await OpenHeroAsync();
        NativeMenu bar = BuildBar(editor);
        NativeMenuItem close = Find(bar, "File", "Close Project Folder");
        NativeMenuTestSupport.RaiseOpening(Find(bar, "File").Menu!);
        close.IsEnabled.ShouldBeFalse();

        await editor.Window.OpenProjectFolderForTestAsync(editor.ProjectFolder);
        NativeMenuTestSupport.RaiseOpening(Find(bar, "File").Menu!);

        close.IsEnabled.ShouldBeTrue();
    }
}
