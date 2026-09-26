using AnimationEditor.Views.Controls;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// The surfaces around the editor: the Project folder panel and its preview tabs, the group
/// preview for several chains, the PNG tab, the Shortcuts tab, the theme menu across a restart,
/// and Close Project.
/// </summary>
public class PanelsScenarioTests
{
    [AvaloniaFact]
    public async Task CloseProject_ClearsTheTabsAndThePanel()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.Window.OpenProjectFolderForTestAsync(editor.ProjectFolder);
        editor.Layout();
        await editor.OpenAsync(Path.Combine(editor.ProjectFolder, "hero.achx"));
        editor.Tabs.Tabs.Count.ShouldBe(1);
        editor.Wireframe.BitmapSize.ShouldBe((64, 64));

        editor.Dialogs.AnswerNextConfirm(true);
        editor.ClickMenu("MenuCloseProject");
        editor.Wait(TimeSpan.FromMilliseconds(200));

        editor.Tabs.Tabs.ShouldBeEmpty();
        editor.Control<ProjectPanelControl>("ProjectPanel").TreeRoots.ShouldBeEmpty();
        editor.Wireframe.BitmapSize.ShouldBe((0, 0), "no document open, so no texture on the canvas");
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task CtrlClickingTwoChains_ShowsTheGroupPreview_AndAPlainClickLeavesIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (32, 0, 16, 16), (48, 0, 16, 16)));
        await editor.OpenAsync(path);
        editor.ClickRow(editor.ChainNamed("Walk"));
        editor.ClickRow(editor.ChainNamed("Run"), RawInputModifiers.Control);

        editor.Preview.IsGroupPreviewActive.ShouldBeTrue();
        editor.Preview.GroupTracks.Count.ShouldBe(2);
        editor.Control<Control>("GroupTimelineScrubHost").IsVisible.ShouldBeTrue();
        editor.Control<Control>("TimelineScrubSurface").IsVisible.ShouldBeFalse();

        editor.Press(Key.Space);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));
        editor.Press(Key.Space);
        editor.ThrowIfErrorShown();

        editor.ClickRow(editor.ChainNamed("Walk"));

        editor.Preview.IsGroupPreviewActive.ShouldBeFalse();
        editor.Control<Control>("TimelineScrubSurface").IsVisible.ShouldBeTrue();
    }

    [AvaloniaFact]
    public async Task OpenPngAsTab_ShowsThePngPane_AndTheAchxTabBringsTheEditorBack()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string png = editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        string achxTab = editor.Tabs.ActiveTab!.DisplayName;

        editor.Window.OpenPngAsTab(png);
        await editor.Window.WhenPngTabLoaded();
        editor.Layout();

        editor.Tabs.Tabs.Count.ShouldBe(2);
        editor.Control<Control>("PngPaneGrid").IsVisible.ShouldBeTrue();
        editor.Control<Control>("AchxEditorPane").IsVisible.ShouldBeFalse();

        editor.ClickTab(achxTab);

        editor.Control<Control>("AchxEditorPane").IsVisible.ShouldBeTrue();
        editor.Control<Control>("PngPaneGrid").IsVisible.ShouldBeFalse();
        editor.VisibleChainHeaders.ShouldBe(new[] { "Walk" });
        editor.CloseTab(editor.Tabs.Tabs.Single(tab => tab.Path.FullPath.EndsWith(".png")).DisplayName);
        editor.Tabs.Tabs.Count.ShouldBe(1);
    }

    [AvaloniaFact]
    public async Task ProjectPanel_ClickPreviewsAFile_ASecondClickReplacesThePreview_DoubleClickKeepsIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        editor.WriteAchx("enemy.achx", AnimationEditorHarness.Chain("Bite", "sheet.png", (0, 0, 16, 16)));
        await editor.Window.OpenProjectFolderForTestAsync(editor.ProjectFolder);
        editor.Layout();
        ProjectPanelControl panel = editor.Control<ProjectPanelControl>("ProjectPanel");
        panel.TreeRoots.Select(node => node.Name).ShouldBe(new[] { "enemy.achx", "hero.achx" }, ignoreOrder: true);

        editor.Click(RowFor(editor, panel, "hero.achx"));
        editor.Wait(TimeSpan.FromMilliseconds(200));
        editor.Tabs.Tabs.Count.ShouldBe(1);
        editor.Tabs.ActiveTab!.IsPreview.ShouldBeTrue("a single click opens a preview tab");
        editor.VisibleChainHeaders.ShouldBe(new[] { "Walk" });

        editor.Click(RowFor(editor, panel, "enemy.achx"));
        editor.Wait(TimeSpan.FromMilliseconds(200));
        editor.Tabs.Tabs.Count.ShouldBe(1, "a preview tab is replaced, not stacked");
        editor.Tabs.ActiveTab!.Path.FullPath.ShouldEndWith("enemy.achx");
        editor.VisibleChainHeaders.ShouldBe(new[] { "Bite" });

        editor.DoubleClickAt(editor.CenterOf(RowFor(editor, panel, "enemy.achx")));
        editor.Wait(TimeSpan.FromMilliseconds(200));
        editor.Tabs.ActiveTab!.IsPreview.ShouldBeFalse("a double-click keeps the tab");

        editor.Click(RowFor(editor, panel, "hero.achx"));
        editor.Wait(TimeSpan.FromMilliseconds(200));
        editor.Tabs.Tabs.Count.ShouldBe(2, "the kept tab survives the next preview");
    }

    [AvaloniaFact]
    public async Task ProjectPanel_EditingInsideAPreviewTab_PromotesIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.Window.OpenProjectFolderForTestAsync(editor.ProjectFolder);
        editor.Layout();
        ProjectPanelControl panel = editor.Control<ProjectPanelControl>("ProjectPanel");
        editor.Click(RowFor(editor, panel, "hero.achx"));
        editor.Wait(TimeSpan.FromMilliseconds(200));
        editor.Tabs.ActiveTab!.IsPreview.ShouldBeTrue();

        editor.Click(editor.RowButton(editor.ChainNamed("Walk"), "Add Frame"));

        editor.Tabs.ActiveTab!.IsPreview.ShouldBeFalse("an edit turns the preview into a real tab");
        editor.ChainNamed("Walk").Frames.Count.ShouldBe(2);
    }

    // #1208: the copy sits beside the original, so its relative texture path still resolves.
    [AvaloniaFact]
    public async Task ProjectPanel_DuplicateOnAFile_WritesACopyBesideItAndOpensIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string original = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        string copy = Path.Combine(editor.ProjectFolder, "heroCopy.achx");
        await editor.Window.OpenProjectFolderForTestAsync(editor.ProjectFolder);
        editor.Layout();
        ProjectPanelControl panel = editor.Control<ProjectPanelControl>("ProjectPanel");

        editor.RightClick(RowFor(editor, panel, "hero.achx"));
        editor.PickMenuItem(panel.ProjectTree.ContextMenu!, "Duplicate");
        (await editor.WaitUntilAsync(() => editor.Tabs.ActiveTab?.Path == new AnimationEditor.Core.Paths.FilePath(copy), TimeSpan.FromSeconds(5)))
            .ShouldBeTrue("the copy opens as the active tab");

        File.ReadAllBytes(copy).ShouldBe(File.ReadAllBytes(original));
        editor.VisibleChainHeaders.ShouldBe(new[] { "Walk" });
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task ShortcutsTab_ListsEveryHotkey_AndItsCloseButtonHidesIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        await editor.OpenAsync(editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16))));

        editor.ClickMenu("MenuShowShortcuts");

        editor.Control<Control>("ShortcutsTab").IsVisible.ShouldBeTrue();
        ItemsControl list = editor.Control<ItemsControl>("ShortcutsList");
        List<AnimationEditor.App.Models.HotkeyCategoryVm> groups = list.ItemsSource!.Cast<AnimationEditor.App.Models.HotkeyCategoryVm>().ToList();
        groups.Select(group => group.Category).ShouldBe(new[] { "Edit", "View", "Playback", "Tree", "File" }, ignoreOrder: true);
        groups.Sum(group => group.Hotkeys.Count).ShouldBeGreaterThanOrEqualTo(15);

        editor.Click(editor.Control<Button>("ShortcutsTabCloseButton"));

        editor.Control<Control>("ShortcutsTab").IsVisible.ShouldBeFalse();
        editor.Control<Control>("InspectorTab").IsVisible.ShouldBeTrue();
    }

    [AvaloniaFact]
    public async Task ThemeDark_AppliesNow_AndSurvivesARestart()
    {
        string settingsRoot = Path.Combine(Path.GetTempPath(), "AnimationEditorDogfood", "settings-" + Guid.NewGuid().ToString("N"));
        using (AnimationEditorHarness first = new AnimationEditorHarness(settingsRoot))
        {
            first.WritePng("sheet.png", 64, 64);
            await first.OpenAsync(first.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16))));

            first.ClickMenu("MenuThemeDark");

            Avalonia.Application.Current!.RequestedThemeVariant.ShouldBe(ThemeVariant.Dark);
            first.Control<MenuItem>("MenuThemeDark").IsChecked.ShouldBeTrue();
        }

        using AnimationEditorHarness second = new AnimationEditorHarness(settingsRoot);
        second.Wait(TimeSpan.FromMilliseconds(100));
        Avalonia.Application.Current!.RequestedThemeVariant.ShouldBe(ThemeVariant.Dark);
        second.Control<MenuItem>("MenuThemeDark").IsChecked.ShouldBeTrue();
        second.ClickMenu("MenuThemeLight");
        Avalonia.Application.Current!.RequestedThemeVariant.ShouldBe(ThemeVariant.Light);
    }

    private static TreeViewItem RowFor(AnimationEditorHarness editor, ProjectPanelControl panel, string fileName)
    {
        editor.Layout();
        return panel.ProjectTree.GetVisualDescendants().OfType<TreeViewItem>()
            .FirstOrDefault(row => row.DataContext is AchxTreeNodeVm node && node.Name == fileName)
            ?? throw new InvalidOperationException($"The Project panel shows no row for {fileName}; it has [{string.Join(", ", panel.TreeRoots.Select(node => node.Name))}].");
    }
}
