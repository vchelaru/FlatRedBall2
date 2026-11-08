using System.Text.Json;
using AnimationEditor.Core.Models;
using AnimationEditor.Core.IO;
using AnimationEditor.App.Controls;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Moving between the two tab kinds, an <c>.achx</c> document and a read-only PNG viewer: each
/// shows its own pane, keeps its own state across a switch, and owns the status bar, the title
/// and the editing hotkeys while it is active.
/// </summary>
public class TabKindScenarioTests
{
    private static bool PngPaneShown(AnimationEditorHarness editor) =>
        editor.Control<PngPreviewControl>("PngPane").IsVisible && !editor.Control<Grid>("AchxEditorPane").IsVisible;

    private static bool AchxPaneShown(AnimationEditorHarness editor) =>
        editor.Control<Grid>("AchxEditorPane").IsVisible && !editor.Control<PngPreviewControl>("PngPane").IsVisible;

    [AvaloniaFact]
    public async Task SwitchingToAPngTabAndBack_KeepsTheDocumentAndItsUndoHistory()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string sheet = editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (16, 0, 16, 16)));
        await editor.OpenAsync(hero);
        editor.ClickRow(editor.ChainNamed("Run"));
        editor.Press(Key.Delete);
        List<string> undoBefore = editor.UndoLabels;
        undoBefore.ShouldNotBeEmpty();

        await editor.OpenPngTabAsync(sheet);
        PngPaneShown(editor).ShouldBeTrue();
        editor.ClickTab("hero.achx");

        AchxPaneShown(editor).ShouldBeTrue();
        editor.Control<Control>("AnimationsBlock").IsVisible.ShouldBeTrue();
        editor.UndoLabels.ShouldBe(undoBefore);
        editor.VisibleChainHeaders.ShouldBe(new[] { "Walk" });
    }

    [AvaloniaFact]
    public async Task ClosingTheActivePngTab_ShowsTheDocumentBehindIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string sheet = editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(hero);
        await editor.OpenPngTabAsync(sheet);

        editor.CloseTab("sheet.png");

        AchxPaneShown(editor).ShouldBeTrue();
        editor.TabLabels.ShouldBe(new[] { "hero.achx" });
        editor.VisibleChainHeaders.ShouldBe(new[] { "Walk" });
    }

    [AvaloniaFact]
    public async Task ClosingTheActiveDocumentTab_WithOnlyAPngTabLeft_ShowsThePng()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string sheet = editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenPngTabAsync(sheet);
        await editor.OpenAsync(hero);

        editor.CloseTab("hero.achx");
        await editor.Window.WhenPngTabLoaded();

        PngPaneShown(editor).ShouldBeTrue();
        editor.TabLabels.ShouldBe(new[] { "sheet.png" });
    }

    [AvaloniaFact]
    public async Task RestoringASessionWhoseActiveTabIsAPng_ShowsThePng_ThenTheDocumentOnClick()
    {
        string folder = Path.Combine(Path.GetTempPath(), "AnimationEditorDogfood", Guid.NewGuid().ToString("N"));
        string settingsRoot = Path.Combine(folder, "settings");
        Directory.CreateDirectory(folder);
        string sheet = Path.Combine(folder, "sheet.png");
        string hero = Path.Combine(folder, "hero.achx");
        using (SkiaSharp.SKBitmap bitmap = new SkiaSharp.SKBitmap(64, 64))
        using (SkiaSharp.SKData data = bitmap.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100))
            File.WriteAllBytes(sheet, data.ToArray());
        AnimationChainListSaveFor("Walk").Save(hero);
        var settingsFile = AppSettingsLocation.ForApplicationDataRoot(settingsRoot);
        Directory.CreateDirectory(settingsFile.GetDirectoryContainingThis().FullPath);
        File.WriteAllText(settingsFile.FullPath, JsonSerializer.Serialize(new AppSettingsModel
        {
            OpenTabPaths = new List<string> { hero, sheet },
            ActiveTabPath = sheet,
        }));

        try
        {
            using AnimationEditorHarness editor = new AnimationEditorHarness(settingsRoot);
            editor.WaitUntil(() => editor.TabLabels.Count == 2, TimeSpan.FromSeconds(5));
            await editor.Window.WhenPngTabLoaded();
            PngPaneShown(editor).ShouldBeTrue();

            editor.ClickTab("hero.achx");

            AchxPaneShown(editor).ShouldBeTrue();
            editor.VisibleChainHeaders.ShouldBe(new[] { "Walk" });
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); } catch { /* a watcher may still hold it */ }
        }
    }

    private static FlatRedBall2.AnimationEditorCommon.AnimationChainListSave AnimationChainListSaveFor(string chainName)
    {
        var list = new FlatRedBall2.AnimationEditorCommon.AnimationChainListSave
        {
            CoordinateType = FlatRedBall2.AnimationEditorCommon.TextureCoordinateType.Pixel,
        };
        list.AnimationChains.Add(AnimationEditorHarness.Chain(chainName, "sheet.png", (0, 0, 16, 16)));
        return list;
    }

    [AvaloniaFact]
    public async Task OnAPngTab_TheStatusBarAndTitleDescribeThePng_NotTheHiddenDocument()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string sheet = editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(hero);

        await editor.OpenPngTabAsync(sheet);

        editor.Control<TextBlock>("StatusFilename").Text.ShouldBe("sheet.png");
        editor.Control<TextBlock>("StatusSaveLabel").Text.ShouldBeNullOrEmpty();
        editor.Control<TextBlock>("StatusCounts").Text.ShouldBeNullOrEmpty();
        editor.TitleFileName.ShouldBe("sheet.png");
    }

    [AvaloniaFact]
    public async Task OnAPngTab_Undo_LeavesTheHiddenDocumentAlone()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string sheet = editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (16, 0, 16, 16)));
        await editor.OpenAsync(hero);
        editor.ClickRow(editor.ChainNamed("Run"));
        editor.Press(Key.Delete);
        List<string> undoBefore = editor.UndoLabels;
        await editor.OpenPngTabAsync(sheet);
        editor.Click(editor.Control<PngPreviewControl>("PngPane"));

        editor.Press(Key.Z, RawInputModifiers.Control);

        editor.Project.AnimationChains.Select(chain => chain.Name).ShouldBe(new[] { "Walk" });
        editor.ClickTab("hero.achx");
        editor.UndoLabels.ShouldBe(undoBefore);
    }

    [AvaloniaFact]
    public async Task OnAPngTab_Delete_LeavesTheHiddenDocumentAlone()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string sheet = editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (16, 0, 16, 16)));
        await editor.OpenAsync(hero);
        editor.ClickRow(editor.ChainNamed("Walk"));
        await editor.OpenPngTabAsync(sheet);
        editor.Click(editor.Control<PngPreviewControl>("PngPane"));

        editor.Press(Key.Delete);

        editor.Project.AnimationChains.Select(chain => chain.Name).ShouldBe(new[] { "Walk", "Run" });
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task OnAPngTab_DocumentMenuItemsAreDisabled_AndComeBackOnTheDocumentTab()
    {
        string[] documentItems =
        {
            "MenuSave", "MenuSaveAs", "MenuExportPixiJs", "MenuExportGodot", "MenuExportGif", "MenuExportWebp",
            "MenuUndo", "MenuRedo", "MenuCopy", "MenuCut", "MenuPaste", "MenuDuplicate",
            "MenuReloadFromDisk", "MenuResizeTexture", "MenuAssociateTiledTileset",
            "MenuWireframeZoomIn", "MenuWireframeZoomOut", "MenuPreviewZoomIn", "MenuPreviewZoomOut", "MenuShowHistory",
        };
        string[] appItems = { "MenuNew", "MenuLoad", "MenuOpenProjectFolder", "MenuSettings", "MenuEnableHotReload", "MenuViewLog", "MenuAbout" };
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string sheet = editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (16, 0, 16, 16)),
            AnimationEditorHarness.Chain("Jump", "sheet.png", (32, 0, 16, 16)));
        await editor.OpenAsync(hero);
        // One step to undo and one to redo, so Undo and Redo are both live on the document tab.
        editor.ClickRow(editor.ChainNamed("Run"));
        editor.Press(Key.Delete);
        editor.ClickRow(editor.ChainNamed("Jump"));
        editor.Press(Key.Delete);
        editor.Press(Key.Z, RawInputModifiers.Control);

        await editor.OpenPngTabAsync(sheet);

        documentItems.Where(name => editor.Control<MenuItem>(name).IsEnabled).ShouldBeEmpty();
        appItems.Where(name => !editor.Control<MenuItem>(name).IsEnabled).ShouldBeEmpty();
        editor.ClickTab("hero.achx");
        documentItems.Where(name => !editor.Control<MenuItem>(name).IsEnabled).ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task OnAPngTab_NativeMenuUndo_LeavesTheHiddenDocumentAlone()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string sheet = editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (16, 0, 16, 16)));
        await editor.OpenAsync(hero);
        editor.ClickRow(editor.ChainNamed("Run"));
        editor.Press(Key.Delete);
        await editor.OpenPngTabAsync(sheet);

        // The macOS menu bar calls these delegates directly, with its own Cmd+Z gesture.
        NativeMenuTestSupport.Activate(NativeMenuTestSupport.Build(editor), "Edit", "Undo");
        editor.Layout();

        editor.Project.AnimationChains.Select(chain => chain.Name).ShouldBe(new[] { "Walk" });
    }
}
