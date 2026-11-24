using Avalonia.Headless.XUnit;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>An export or File &gt; New Sequence File onto a file it must not overwrite: the problem banner says why, and the file stays as it is.</summary>
public class OverwriteRefusalScenarioTests
{
    private static async Task OpenAsync(AnimationEditorHarness editor)
    {
        editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(hero);
        editor.ClickRow(editor.ChainNamed("Walk"));
    }

    [AvaloniaTheory]
    [InlineData("MenuExportPixiJs", "hero.achx", "a .json")]
    [InlineData("MenuExportGodot", "boss.tsx", "a .tres")]
    [InlineData("MenuExportGif", "hero.achx", "a .gif")]
    [InlineData("MenuExportGif", "boss.seqj", "a .gif")]
    [InlineData("MenuExportWebp", "sheet.png", "a .webp")]
    public async Task Export_AnotherTypesExtension_ShowsTheProblemBanner_AndWritesNothing(string menu, string picked, string kind)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await OpenAsync(editor);
        string path = Path.Combine(editor.ProjectFolder, picked);
        if (!File.Exists(path)) File.WriteAllText(path, "another file");
        byte[] before = File.ReadAllBytes(path);

        if (menu is "MenuExportGif" or "MenuExportWebp") editor.Dialogs.AnswerNextEditorDialog(_ => true);
        editor.Dialogs.AnswerNextSaveFile(path);
        editor.ClickMenu(menu);
        await editor.WaitUntilAsync(() => editor.InfoText is not null || editor.ProblemText is not null, TimeSpan.FromSeconds(10));

        editor.ProblemText.ShouldBe($"{picked} is not {kind} file. Give it {kind} extension.");
        File.ReadAllBytes(path).ShouldBe(before);
    }

    // The front tab's own file counts too: the new file would replace the document under it.
    [AvaloniaTheory]
    [InlineData("boss.seqj")]
    [InlineData("hero.achx")]
    public async Task NewSequenceFile_OntoAFileOpenInATab_IsRefused(string inFront)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await OpenAsync(editor);
        string boss = editor.WriteSeqj("boss.seqj", "Intro", "Attack");
        await editor.OpenAsync(boss);
        editor.ClickTab(inFront);
        string before = File.ReadAllText(boss);

        editor.Dialogs.AnswerNextSaveFile(Path.Combine(editor.ProjectFolder, "sub", "..", "boss.seqj"));
        editor.ClickMenu("MenuNewSequence");
        editor.Wait(TimeSpan.FromMilliseconds(100));

        editor.ProblemText.ShouldBe("boss.seqj is open in another tab. Close that tab first.");
        File.ReadAllText(boss).ShouldBe(before);
        editor.TabLabels.ShouldBe(new[] { "hero.achx", "boss.seqj" });
        editor.ClickTab("boss.seqj");
        editor.SequenceNames.ShouldBe(new[] { "Intro", "Attack" });
    }

    [AvaloniaFact]
    public void NewSequenceFile_OntoAClosedSeqj_OverwritesItAndOpensIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", "Intro", "Attack");

        editor.Dialogs.AnswerNextSaveFile(boss);
        editor.ClickMenu("MenuNewSequence");
        editor.WaitUntil(() => editor.TabLabels.Contains("boss.seqj"), TimeSpan.FromSeconds(5)).ShouldBeTrue();

        editor.SequenceNames.ShouldBe(new[] { "Sequence" });
    }

    [AvaloniaTheory]
    [InlineData("boss.json")]
    [InlineData("boss")]
    [InlineData("boss.seqj.")]
    public void NewSequenceFile_NotASeqjName_IsRefused_AndWritesNothing(string picked)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();

        editor.Dialogs.AnswerNextSaveFile(Path.Combine(editor.ProjectFolder, picked));
        editor.ClickMenu("MenuNewSequence");
        editor.Wait(TimeSpan.FromMilliseconds(100));

        editor.ProblemText.ShouldBe($"Could not create {picked}: it is not a .seqj file. Give it a .seqj extension.");
        Directory.GetFiles(editor.ProjectFolder).ShouldBeEmpty();
    }
}
