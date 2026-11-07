using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Delete removes whatever was last clicked (a sequence row, a track or a chain), and undo and redo of a sequence
/// add, rename or delete leave the list and the Inspector on the sequence that changed.
/// </summary>
public class SequenceListSelectionScenarioTests
{
    private static ListBox List(AnimationEditorHarness editor) => editor.Control<ListBox>("SequenceList");

    private static SequenceTimelineControl Timeline(AnimationEditorHarness editor) =>
        editor.Control<SequenceTimelineControl>("SequenceTimeline");

    private static SequenceInspectorControl Inspector(AnimationEditorHarness editor) =>
        editor.Control<SequenceInspectorControl>("SequenceInspector");

    private static void ClickRow(AnimationEditorHarness editor, int row) => editor.Click((Control)List(editor).ContainerFromIndex(row)!);

    private static List<string> SavedNames(string path) =>
        SequenceListSave.FromFile(path).Sequences.Select(sequence => sequence.Name).ToList();

    private static void Undo(AnimationEditorHarness editor) => editor.Press(Key.Z, RawInputModifiers.Control);

    private static void Redo(AnimationEditorHarness editor) => editor.Press(Key.Y, RawInputModifiers.Control);

    /// <summary>The list selection and what the Inspector shows must agree on the same sequence.</summary>
    private static void ShouldShowSequence(AnimationEditorHarness editor, int index, string name)
    {
        List(editor).SelectedIndex.ShouldBe(index);
        Inspector(editor).SequencePanel.IsVisible.ShouldBeTrue();
        Inspector(editor).SequenceNameBox.Text.ShouldBe(name);
    }

