using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// A <c>.seqj</c> that changed on disk while its tab was behind another one (a git pull is the common case) is read
/// again when the tab comes back, and File &gt; Reload from Disk does the same on demand. No watcher runs for a hidden tab,
/// so only the switch-back or the menu can notice.
/// </summary>
public class SequenceDiskCatchUpScenarioTests
{
    private static ListBox List(AnimationEditorHarness editor) => editor.Control<ListBox>("SequenceList");

    // boss.seqj (Attack, Idle) is open and hero.achx is in front of it.
    private static async Task<(string Seqj, string Hero)> OpenSeqjThenAchxAsync(AnimationEditorHarness editor)
    {
        editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        string seqj = editor.WriteSeqj("boss.seqj", "Attack", "Idle");
        await editor.OpenAsync(seqj);
        await editor.OpenAsync(hero);
        return (seqj, hero);
    }

    private static void Rewrite(string seqj, params string[] names)
    {
        var save = new SequenceListSave();
        save.Sequences.AddRange(names.Select(name => new SequenceSave { Name = name }));
        save.Save(seqj);
    }

    [AvaloniaFact]
    public async Task SeqjRewrittenWhileAnotherTabIsInFront_IsReadAgainOnSwitchBack_AndTheNextEditKeepsIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (seqj, _) = await OpenSeqjThenAchxAsync(editor);

        Rewrite(seqj, "Intro", "Attack", "Idle");
        editor.ClickTab("boss.seqj");
        editor.Layout();

        editor.SequenceNames.ShouldBe(new[] { "Intro", "Attack", "Idle" });
        List(editor).SelectedIndex.ShouldBe(1, "the selected sequence is followed by name");
        editor.UndoLabels.ShouldBeEmpty("history of the old file does not apply to the new one");
        editor.InfoText!.ShouldContain("Reloaded boss.seqj");

        editor.Click(editor.Control<Button>("AddSequenceButton"));
        await editor.WaitAsync(TimeSpan.FromMilliseconds(300));
        SequenceListSave.FromFile(seqj).Sequences.Select(s => s.Name).ShouldBe(new[] { "Intro", "Attack", "Idle", "Sequence" },
            customMessage: "the foreign change must survive the next save");
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task SeqjUntouchedWhileAway_SwitchBackKeepsTheUndoHistory_AndShowsNoToast()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        string seqj = editor.WriteSeqj("boss.seqj", "Attack");
        await editor.OpenAsync(seqj);
        editor.Click(editor.Control<Button>("AddSequenceButton"));
        await editor.OpenAsync(hero);
        await editor.WaitAsync(TimeSpan.FromSeconds(4));

        editor.ClickTab("boss.seqj");
        editor.Layout();

        editor.SequenceNames.ShouldBe(new[] { "Attack", "Sequence" });
        editor.UndoLabels.ShouldBe(new[] { "Add Sequence 'Sequence'" });
        editor.InfoText.ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task SeqjBecameInvalidWhileAway_SwitchBackShowsTheBanner_KeepsTheDocument_PausesSaving_AndRecoversWhenFixed()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (seqj, _) = await OpenSeqjThenAchxAsync(editor);
        const string foreign = "{ \"sequences\": [ { \"name\": \"Hal";
        File.WriteAllText(seqj, foreign);

        editor.ClickTab("boss.seqj");
        editor.Layout();

        editor.ProblemText.ShouldNotBeNull();
        editor.ProblemText!.ShouldContain("boss.seqj");
        editor.SequenceNames.ShouldBe(new[] { "Attack", "Idle" });
        editor.Click(editor.Control<Button>("AddSequenceButton"));
        await editor.WaitAsync(TimeSpan.FromMilliseconds(300));
        File.ReadAllText(seqj).ShouldBe(foreign, "saving is paused so the other program's file is not destroyed");

