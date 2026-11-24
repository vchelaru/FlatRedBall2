using AnimationEditor.Views.Dialogs;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Issue #1961: renaming or deleting an animation that sequences play raises one problem banner naming the blocks, after
/// a scan that never holds up the edit. Undo, redo and an animation no sequence plays say nothing, and no .seqj is written.
/// </summary>
public class BrokenSequenceBannerScenarioTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static string WriteFight(AnimationEditorHarness editor, string name, params string[] chains)
    {
        string path = Path.Combine(editor.ProjectFolder, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string source = name.Contains('/') ? "../hero.achx" : "hero.achx";
        var track = new SequenceTrackSave { Name = "Body" };
        track.Chains.AddRange(chains.Select(chain => new SequenceChainReferenceSave { Source = source, Chain = chain }));
        var save = new SequenceListSave();
        save.Sources.Add(source);
        save.Sequences.Add(new SequenceSave { Name = "Attack", Tracks = { track } });
        save.Save(path);
        return path;
    }

    private static string WriteHero(AnimationEditorHarness editor)
    {
        editor.WritePng("sheet.png", 64, 64);
        return editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (16, 0, 16, 16)),
            AnimationEditorHarness.Chain("Idle", "sheet.png", (32, 0, 16, 16)));
    }

    private static void Rename(AnimationEditorHarness editor, string chain, string newName)
    {
        editor.ClickRow(editor.ChainNamed(chain));
        editor.Press(Key.F2);
        editor.TypeAndEnter(editor.InlineRenameBox(editor.ChainNamed(chain)), newName);
        editor.ChainNamed(newName).ShouldNotBeNull();
    }

    private static async Task<string> OpenFightWithUnsavedEditsAsync(AnimationEditorHarness editor)
    {
        string fight = WriteFight(editor, "fight.seqj", "Walk");
        await editor.OpenAsync(fight);
        UnusualProjectScenarioTests.MakeUnwritable(fight);
        try
        {
            editor.Click(editor.Control<Button>("AddSequenceButton"));
            editor.ProblemText.ShouldNotBeNull().ShouldStartWith("Auto save failed");
            editor.Click(editor.Notifications.ErrorBannerDismissBtn);
        }
        finally
        {
            UnusualProjectScenarioTests.MakeWritable(fight);
        }
        return fight;
    }

    [AvaloniaFact]
    public async Task RenameThenDelete_NamesTheBrokenBlocks_AndUndoRedoSayNothing()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string hero = WriteHero(editor);
        string fight = WriteFight(editor, "fight.seqj", "Walk", "Run");
        string before = File.ReadAllText(fight);
        await editor.OpenAsync(hero);

        Rename(editor, "Walk", "Stride");

        (await editor.WaitUntilAsync(() => editor.ProblemText != null, Timeout)).ShouldBeTrue();
        editor.ProblemText.ShouldBe("Renaming 'Walk' broke 1 sequence block: fight.seqj (Attack/Body).");
        editor.Layout();
        editor.Click(editor.Notifications.ErrorBannerDismissBtn);

        editor.Press(Key.Z, RawInputModifiers.Control);
        editor.ChainNamed("Walk").ShouldNotBeNull();
        editor.Press(Key.Y, RawInputModifiers.Control);
        editor.ChainNamed("Stride").ShouldNotBeNull();
        await editor.Window.BrokenSequenceScan;
        editor.ProblemText.ShouldBeNull();

        editor.ClickRow(editor.ChainNamed("Run"));
        editor.Press(Key.Delete);

        (await editor.WaitUntilAsync(() => editor.ProblemText != null, Timeout)).ShouldBeTrue();
        editor.ProblemText.ShouldBe("Deleting 'Run' broke 1 sequence block: fight.seqj (Attack/Body).");
        File.ReadAllText(fight).ShouldBe(before);
    }

    [AvaloniaFact]
    public async Task RenameWithNoUsers_ShowsNothing()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string hero = WriteHero(editor);
        WriteFight(editor, "fight.seqj", "Walk", "Run");
        await editor.OpenAsync(hero);

        Rename(editor, "Idle", "Rest");

        await editor.Window.BrokenSequenceScan;
        editor.ProblemText.ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task UndoBeforeTheScanEnds_ShowsNothing()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string hero = WriteHero(editor);
        WriteFight(editor, "fight.seqj", "Walk");
        await editor.OpenAsync(hero);
        var gate = new TaskCompletionSource();
        editor.Window.BrokenSequenceScanGate = gate.Task;
        Rename(editor, "Walk", "Stride");
        editor.Press(Key.Z, RawInputModifiers.Control);

        gate.SetResult();
        await editor.Window.BrokenSequenceScan;

        editor.ProblemText.ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task TabClosedBeforeTheScanEnds_ShowsNothing()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string hero = WriteHero(editor);
        WriteFight(editor, "fight.seqj", "Walk");
        await editor.OpenAsync(hero);
        var gate = new TaskCompletionSource();
        editor.Window.BrokenSequenceScanGate = gate.Task;
        Rename(editor, "Walk", "Stride");
        editor.CloseTab("hero.achx");

        gate.SetResult();
        await editor.Window.BrokenSequenceScan;

        editor.ProblemText.ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task ProjectFolderOpen_ScansSubfolders_AndLeavesOutBinObj()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string hero = WriteHero(editor);
        WriteFight(editor, "Sequences/fight.seqj", "Walk");
        WriteFight(editor, "bin/copy.seqj", "Walk");
        await editor.OpenProjectFolderAsync();
        await editor.OpenAsync(hero);

        Rename(editor, "Walk", "Stride");

        (await editor.WaitUntilAsync(() => editor.ProblemText != null, Timeout)).ShouldBeTrue();
        editor.ProblemText.ShouldBe("Renaming 'Walk' broke 1 sequence block: fight.seqj (Attack/Body).");
    }

    [AvaloniaFact]
    public async Task CutIntoAnotherFile_NamesTheBlocksTheSourceFileLost()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string hero = WriteHero(editor);
        WriteFight(editor, "fight.seqj", "Walk");
        string villain = editor.WriteAchx("villain.achx", AnimationEditorHarness.Chain("Fly", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(hero);
        editor.ClickRow(editor.ChainNamed("Walk"));
        editor.Press(Key.X, RawInputModifiers.Control);
        await editor.OpenAsync(villain);
        editor.ClickRow(editor.ChainNamed("Fly"));

        editor.Press(Key.V, RawInputModifiers.Control);

        (await editor.WaitUntilAsync(() => editor.ProblemText != null, Timeout)).ShouldBeTrue();
        editor.ProblemText.ShouldBe("Cutting 'Walk' broke 1 sequence block: fight.seqj (Attack/Body).");
    }

    [AvaloniaFact]
    public async Task SeqjWithUnsavedEditsInATab_CountsAsTheTabHasIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string hero = WriteHero(editor);
        string fight = await OpenFightWithUnsavedEditsAsync(editor);
        await editor.OpenAsync(hero);
        // The file no longer plays Walk, but the tab's unsaved version does.
        File.WriteAllText(fight, File.ReadAllText(fight).Replace("Walk", "Idle"));

        Rename(editor, "Walk", "Stride");

        (await editor.WaitUntilAsync(() => editor.ProblemText != null, Timeout)).ShouldBeTrue();
        editor.ProblemText.ShouldBe("Renaming 'Walk' broke 1 sequence block: fight.seqj (Attack/Body).");
    }

    [AvaloniaFact]
    public async Task RenameWhoseSaveFails_KeepsTheSaveBanner()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string hero = WriteHero(editor);
        WriteFight(editor, "fight.seqj", "Walk");
        await editor.OpenAsync(hero);
        var gate = new TaskCompletionSource();
        editor.Window.BrokenSequenceScanGate = gate.Task;
        UnusualProjectScenarioTests.MakeUnwritable(hero);
        try
        {
            Rename(editor, "Walk", "Stride");
            editor.ProblemText.ShouldNotBeNull().ShouldStartWith("Auto save failed");

            gate.SetResult();
            await editor.Window.BrokenSequenceScan;

            editor.ProblemText.ShouldNotBeNull().ShouldStartWith("Auto save failed");
        }
        finally
        {
            UnusualProjectScenarioTests.MakeWritable(hero);
        }
    }

    [AvaloniaFact]
    public async Task CutIntoAnotherFileWhoseSourceSaveFails_KeepsTheSaveBanner()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string hero = WriteHero(editor);
        WriteFight(editor, "fight.seqj", "Walk");
        string villain = editor.WriteAchx("villain.achx", AnimationEditorHarness.Chain("Fly", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(hero);
        editor.ClickRow(editor.ChainNamed("Walk"));
        editor.Press(Key.X, RawInputModifiers.Control);
        await editor.OpenAsync(villain);
        editor.ClickRow(editor.ChainNamed("Fly"));
        var gate = new TaskCompletionSource();
        editor.Window.BrokenSequenceScanGate = gate.Task;
        UnusualProjectScenarioTests.MakeUnwritable(hero);
        try
        {
            editor.Press(Key.V, RawInputModifiers.Control);
            (await editor.WaitUntilAsync(() => editor.ProblemText != null, Timeout)).ShouldBeTrue();
            editor.ProblemText.ShouldStartWith("Auto save failed");

            gate.SetResult();
            await editor.Window.BrokenSequenceScan;

            editor.ProblemText.ShouldNotBeNull().ShouldStartWith("Auto save failed");
        }
        finally
        {
            UnusualProjectScenarioTests.MakeWritable(hero);
        }
    }

    [AvaloniaFact]
    public async Task UnsavedSeqjTabDiscardedBeforeTheScanEnds_CountsAsItsFile()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string hero = WriteHero(editor);
        string fight = await OpenFightWithUnsavedEditsAsync(editor);
        await editor.OpenAsync(hero);
        // The tab's unsaved version plays Walk; the file it falls back to does not.
        File.WriteAllText(fight, File.ReadAllText(fight).Replace("Walk", "Idle"));
        var gate = new TaskCompletionSource();
        editor.Window.BrokenSequenceScanGate = gate.Task;
        Rename(editor, "Walk", "Stride");
        editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Discard);
        editor.CloseTab("fight.seqj");
        editor.TabLabels.ShouldNotContain("fight.seqj");

        gate.SetResult();
        await editor.Window.BrokenSequenceScan;

        editor.ProblemText.ShouldBeNull();
    }
}
