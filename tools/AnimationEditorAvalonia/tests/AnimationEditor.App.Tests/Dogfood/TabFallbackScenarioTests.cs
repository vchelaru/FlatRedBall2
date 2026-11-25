using AnimationEditor.Views.Controls;
using Avalonia.Headless.XUnit;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// The tab in front and the document the editor holds are the same file, also when the file that should have shown does
/// not load. Otherwise leaving that tab stores the other file's document on it, and the next edit writes it there.
/// </summary>
public class TabFallbackScenarioTests
{
    private const string Corrupt = "<AnimationChainArrayS";

    private static void ShouldAgree(AnimationEditorHarness editor)
    {
        string? document = editor.Services.ProjectManager.FileName is { } file ? Path.GetFileName(file) : null;
        editor.Tabs.ActiveTab?.DisplayName.ShouldBe(document, "the front tab is not the document the editor holds");
    }

    private static List<string> Chains(string path) =>
        AnimationEditorHarness.ReadSaved(path).AnimationChains.Select(chain => chain.Name).ToList();

    [AvaloniaFact]
    public async Task ClosingTheFrontTab_WhenTheNextTabsFileNoLongerLoads_DoesNotShowTheClosedDocumentUnderIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string boss = editor.WriteAchx("boss.achx", AnimationEditorHarness.Chain("Roar", "sheet.png", (0, 0, 16, 16)));
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(boss);
        await editor.OpenAsync(hero);
        File.WriteAllText(boss, Corrupt);

        editor.CloseTab("hero.achx");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