        Rewrite(seqj, "Attack", "Idle", "Die");
        (await editor.WaitUntilAsync(() => editor.SequenceNames.Contains("Die"), TimeSpan.FromSeconds(6)))
            .ShouldBeTrue("the next valid file recovers through the live path");
        editor.SequenceNames.ShouldBe(new[] { "Attack", "Idle", "Die" });
    }

    [AvaloniaFact]
    public async Task SeqjDeletedWhileAway_SwitchBackToastsLikeTheLivePath_AndKeepsTheDocument()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (seqj, _) = await OpenSeqjThenAchxAsync(editor);

        File.Delete(seqj);
        editor.ClickTab("boss.seqj");
        editor.Layout();

        editor.InfoText!.ShouldContain("deleted from disk");
        editor.TabLabels.ShouldContain("boss.seqj");
        editor.SequenceNames.ShouldBe(new[] { "Attack", "Idle" });
    }

    [AvaloniaFact]
    public async Task HotReloadOff_SwitchBackStillReadsAChangedSeqj_SoTheNextSaveCannotOverwriteIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (seqj, _) = await OpenSeqjThenAchxAsync(editor);
        editor.ClickMenu("MenuEnableHotReload");

        Rewrite(seqj, "Attack", "Idle", "Die");
        editor.ClickTab("boss.seqj");
        editor.Layout();

        editor.SequenceNames.ShouldBe(new[] { "Attack", "Idle", "Die" });
    }

    [AvaloniaFact]
    public async Task ReloadFromDiskMenu_OnASequenceTab_IsEnabled_AndReadsTheFileAgain()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string seqj = editor.WriteSeqj("boss.seqj", "Attack", "Idle");
        await editor.OpenAsync(seqj);
        editor.Control<MenuItem>("MenuReloadFromDisk").IsEnabled.ShouldBeTrue();
        // With hot reload off nothing but the menu can bring the change in.
        editor.ClickMenu("MenuEnableHotReload");

        Rewrite(seqj, "Intro", "Attack", "Idle");
        editor.ClickMenu("MenuReloadFromDisk");

        editor.SequenceNames.ShouldBe(new[] { "Intro", "Attack", "Idle" });
        List(editor).SelectedIndex.ShouldBe(1);
        editor.UndoLabels.ShouldBeEmpty();
        editor.InfoText!.ShouldContain("Reloaded boss.seqj");
    }

    [AvaloniaFact]
    public async Task ReloadFromDiskMenu_DiscardsEditsThatNeverReachedTheFile_LikeAnAchx()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string seqj = editor.WriteSeqj("boss.seqj", "Attack");
        await editor.OpenAsync(seqj);
        editor.ClickMenu("MenuEnableHotReload");
        // An edit made while the file could not be read (saving paused) is the one kind that never reaches the file.
        File.WriteAllText(seqj, "{ nope");
        editor.ClickMenu("MenuReloadFromDisk");
        editor.Click(editor.Control<Button>("AddSequenceButton"));
        editor.SequenceNames.ShouldBe(new[] { "Attack", "Sequence" });

        Rewrite(seqj, "Attack", "Idle");
        editor.ClickMenu("MenuReloadFromDisk");

        editor.SequenceNames.ShouldBe(new[] { "Attack", "Idle" });
    }

    [AvaloniaFact]
    public async Task ReloadFromDiskMenu_InvalidFile_ShowsTheBanner_KeepsTheDocument_AndRecoversWhenValidAgain()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string seqj = editor.WriteSeqj("boss.seqj", "Attack", "Idle");
        await editor.OpenAsync(seqj);
        editor.ClickMenu("MenuEnableHotReload");

        File.WriteAllText(seqj, "{ nope");
        editor.ClickMenu("MenuReloadFromDisk");

        editor.ProblemText!.ShouldContain("boss.seqj");
        editor.SequenceNames.ShouldBe(new[] { "Attack", "Idle" });

        Rewrite(seqj, "Attack", "Idle", "Die");
        editor.ClickMenu("MenuReloadFromDisk");

        editor.SequenceNames.ShouldBe(new[] { "Attack", "Idle", "Die" });
    }

    [AvaloniaFact]
    public async Task ReloadFromDiskMenu_SeqjDeleted_ToastsAndKeepsTheDocument()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string seqj = editor.WriteSeqj("boss.seqj", "Attack");
        await editor.OpenAsync(seqj);
        editor.ClickMenu("MenuEnableHotReload");

        File.Delete(seqj);
        editor.ClickMenu("MenuReloadFromDisk");

        editor.InfoText!.ShouldContain("deleted from disk");
        editor.SequenceNames.ShouldBe(new[] { "Attack" });
    }
}
