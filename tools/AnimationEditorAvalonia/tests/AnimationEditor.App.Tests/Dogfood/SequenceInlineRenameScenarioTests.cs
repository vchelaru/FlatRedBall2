using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using AnimationEditor.Views.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Double-clicking a track's name on the timeline or a sequence's name in the list edits it in place, with the
/// ANIMATIONS tree's keys: Enter or clicking away commits, Escape cancels. A refused name shows the error banner and
/// keeps the old one.
/// </summary>
public class SequenceInlineRenameScenarioTests
{
    private static string WriteFixture(AnimationEditorHarness editor)
    {
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        var attack = new SequenceSave { Name = "Attack" };
        attack.Tracks.Add(new SequenceTrackSave { Name = "Body" });
        attack.Tracks.Add(new SequenceTrackSave { Name = "Arms" });
        save.Sequences.Add(attack);
        save.Sequences.Add(new SequenceSave { Name = "Idle" });
        save.Save(path);
        return path;
    }

    private static SequenceTimelineControl Timeline(AnimationEditorHarness editor) =>
        editor.Control<SequenceTimelineControl>("SequenceTimeline");

    private static ListBox List(AnimationEditorHarness editor) => editor.Control<ListBox>("SequenceList");

    private static Point OnHeader(AnimationEditorHarness editor, int lane) =>
        editor.PointIn(Timeline(editor), 20, Timeline(editor).Viewport.LaneTop(lane) + 10);

    private static InlineRenameBox? TrackBox(AnimationEditorHarness editor) =>
        Timeline(editor).GetVisualChildren().OfType<InlineRenameBox>().SingleOrDefault();

    private static InlineRenameBox ListBoxEditor(AnimationEditorHarness editor, int row) =>
        List(editor).ContainerFromIndex(row)!.GetVisualDescendants().OfType<InlineRenameBox>().Single();

    private static List<string> SavedTracks(string path) =>
        SequenceListSave.FromFile(path).Sequences[0].Tracks.Select(track => track.Name).ToList();

    private static List<string> SavedSequences(string path) =>
        SequenceListSave.FromFile(path).Sequences.Select(sequence => sequence.Name).ToList();

    [AvaloniaFact]
    public async Task DoubleClickingATrackName_EditsItInPlace_AndEnterCommitsOneUndoStep()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);
        TrackBox(editor).ShouldBeNull();

        editor.DoubleClickAt(OnHeader(editor, 1));

        var box = TrackBox(editor)!;
        box.IsEditing.ShouldBeTrue();
        box.Text.ShouldBe("Arms");
        editor.TypeAndEnter(box, "Legs");

        TrackBox(editor).ShouldBeNull();
        SavedTracks(boss).ShouldBe(new[] { "Body", "Legs" });
        editor.UndoLabels.ShouldBe(new[] { "Rename Track 'Arms' to 'Legs'" });
        editor.ProblemText.ShouldBeNull();
        editor.Press(Key.Z, RawInputModifiers.Control);
        SavedTracks(boss).ShouldBe(new[] { "Body", "Arms" });
    }

    [AvaloniaFact]
    public async Task RenamingATrackToATakenName_ShowsTheBanner_AndKeepsTheOldName()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);
        editor.DoubleClickAt(OnHeader(editor, 1));

        editor.TypeAndEnter(TrackBox(editor)!, "Body");

        editor.ProblemText.ShouldNotBeNull().ShouldContain("already exists");
        SavedTracks(boss).ShouldBe(new[] { "Body", "Arms" });
        editor.UndoLabels.ShouldBeEmpty();
        TrackBox(editor).ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task EscapeCancelsATrackRename_AndClickingAwayCommitsIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);

        editor.DoubleClickAt(OnHeader(editor, 0));
        var box = TrackBox(editor)!;
        box.Focus();
        box.SelectAll();
        editor.Type("Torso");
        editor.Press(Key.Escape);

        TrackBox(editor).ShouldBeNull();
        SavedTracks(boss).ShouldBe(new[] { "Body", "Arms" });
        editor.UndoLabels.ShouldBeEmpty();

        // A press at the same point right after would count as a third click, so start from another lane.
        editor.ClickAt(OnHeader(editor, 1));
        editor.DoubleClickAt(OnHeader(editor, 0));
        box = TrackBox(editor)!;
        box.Focus();
        box.SelectAll();
        editor.Type("Torso");
        editor.ClickAt(OnHeader(editor, 1));

        SavedTracks(boss).ShouldBe(new[] { "Torso", "Arms" });
        editor.UndoLabels.ShouldBe(new[] { "Rename Track 'Body' to 'Torso'" });
    }

    [AvaloniaFact]
    public async Task DoubleClickingASequence_EditsItInPlace_AndEnterCommitsOneUndoStep()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);

        editor.DoubleClickAt(editor.CenterOf((Control)List(editor).ContainerFromIndex(1)!));

        var box = ListBoxEditor(editor, 1);
        box.IsVisible.ShouldBeTrue();
        box.IsEditing.ShouldBeTrue();
        box.Text.ShouldBe("Idle");
        editor.TypeAndEnter(box, "Charge");

        editor.SequenceNames.ShouldBe(new[] { "Attack", "Charge" });
        SavedSequences(boss).ShouldBe(new[] { "Attack", "Charge" });
        editor.UndoLabels.ShouldBe(new[] { "Rename Sequence 'Idle' to 'Charge'" });
        List(editor).SelectedIndex.ShouldBe(1);
        editor.Press(Key.Z, RawInputModifiers.Control);
        editor.SequenceNames.ShouldBe(new[] { "Attack", "Idle" });
    }

    [AvaloniaFact]
    public async Task RenamingASequenceToATakenName_ShowsTheBanner_EscapeAndTheMenuStillWork()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);

        editor.DoubleClickAt(editor.CenterOf((Control)List(editor).ContainerFromIndex(1)!));
        editor.TypeAndEnter(ListBoxEditor(editor, 1), "Attack");

        editor.ProblemText.ShouldNotBeNull().ShouldContain("already exists");
        editor.SequenceNames.ShouldBe(new[] { "Attack", "Idle" });
        editor.UndoLabels.ShouldBeEmpty();
        ListBoxEditor(editor, 1).IsVisible.ShouldBeFalse();

        editor.DoubleClickAt(editor.CenterOf((Control)List(editor).ContainerFromIndex(1)!));
        var box = ListBoxEditor(editor, 1);
        box.Focus();
        box.SelectAll();
        editor.Type("Nope");
        editor.Press(Key.Escape);
        editor.SequenceNames.ShouldBe(new[] { "Attack", "Idle" });
        box.IsVisible.ShouldBeFalse();

        // The right-click item is still there.
        editor.RightClick((Control)List(editor).ContainerFromIndex(1)!);
        editor.PickMenuItem(List(editor).ContextMenu!, "Rename…");
        editor.TypeIntoOpenSequenceRename("Charge");
        editor.SequenceNames.ShouldBe(new[] { "Attack", "Charge" });
    }
}
