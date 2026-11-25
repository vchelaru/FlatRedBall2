using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>Project panel operations on a file that is open with edits its file lacks.</summary>
public class ProjectFileOperationScenarioTests
{
    [AvaloniaTheory]
    [InlineData("hero.achx")]
    [InlineData("boss.seqj")]
    public async Task Duplicate_OfATabWhoseSaveFailsAgain_CopiesNothingAndSaysWhy(string fileName)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string file = fileName == "hero.achx" ? editor.WriteAchx(fileName) : editor.WriteSeqj(fileName, "Attack");
        await editor.OpenProjectFolderAsync();
        await editor.OpenAsync(file);
        UnusualProjectScenarioTests.MakeUnwritable(file);
        try
        {
            if (fileName == "hero.achx") editor.Services.AppCommands.AddAnimationChainWithName("Unsaved");
            else editor.Click(editor.Control<Button>("AddSequenceButton"));
            editor.Wait(TimeSpan.FromMilliseconds(50));

            await editor.Window.DuplicateProjectFileAsync(fileName);
            await editor.WaitAsync(TimeSpan.FromMilliseconds(100));

            editor.ProblemText!.ShouldStartWith($"Could not duplicate {fileName}: its unsaved edits could not be saved");
            editor.TabLabels.ShouldBe(new[] { fileName });
            Directory.GetFiles(editor.ProjectFolder, "*2.*").ShouldBeEmpty();
        }
        finally
        {
            UnusualProjectScenarioTests.MakeWritable(file);
        }
    }

    [AvaloniaFact]
    public async Task Duplicate_OfATabWithAFailedSave_CopiesTheEditsOrSaysWhyNot()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenProjectFolderAsync();
        await editor.OpenAsync(hero);
        UnusualProjectScenarioTests.MakeUnwritable(hero);
        editor.Services.AppCommands.AddAnimationChainWithName("Unsaved");
        editor.Wait(TimeSpan.FromMilliseconds(50));
        UnusualProjectScenarioTests.MakeWritable(hero);

        await editor.Window.DuplicateProjectFileAsync("hero.achx");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(100));

        string copy = Path.Combine(editor.ProjectFolder, "hero2.achx");
        bool copied = File.Exists(copy);
        if (copied)
        {
            AnimationEditorHarness.ReadSaved(copy).AnimationChains.Select(chain => chain.Name).ShouldContain("Unsaved");
        }
        else
        {
            editor.ProblemText.ShouldNotBeNull();
        }
    }

    [AvaloniaFact]
    public async Task Duplicate_Twice_OfASequenceFile_MakesTwoCopies()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WriteSeqj("boss.seqj", "Attack");
        await editor.OpenProjectFolderAsync();

        await editor.Window.DuplicateProjectFileAsync("boss.seqj");
        await editor.Window.DuplicateProjectFileAsync("boss.seqj");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(100));

        editor.ProblemText.ShouldBeNull();
        File.Exists(Path.Combine(editor.ProjectFolder, "boss3.seqj")).ShouldBeTrue();
    }

    [AvaloniaFact]
    public async Task Duplicate_WhenTheFileIsGone_ReportsIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WriteAchx("hero.achx");
        await editor.OpenProjectFolderAsync();
        Directory.CreateDirectory(Path.Combine(editor.ProjectFolder, "sub"));
        Directory.Delete(Path.Combine(editor.ProjectFolder, "sub"));

        await Should.NotThrowAsync(() => editor.Window.DuplicateProjectFileAsync("sub/hero.achx"));

        editor.ProblemText.ShouldNotBeNull();
    }
}
