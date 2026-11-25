using AnimationEditor.Core.Paths;
using AnimationEditor.Views.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Opening a file that already has a tab: the file in front is left alone, and one behind another kind of tab comes
/// back with its history, however the path is spelled.
/// </summary>
public class ProjectOpenRowScenarioTests
{
    private static string WriteHero(AnimationEditorHarness editor)
    {
        editor.WritePng("sheet.png", 64, 64);
        return editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (16, 0, 16, 16)),
            AnimationEditorHarness.Chain("Jump", "sheet.png", (32, 0, 16, 16)));
    }

    [AvaloniaFact]
    public async Task ClickingTheRowOfTheFileInFront_KeepsTheSelectionAndTheHistory()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string hero = WriteHero(editor);
        await editor.OpenProjectFolderAsync();
        await editor.OpenAsync(hero);
        editor.Services.AppCommands.AddAnimationChainWithName("Idle");
        editor.Layout();
        editor.ClickRow(editor.ChainNamed("Walk"));
        editor.ClickRow(editor.ChainNamed("Jump"), RawInputModifiers.Control);
        List<string> history = editor.UndoLabels;
        history.ShouldNotBeEmpty();
        AnimationChainListSave project = editor.Project;

        editor.Click(PanelsScenarioTests.RowFor(editor, editor.Control<ProjectPanelControl>("ProjectPanel"), "hero.achx"));
        editor.Wait(TimeSpan.FromMilliseconds(100));

        editor.Services.SelectedState.SelectedChains.Select(chain => chain.Name).ShouldBe(new[] { "Walk", "Jump" });
        editor.UndoLabels.ShouldBe(history);
        editor.Project.ShouldBeSameAs(project);
    }

    // The undo stack is shared: the .seqj tab holds it while in front, though the .achx is still the loaded document.
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OpeningAFileBehindASequenceTab_BringsItsHistoryBack(bool forwardSlashes)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string hero = WriteHero(editor);
        string boss = editor.WriteSeqj("boss.seqj", "Attack");
        await editor.OpenAsync(hero);
        editor.Services.AppCommands.AddAnimationChainWithName("Idle");
        List<string> history = editor.UndoLabels;
        history.ShouldNotBeEmpty();
        await editor.OpenAsync(boss);
        editor.UndoLabels.ShouldBeEmpty();

        await editor.OpenAsync(forwardSlashes ? new FilePath(hero).FullPath : hero);

        editor.Tabs.ActiveTab!.DisplayName.ShouldBe("hero.achx");
        editor.UndoLabels.ShouldBe(history);
    }

    // With the watcher off, re-opening is how an outside change arrives; missing it, the next autosave overwrites it.
    [AvaloniaTheory]
    [InlineData("menu")]
    [InlineData("second instance")]
    [InlineData("row")]
    public async Task HotReloadOff_ReopeningTheFileInFront_TakesAnOutsideChange(string how)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenProjectFolderAsync();
        await editor.OpenAsync(hero);
        editor.ClickMenu("MenuEnableHotReload");
        editor.Services.AppCommands.HotReloadWatcher.IsEnabled.ShouldBeFalse();
        editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (16, 0, 16, 16)));

        if (how == "menu")
        {
            editor.Dialogs.AnswerNextOpenFile(hero);
            editor.ClickMenu("MenuLoad");
        }
        else if (how == "second instance")
            await editor.OpenAsync(hero);
        else
            editor.Click(PanelsScenarioTests.RowFor(editor, editor.Control<ProjectPanelControl>("ProjectPanel"), "hero.achx"));
        editor.Wait(TimeSpan.FromMilliseconds(150));
        editor.Services.AppCommands.AddAnimationChainWithName("Idle");
        editor.Wait(TimeSpan.FromMilliseconds(50));

        AnimationEditorHarness.ReadSaved(hero).AnimationChains.Select(chain => chain.Name).ShouldBe(new[] { "Walk", "Run", "Idle" });
    }
}
