using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Saving onto a file another tab of this window holds is refused with the problem banner, whatever the format,
/// however the path is spelled, and whether the document has a file yet. Nothing is written.
/// </summary>
public class SaveAsOverOpenTabScenarioTests
{
    private const string TsxXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="16" columns="4">
         <image source="sheet.png" width="64" height="64"/>
        </tileset>
        """;

    /// <summary>Opens hero then boss and leaves hero in front.</summary>
    private static async Task<(string Hero, string Boss)> OpenTwoTabsAsync(AnimationEditorHarness editor, string heroName, string bossName)
    {
        editor.WritePng("sheet.png", 64, 64);
        string Write(string name)
        {
            AnimationChainSave walk = AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16));
            string path = Path.Combine(editor.ProjectFolder, name);
            if (name.EndsWith(".tsx"))
                File.WriteAllText(path, TsxXml);
            else if (name.EndsWith(".achj"))
                new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel, AnimationChains = { walk } }.SaveJson(path);
            else
                editor.WriteAchx(name, walk);
            return path;
        }
        string hero = Write(heroName), boss = Write(bossName);
        await editor.OpenAsync(hero);
        await editor.OpenAsync(boss);
        editor.ClickTab(heroName);
        return (hero, boss);
    }

    private static void SaveAs(AnimationEditorHarness editor, string path)
    {
        editor.Dialogs.AnswerNextSaveFile(path);
        editor.ClickMenu("MenuSaveAs");
        editor.Wait(TimeSpan.FromMilliseconds(100));
    }

    [AvaloniaTheory]
    [InlineData("hero.achx", "boss.achx", "boss.achx")]
    [InlineData("hero.achx", "boss.achx", "sub/../BOSS.achx")]
    [InlineData("hero.achx", "boss.achj", "boss.achj")]
    [InlineData("hero.tsx", "boss.tsx", "boss.tsx")]
    public async Task SaveAs_OntoAnotherTabsFile_IsRefused_AndWritesNothing(string heroName, string bossName, string picked)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        (string hero, string boss) = await OpenTwoTabsAsync(editor, heroName, bossName);
        File.Delete(boss); // Any write would bring it back, even one with the same content.

        SaveAs(editor, Path.Combine(editor.ProjectFolder, picked));

        editor.ProblemText.ShouldBe($"{Path.GetFileName(picked)} is open in another tab. Close that tab first.");
        File.Exists(boss).ShouldBeFalse();
        editor.TabLabels.ShouldBe(new[] { heroName, bossName });
        editor.Services.ProjectManager.FileName.ShouldBe(new AnimationEditor.Core.Paths.FilePath(hero).FullPath);
    }

    [AvaloniaFact]
    public async Task SavingAnUntitledTab_OntoAnotherTabsFile_IsRefused_AndStaysUntitled()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        (string hero, _) = await OpenTwoTabsAsync(editor, "hero.achx", "boss.achx");
        byte[] before = File.ReadAllBytes(hero);

        editor.Dialogs.AnswerNextSaveFile(hero);
        editor.Press(Key.N, RawInputModifiers.Control); // File > New asks where to save.
        editor.Wait(TimeSpan.FromMilliseconds(100));

        editor.ProblemText.ShouldBe("hero.achx is open in another tab. Close that tab first.");
        File.ReadAllBytes(hero).ShouldBe(before);
        editor.Services.ProjectManager.FileName.ShouldBeNull();
        editor.Tabs.Tabs.Count.ShouldBe(3);
    }

    [AvaloniaFact]
    public async Task SaveAs_OntoTheTabsOwnFile_OrAClosedTabsFile_Writes()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        (string hero, string boss) = await OpenTwoTabsAsync(editor, "hero.achx", "boss.achx");
        File.Delete(hero);

        SaveAs(editor, hero);

        File.Exists(hero).ShouldBeTrue();
        editor.CloseTab("boss.achx");
        File.Delete(boss);
        SaveAs(editor, boss);
        File.Exists(boss).ShouldBeTrue();
        editor.ProblemText.ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task SaveAs_ThatFailed_CanBeRetried()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await OpenTwoTabsAsync(editor, "hero.achx", "boss.achx");
        string target = editor.WriteAchx("target.achx", AnimationEditorHarness.Chain("Old", "sheet.png", (0, 0, 16, 16)));
        UnusualProjectScenarioTests.MakeUnwritable(target);
        SaveAs(editor, target);
        UnusualProjectScenarioTests.MakeWritable(target);

        SaveAs(editor, target);

        AnimationEditorHarness.ReadSaved(target).AnimationChains.Single().Name.ShouldBe("Walk");
    }
}