        ShouldAgree(editor);
    }

    [AvaloniaFact]
    public async Task ClosingTheFrontTab_WhenTheNextTabsFileNoLongerLoads_NeverWritesTheClosedDocumentOverIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string boss = editor.WriteAchx("boss.achx", AnimationEditorHarness.Chain("Roar", "sheet.png", (0, 0, 16, 16)));
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        string npc = editor.WriteAchx("npc.achx", AnimationEditorHarness.Chain("Wave", "sheet.png", (0, 0, 16, 16)));
        string bossText = File.ReadAllText(boss);
        await editor.OpenAsync(npc);
        await editor.OpenAsync(boss);
        await editor.OpenAsync(hero);
        File.WriteAllText(boss, Corrupt);   // another program is halfway through writing it
        editor.CloseTab("hero.achx");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));
        File.WriteAllText(boss, bossText);  // and is done

        editor.ClickTab("npc.achx");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));
        if (editor.TabLabels.Contains("boss.achx"))
        {
            editor.ClickTab("boss.achx");
            await editor.WaitAsync(TimeSpan.FromMilliseconds(150));
        }
        else
        {
            await editor.OpenAsync(boss);
        }
        editor.Services.AppCommands.AddAnimationChainWithName("Idle");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

        Chains(boss).ShouldBe(new[] { "Roar", "Idle" });
    }

    [AvaloniaFact]
    public async Task EditingUnderTheNextTab_WhenItsFileNoLongerLoads_DoesNotWriteTheClosedFile()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string boss = editor.WriteAchx("boss.achx", AnimationEditorHarness.Chain("Roar", "sheet.png", (0, 0, 16, 16)));
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(boss);
        await editor.OpenAsync(hero);
        File.WriteAllText(boss, Corrupt);
        editor.CloseTab("hero.achx");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

        editor.Services.AppCommands.AddAnimationChainWithName("TypedUnderBoss");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

        Chains(hero).ShouldBe(new[] { "Walk" });
    }

    [AvaloniaFact]
    public async Task DeletingTheFrontFile_WhenTheNextTabsFileNoLongerLoads_DoesNotBringItBackOnTheNextEdit()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string boss = editor.WriteAchx("boss.achx", AnimationEditorHarness.Chain("Roar", "sheet.png", (0, 0, 16, 16)));
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenProjectFolderAsync();
        await editor.OpenAsync(boss);
        await editor.OpenAsync(hero);
        File.WriteAllText(boss, Corrupt);
        editor.Window.DeleteToRecycleBin = path => { File.Delete(path); return null; };
        editor.Dialogs.AnswerNextConfirm(true);
        await editor.Window.DeleteProjectFileAsync("hero.achx");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

        editor.Services.AppCommands.AddAnimationChainWithName("TypedUnderBoss");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

        File.Exists(hero).ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task ReopeningABackgroundTabWhoseFileNoLongerLoads_DoesNotShowThePreviousDocumentUnderIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string boss = editor.WriteAchx("boss.achx", AnimationEditorHarness.Chain("Roar", "sheet.png", (0, 0, 16, 16)));
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(boss);
        await editor.OpenAsync(hero);
        File.WriteAllText(boss, Corrupt);

        await editor.OpenAsync(boss);       // File > Open, a Project row or a recent file
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

        ShouldAgree(editor);
    }

    [AvaloniaFact]
    public async Task PreviewingAFileThatDoesNotLoad_OverAnotherPreview_DoesNotShowThatPreviewUnderAnotherTab()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string boss = editor.WriteAchx("boss.achx", AnimationEditorHarness.Chain("Roar", "sheet.png", (0, 0, 16, 16)));
        editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        File.WriteAllText(Path.Combine(editor.ProjectFolder, "zbroken.achx"), Corrupt);
        await editor.OpenProjectFolderAsync();
        await editor.OpenAsync(boss);
        ProjectPanelControl panel = editor.Control<ProjectPanelControl>("ProjectPanel");
        await editor.WaitUntilAsync(() => panel.TreeRoots.Count > 0, TimeSpan.FromSeconds(5));
        editor.Click(PanelsScenarioTests.RowFor(editor, panel, "hero.achx"));
        await editor.WaitAsync(TimeSpan.FromMilliseconds(600));
        editor.TabLabels.ShouldBe(new[] { "boss.achx", "hero.achx" });

        editor.Click(PanelsScenarioTests.RowFor(editor, panel, "zbroken.achx"));
        await editor.WaitAsync(TimeSpan.FromMilliseconds(600));

        editor.ProblemText!.ShouldContain("zbroken.achx");
        ShouldAgree(editor);
    }

    [AvaloniaFact]
    public async Task ClosingTheFrontTab_WhenSeveralNextFilesNoLongerLoad_ShowsTheFirstThatDoes()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string npc = editor.WriteAchx("npc.achx", AnimationEditorHarness.Chain("Wave", "sheet.png", (0, 0, 16, 16)));
        string boss = editor.WriteAchx("boss.achx", AnimationEditorHarness.Chain("Roar", "sheet.png", (0, 0, 16, 16)));
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        string imp = editor.WriteAchx("imp.achx", AnimationEditorHarness.Chain("Hop", "sheet.png", (0, 0, 16, 16)));
        foreach (string path in new[] { npc, boss, hero, imp }) await editor.OpenAsync(path);
        File.WriteAllText(boss, Corrupt);
        File.WriteAllText(hero, Corrupt);

        editor.CloseTab("imp.achx");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

        editor.TabLabels.ShouldBe(new[] { "npc.achx" });
        ShouldAgree(editor);
    }

    [AvaloniaFact]
    public async Task ClosingTheProject_WhenNoRemainingFileLoads_LeavesTheEditorEmpty()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string outside = editor.WriteAchx("outside.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        string game = Directory.CreateDirectory(Path.Combine(editor.ProjectFolder, "game")).FullName;
        string hero = Path.Combine(game, "hero.achx");
        File.Copy(outside, hero);
        await editor.OpenProjectFolderAsync(game);
        await editor.OpenAsync(outside);
        await editor.OpenAsync(hero);
        File.WriteAllText(outside, Corrupt);

        editor.Window.CloseProjectFolder();
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

        editor.TabLabels.ShouldBeEmpty();
        editor.Services.ProjectManager.FileName.ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task OpeningAFileThatDoesNotLoad_OverContentWithoutATab_KeepsThatContent()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string broken = Path.Combine(editor.ProjectFolder, "broken.achx");
        File.WriteAllText(broken, Corrupt);
        editor.Services.AppCommands.AddAnimationChainWithName("Pasted");   // a paste into an empty editor opens no tab

        await editor.OpenAsync(broken);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

        editor.Project.AnimationChains.Select(chain => chain.Name).ShouldBe(new[] { "Pasted" });
    }

    [AvaloniaFact]
    public async Task OpeningAFile_OverContentWithoutATab_KeepsThatContentInItsOwnTab()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        editor.Services.AppCommands.AddAnimationChainWithName("Pasted");   // a paste into an empty editor opens no tab

        await editor.OpenAsync(hero);
        editor.ClickTab("Untitled");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

        editor.Project.AnimationChains.Select(chain => chain.Name).ShouldBe(new[] { "Pasted" });
    }

    [AvaloniaFact]
    public async Task GoingToAChainOfAFileThatNoLongerLoads_DoesNotSelectThatNameInTheFileShown()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string boss = editor.WriteAchx("boss.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        string hero = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Idle", "sheet.png", (0, 0, 16, 16)),
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(hero);
        File.WriteAllText(boss, Corrupt);
        editor.ClickRow(editor.ChainNamed("Idle"));

        await editor.Window.OpenAndSelectChainAsync(boss, "Walk");

        editor.Services.SelectedState.SelectedChain!.Name.ShouldBe("Idle");
    }

    [AvaloniaFact]
    public async Task OpeningAFile_AfterClosingOneWithAPngBehindIt_DoesNotBringTheClosedOneBack()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string png = editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        string boss = editor.WriteAchx("boss.achx", AnimationEditorHarness.Chain("Roar", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenPngTabAsync(png);
        await editor.OpenAsync(hero);
        editor.CloseTab("hero.achx");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

        await editor.OpenAsync(boss);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

        editor.TabLabels.ShouldBe(new[] { "sheet.png", "boss.achx" });
    }
}
