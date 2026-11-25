using AnimationEditor.Core.Data;
using Avalonia.Headless.XUnit;
using Shouldly;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>The preview's guides belong to the document they were dragged in.</summary>
public class GuidesPerFileScenarioTests
{
    [AvaloniaFact]
    public async Task OpeningAFileWithoutACompanion_ShowsNoGuides()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await OpenHeroWithGuideAsync(editor);
        string other = editor.WriteAchx("other.achx", AnimationEditorHarness.Chain("Other", "sheet.png", (16, 0, 16, 16)));

        await editor.OpenAsync(other);

        editor.Preview.HGuides.ShouldBeEmpty();
        editor.ClickTab("hero.achx");
        editor.Layout();
        editor.Preview.HGuideCount.ShouldBe(1);
        editor.ClickTab("other.achx"); // now with the companion that leaving it saved
        editor.Layout();
        editor.Preview.HGuides.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task OpeningAFileWithACompanion_NeverSavesTheOtherFilesGuidesIntoIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string other = editor.WriteAchx("other.achx", AnimationEditorHarness.Chain("Other", "sheet.png", (16, 0, 16, 16)));
        editor.Services.IoManager.SaveCompanionFileFor(
            new FilePath(other), new AESettingsSave { HorizontalGuides = { 3 }, WireframeZoomPercent = 300 });
        await OpenHeroWithGuideAsync(editor);

        await editor.OpenAsync(other);

        editor.Preview.HGuides.ShouldBe(new[] { 3f });
        editor.Services.IoManager.TryLoadCompanionSettings(other)!.HorizontalGuides.ShouldBe(new[] { 3f });
    }

    [AvaloniaFact]
    public async Task ReloadingAFileWithoutACompanion_KeepsItsGuides()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string hero = await OpenHeroWithGuideAsync(editor);
        File.Delete(Path.ChangeExtension(hero, ".aeproperties"));

        editor.Services.AppCommands.ReloadAchxFromDisk(hero);
        editor.Layout();

        editor.Preview.HGuideCount.ShouldBe(1);
    }

    [AvaloniaFact]
    public async Task UntitledDocument_HasItsOwnGuides()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await OpenHeroWithGuideAsync(editor);
        float heroGuide = editor.Preview.HGuides.Single();

        editor.Dialogs.AnswerNextSaveFile(null);
        editor.ClickMenu("MenuNew");
        editor.Preview.HGuides.ShouldBeEmpty();

        editor.Drag(editor.PointIn(editor.Preview, 2, 90), editor.PointIn(editor.Preview, 60, 100));
        float untitledGuide = editor.Preview.HGuides.Single();
        editor.ClickTab("hero.achx");
        editor.Layout();
        editor.Preview.HGuides.ShouldBe(new[] { heroGuide });
        editor.ClickTab(editor.TabLabels[1]);
        editor.Layout();
        editor.Preview.HGuides.ShouldBe(new[] { untitledGuide });
    }

    [AvaloniaFact]
    public async Task ClosingTheLastTab_TakesItsGuidesAway()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await OpenHeroWithGuideAsync(editor);

        editor.CloseTab("hero.achx");

        editor.Preview.HGuides.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task FileThatDoesNotLoad_LeavesTheGuidesOfTheTabInFront()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await OpenHeroWithGuideAsync(editor);
        string broken = Path.Combine(editor.ProjectFolder, "broken.achx");
        File.WriteAllText(broken, "<AnimationChainArrayS");

        await editor.OpenAsync(broken);

        editor.TabLabels.ShouldBe(new[] { "hero.achx" });
        editor.Preview.HGuideCount.ShouldBe(1);
    }

    private static async Task<string> OpenHeroWithGuideAsync(AnimationEditorHarness editor)
    {
        editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(hero);
        editor.Drag(editor.PointIn(editor.Preview, 2, 120), editor.PointIn(editor.Preview, 60, 140));
        editor.Preview.HGuideCount.ShouldBe(1);
        return hero;
    }
}
