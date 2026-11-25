using AnimationEditor.Views.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

public class ConvertSequenceSourceScenarioTests
{
    /// <summary>Every Project panel command that removes a file sequences play: the confirm's answer is Yes and the files are gone.</summary>
    [AvaloniaTheory]
    [InlineData("Delete", "hero.achx", "Deleting hero.achx broke 1 sequence block: attack.seqj (Attack/Body).")]
    [InlineData("Convert to .achj", "hero.achx", "Converting hero.achx broke 1 sequence block: attack.seqj (Attack/Body).")]
    [InlineData("Convert Animations to .achj", "Sprites", "Converting 2 files broke 2 sequence blocks: attack.seqj (Attack/Body).")]
    public async Task RemovingAFile_ThatASequencePlays_NamesTheBrokenBlocks(string item, string row, string banner)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        Directory.CreateDirectory(Path.Combine(editor.ProjectFolder, "Sprites"));
        editor.WriteAchx("Sprites/hero.achx", AnimationEditorHarness.Chain("Walk", "../sheet.png", (0, 0, 16, 16)));
        editor.WriteAchx("Sprites/enemy.achx", AnimationEditorHarness.Chain("Bite", "../sheet.png", (0, 0, 16, 16)));
        var track = new SequenceTrackSave { Name = "Body" };
        track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk", Start = 0 });
        track.Chains.Add(new SequenceChainReferenceSave { Source = "enemy.achx", Chain = "Bite", Start = 1 });
        track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Gone", Start = 2 }); // already broken, so not counted
        string seqj = Path.Combine(editor.ProjectFolder, "Sprites", "attack.seqj");
        new SequenceListSave { Sources = { "hero.achx", "enemy.achx" }, Sequences = { new SequenceSave { Name = "Attack", Tracks = { track } } } }.Save(seqj);
        string before = File.ReadAllText(seqj);
        await editor.OpenProjectFolderAsync();
        editor.Window.DeleteToRecycleBin = path => { File.Delete(path); return null; };
        ProjectPanelControl panel = editor.Control<ProjectPanelControl>("ProjectPanel");
        TreeViewItem target = PanelsScenarioTests.RowFor(editor, panel, row);
        editor.RightClickAt(target.TranslatePoint(new Point(target.Bounds.Width / 2, 8), editor.Window)!.Value);
        editor.Dialogs.AnswerNextConfirm(true);

        editor.PickMenuItem(panel.ProjectTree.ContextMenu!, item);

        (await editor.WaitUntilAsync(() => editor.ProblemText != null, TimeSpan.FromSeconds(5))).ShouldBeTrue();
        editor.ProblemText.ShouldBe(banner);
        File.ReadAllText(seqj).ShouldBe(before);
    }

    [AvaloniaFact]
    public async Task Convert_AFileASequencePlays_KeepsTheSequenceOrSaysItBreaks()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        Directory.CreateDirectory(Path.Combine(editor.ProjectFolder, "Sprites"));
        editor.WriteAchx("Sprites/hero.achx", AnimationEditorHarness.Chain("Walk", "../sheet.png", (0, 0, 16, 16)));
        var track = new SequenceTrackSave { Name = "Body" };
        track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk", Start = 0 });
        string seqj = Path.Combine(editor.ProjectFolder, "Sprites", "attack.seqj");
        new SequenceListSave { Sources = { "hero.achx" }, Sequences = { new SequenceSave { Name = "Attack", Tracks = { track } } } }.Save(seqj);
        await editor.OpenProjectFolderAsync();
        editor.Window.DeleteToRecycleBin = path => { File.Delete(path); return null; };
        ProjectPanelControl panel = editor.Control<ProjectPanelControl>("ProjectPanel");
        TreeViewItem target = PanelsScenarioTests.RowFor(editor, panel, "hero.achx");
        editor.RightClickAt(target.TranslatePoint(new Point(target.Bounds.Width / 2, 8), editor.Window)!.Value);
        editor.Dialogs.Shown.Clear();
        editor.Dialogs.AnswerNextConfirm(true);

        editor.PickMenuItem(panel.ProjectTree.ContextMenu!, "Convert to .achj");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(1000));

        File.Exists(Path.Combine(editor.ProjectFolder, "Sprites", "hero.achx")).ShouldBeFalse();
        string source = SequenceListSave.FromFile(seqj).Sources.Single();
        bool plays = File.Exists(Path.Combine(editor.ProjectFolder, "Sprites", source));
        bool said = editor.ProblemText?.Contains("attack") == true || editor.Dialogs.Shown.Any(text => text.Contains("attack"));
        (plays || said).ShouldBeTrue($"attack.seqj still names {source}, banner: {editor.ProblemText}, dialogs: {string.Join(";", editor.Dialogs.Shown)}");
    }
}
