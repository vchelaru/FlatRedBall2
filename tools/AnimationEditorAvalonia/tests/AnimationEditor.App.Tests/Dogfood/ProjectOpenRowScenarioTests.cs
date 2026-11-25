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
}
