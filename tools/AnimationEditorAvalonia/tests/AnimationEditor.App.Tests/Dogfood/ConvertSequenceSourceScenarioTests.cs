using AnimationEditor.Views.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

public class ConvertSequenceSourceScenarioTests
{
    /// <summary>hero.achx and enemy.achx in Sprites, and attack.seqj there playing one chain of each. Returns the path of attack.seqj.</summary>
    private static async Task<string> OpenProjectAsync(AnimationEditorHarness editor)
    {
        editor.WritePng("sheet.png", 64, 64);
        Directory.CreateDirectory(Path.Combine(editor.ProjectFolder, "Sprites"));
        editor.WriteAchx("Sprites/hero.achx", AnimationEditorHarness.Chain("Walk", "../sheet.png", (0, 0, 16, 16)));
        editor.WriteAchx("Sprites/enemy.achx", AnimationEditorHarness.Chain("Bite", "../sheet.png", (0, 0, 16, 16)));
        var track = new SequenceTrackSave { Name = "Body" };
        track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk", Start = 0 });
        track.Chains.Add(new SequenceChainReferenceSave { Source = "enemy.achx", Chain = "Bite", Start = 1 });
        track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Gone", Start = 2 }); // already broken, so a delete does not count it
        string seqj = Path.Combine(editor.ProjectFolder, "Sprites", "attack.seqj");
        new SequenceListSave { Sources = { "hero.achx", "enemy.achx" }, Sequences = { new SequenceSave { Name = "Attack", Tracks = { track } } } }.Save(seqj);
        await editor.OpenProjectFolderAsync();
        editor.Window.DeleteToRecycleBin = path => { File.Delete(path); return null; };
        return seqj;
    }

    private static void Pick(AnimationEditorHarness editor, string row, string item)
    {
        ProjectPanelControl panel = editor.Control<ProjectPanelControl>("ProjectPanel");
        TreeViewItem target = PanelsScenarioTests.RowFor(editor, panel, row);
        editor.RightClickAt(target.TranslatePoint(new Point(target.Bounds.Width / 2, 8), editor.Window)!.Value);
        editor.PickMenuItem(panel.ProjectTree.ContextMenu!, item);
    }

    private static string[] Sources(string seqj)
    {
        var save = SequenceListSave.FromFile(seqj);
        return save.Sources.Concat(save.Sequences.Single().Tracks.Single().Chains.Select(block => block.Source)).ToArray();
    }

    [AvaloniaFact]
    public async Task Delete_AFileThatASequencePlays_NamesTheBrokenBlocks()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string seqj = await OpenProjectAsync(editor);
        string before = File.ReadAllText(seqj);
        editor.Dialogs.AnswerNextConfirm(true);

        Pick(editor, "hero.achx", "Delete");

        (await editor.WaitUntilAsync(() => editor.ProblemText != null, TimeSpan.FromSeconds(5))).ShouldBeTrue();
        editor.ProblemText.ShouldBe("Deleting hero.achx broke 1 sequence block: attack.seqj (Attack/Body).");
        File.ReadAllText(seqj).ShouldBe(before);
    }

    [AvaloniaTheory]
    [InlineData("Convert to .achj", "hero.achx", "hero.achj enemy.achx hero.achj enemy.achx hero.achj")]
    [InlineData("Convert Animations to .achj", "Sprites", "hero.achj enemy.achj hero.achj enemy.achj hero.achj")]
    public async Task Convert_AFileASequencePlays_ShowsThePlan_AndTheSequenceKeepsPlaying(string item, string row, string sources)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string seqj = await OpenProjectAsync(editor);
        string said = "";
        editor.Dialogs.AnswerNextEditorDialog(content =>
        {
            said = string.Join("\n", content.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Inlines?.Text ?? block.Text));
            return true;
        });

        Pick(editor, row, item);

        string hero = Path.Combine(editor.ProjectFolder, "Sprites", "hero.achx");
        (await editor.WaitUntilAsync(() => !File.Exists(hero), TimeSpan.FromSeconds(5))).ShouldBeTrue();
        editor.Wait(TimeSpan.FromMilliseconds(100));
        Sources(seqj).ShouldBe(sources.Split(' '));
        editor.ProblemText.ShouldBeNull();
        said.ShouldContain("1 sequence file will be updated to keep playing.");
        said.ShouldContain("Sprites/hero.achx → hero.achj");
        said.ShouldContain("Sprites/attack.seqj");
        said.ShouldContain(Path.GetFullPath(editor.ProjectFolder));
    }

    [AvaloniaFact]
    public async Task Convert_Cancelled_TouchesNothing()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await OpenProjectAsync(editor);
        Dictionary<string, string> Files() => Directory.GetFiles(Path.Combine(editor.ProjectFolder, "Sprites")).ToDictionary(path => path, File.ReadAllText);
        var before = Files();
        editor.Dialogs.AnswerNextEditorDialog(_ => false);

        Pick(editor, "Sprites", "Convert Animations to .achj");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(300));

        editor.Dialogs.Shown.ShouldContain("editor dialog: Convert to .achj");
        Files().ShouldBe(before);
    }

    // In front, or behind another tab: its document, its file and its history move to the .achj together.
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Convert_AFileAnOpenSequencePlays_UpdatesItsTab_AndUndoStillWorks(bool behind)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string seqj = await OpenProjectAsync(editor);
        string other = editor.WriteAchx("other.achx", AnimationEditorHarness.Chain("Other", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(other);
        await editor.OpenAsync(seqj);
        editor.Control<Button>("AddSequenceButton").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        editor.Layout();
        editor.Press(Key.Escape);
        SequenceListSave.FromFile(seqj).Sequences.Count.ShouldBe(2);
        if (behind) editor.ClickTab("other.achx");
        editor.Dialogs.AnswerNextEditorDialog(_ => true);

        Pick(editor, "Sprites", "Convert Animations to .achj");
        string hero = Path.Combine(editor.ProjectFolder, "Sprites", "hero.achx");
        (await editor.WaitUntilAsync(() => !File.Exists(hero), TimeSpan.FromSeconds(5))).ShouldBeTrue();
        // Long enough for the live watcher to see its file replaced.
        await editor.WaitAsync(TimeSpan.FromMilliseconds(1200));

        editor.ProblemText.ShouldBeNull();
        editor.InfoText.ShouldBeNull("its own write is neither a delete nor an outside change");
        if (behind) editor.ClickTab("attack.seqj");
        editor.Press(Key.Z, RawInputModifiers.Control);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(100));

        SequenceListSave.FromFile(seqj).Sequences.Count.ShouldBe(1, "undo took the added sequence back");
        Sources(seqj).ShouldBe(["hero.achj", "enemy.achj", "hero.achj", "enemy.achj", "hero.achj"]);
    }
}