    // Attack has one track with two chains; Idle and Walk are empty.
    private static string WriteFixture(AnimationEditorHarness editor)
    {
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16), (48, 0, 16, 16), (0, 16, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (0, 32, 16, 16), (16, 32, 16, 16)));
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        var attack = new SequenceSave { Name = "Attack" };
        var body = new SequenceTrackSave { Name = "Body" };
        body.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk", Start = 0, Loops = 1 });
        body.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Run", Start = 1, Loops = 1 });
        attack.Tracks.Add(body);
        save.Sequences.Add(attack);
        save.Sequences.Add(new SequenceSave { Name = "Idle" });
        save.Sequences.Add(new SequenceSave { Name = "Walk" });
        save.Save(path);
        return path;
    }

    private static Point OnChain(AnimationEditorHarness editor, double time)
    {
        var timeline = Timeline(editor);
        double y = timeline.Viewport.LaneTop(0) + (TimelineLayout.BodyTop + TimelineLayout.BodyBottom) / 2;
        return editor.PointIn(timeline, timeline.Viewport.XAt(time), y);
    }

    private static Point OnHeader(AnimationEditorHarness editor) =>
        editor.PointIn(Timeline(editor), 20, Timeline(editor).Viewport.LaneTop(0) + 10);

    [AvaloniaFact]
    public async Task Delete_RemovesWhatWasLastClicked_ChainThenTrackThenSequenceRow()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);

        editor.ClickAt(OnChain(editor, 1.1));
        editor.Press(Key.Delete);
        SavedNames(boss).ShouldBe(new[] { "Attack", "Idle", "Walk" });
        SequenceListSave.FromFile(boss).Sequences[0].Tracks.Single().Chains.Select(c => c.Chain).ShouldBe(new[] { "Walk" });

        editor.ClickAt(OnHeader(editor));
        editor.Press(Key.Delete);
        SequenceListSave.FromFile(boss).Sequences[0].Tracks.ShouldBeEmpty();
        SavedNames(boss).ShouldBe(new[] { "Attack", "Idle", "Walk" });

        ClickRow(editor, 1);
        editor.Press(Key.Delete);
        SavedNames(boss).ShouldBe(new[] { "Attack", "Walk" });
        editor.UndoLabels.ShouldBe(new[] { "Remove 'Run' from 'Body'", "Delete Track 'Body'", "Delete Sequence 'Idle'" });
    }

    [AvaloniaFact]
    public async Task Delete_OnASequenceRow_IsOneUndoStep_AndAPressAfterItDeletesTheNextOne()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", "A", "B", "C");
        await editor.OpenAsync(boss);

        ClickRow(editor, 0);
        editor.Press(Key.Delete);
        editor.Press(Key.Delete);

        SavedNames(boss).ShouldBe(new[] { "C" });
        editor.UndoLabels.ShouldBe(new[] { "Delete Sequence 'A'", "Delete Sequence 'B'" });
        Undo(editor);
        SavedNames(boss).ShouldBe(new[] { "B", "C" });
    }

    [AvaloniaFact]
    public async Task Delete_WhileRenamingASequenceInPlace_EditsTheTextAndDeletesNothing()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", "A", "B");
        await editor.OpenAsync(boss);

        editor.DoubleClickAt(editor.CenterOf((Control)List(editor).ContainerFromIndex(1)!));
        editor.Press(Key.Delete);

        SavedNames(boss).ShouldBe(new[] { "A", "B" });
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task Delete_InTheInspector_DeletesNothing()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", "A", "B");
        await editor.OpenAsync(boss);
        ClickRow(editor, 0);
        Inspector(editor).SequenceNameBox.Focus();
        editor.Layout();

        editor.Press(Key.Delete);

        SavedNames(boss).ShouldBe(new[] { "A", "B" });
    }

    [AvaloniaFact]
    public async Task UndoOfDeletingTheFirst_ReselectsIt_AndRedoSelectsTheOneThatTookItsPlace()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(editor.WriteSeqj("boss.seqj", "A", "B", "C"));
        ClickRow(editor, 0);

        editor.Press(Key.Delete);
        ShouldShowSequence(editor, 0, "B");
        Undo(editor);
        editor.SequenceNames.ShouldBe(new[] { "A", "B", "C" });
        ShouldShowSequence(editor, 0, "A");
        Redo(editor);
        editor.SequenceNames.ShouldBe(new[] { "B", "C" });
        ShouldShowSequence(editor, 0, "B");
    }

    [AvaloniaFact]
    public async Task UndoOfDeletingTheLast_ReselectsItAtTheEnd_AndRedoSelectsTheNewLast()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(editor.WriteSeqj("boss.seqj", "A", "B", "C"));
        ClickRow(editor, 2);

        editor.Press(Key.Delete);
        ShouldShowSequence(editor, 1, "B");
        Undo(editor);
        editor.SequenceNames.ShouldBe(new[] { "A", "B", "C" });
        ShouldShowSequence(editor, 2, "C");
        Redo(editor);
        ShouldShowSequence(editor, 1, "B");
    }

    [AvaloniaFact]
    public async Task UndoOfDeletingAMiddleOne_ReselectsItInPlace_EvenAfterClickingAnotherRow()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(editor.WriteSeqj("boss.seqj", "A", "B", "C"));
        ClickRow(editor, 1);
        editor.Press(Key.Delete);
        ClickRow(editor, 0);

        Undo(editor);

        editor.SequenceNames.ShouldBe(new[] { "A", "B", "C" });
        ShouldShowSequence(editor, 1, "B");
    }

    [AvaloniaFact]
    public async Task DeletingTheOnlySequence_EmptiesTheList_AndUndoSelectsItAgain()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(editor.WriteSeqj("boss.seqj", "Only"));
        ClickRow(editor, 0);

        editor.Press(Key.Delete);
        editor.SequenceNames.ShouldBeEmpty();
        List(editor).SelectedIndex.ShouldBe(-1);
        Undo(editor);

        ShouldShowSequence(editor, 0, "Only");
    }

    [AvaloniaFact]
    public async Task UndoAndRedoOfAddSequence_SelectThePreviousOne_ThenTheNewOne()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(editor.WriteSeqj("boss.seqj", "A", "B"));
        ClickRow(editor, 0);
        editor.Click(editor.Control<Button>("AddSequenceButton"));
        ShouldShowSequence(editor, 2, "Sequence");

        Undo(editor);
        ShouldShowSequence(editor, 0, "A");
        Redo(editor);
        ShouldShowSequence(editor, 2, "Sequence");
    }

    [AvaloniaFact]
    public async Task UndoAndRedoOfRenameSequence_SelectTheRenamedOne()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(editor.WriteSeqj("boss.seqj", "A", "B", "C"));
        ClickRow(editor, 1);
        editor.Dialogs.AnswerNextPrompt("Beta");
        editor.RightClick((Control)List(editor).ContainerFromIndex(1)!);
        editor.PickMenuItem(List(editor).ContextMenu!, "Rename…");
        editor.Layout();
        ClickRow(editor, 2);

        Undo(editor);
        ShouldShowSequence(editor, 1, "B");
        ClickRow(editor, 0);
        Redo(editor);
        ShouldShowSequence(editor, 1, "Beta");
    }
}
