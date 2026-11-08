using AnimationEditor.App.Controls;
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
/// The sequence row menu's Rename… and the track header menu's Rename Track… edit the name in place, like the ANIMATIONS
/// tree's Rename…, F2 and Add Sequence: no prompt window. Enter or clicking away commits, Escape cancels.
/// </summary>
public class SequenceMenuRenameScenarioTests
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

    private static InlineRenameBox RowBox(AnimationEditorHarness editor, int row) =>
        List(editor).ContainerFromIndex(row)!.GetVisualDescendants().OfType<InlineRenameBox>().Single();

    private static void PickRowRename(AnimationEditorHarness editor, int row)
    {
        editor.RightClick((Control)List(editor).ContainerFromIndex(row)!);
        editor.PickMenuItem(List(editor).ContextMenu!, "Rename…");
    }

    private static void PickTrackRename(AnimationEditorHarness editor, int lane)
    {
        editor.RightClickAt(OnHeader(editor, lane));
        editor.PickMenuItem(Timeline(editor).Menu, "Rename Track…");
    }

    private static List<string> SavedTracks(string path) =>
        SequenceListSave.FromFile(path).Sequences[0].Tracks.Select(track => track.Name).ToList();

    [AvaloniaFact]
    public async Task SequenceMenuRename_EditsInPlace_AndEnterCommitsOneUndoStep()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);

        PickRowRename(editor, 1);

        var box = RowBox(editor, 1);
        box.IsVisible.ShouldBeTrue();
        box.IsEditing.ShouldBeTrue();
        box.Text.ShouldBe("Idle");
        editor.TypeAndEnter(box, "Charge");

        editor.SequenceNames.ShouldBe(new[] { "Attack", "Charge" });
        editor.UndoLabels.ShouldBe(new[] { "Rename Sequence 'Idle' to 'Charge'" });
        RowBox(editor, 1).IsEditing.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task SequenceMenuRename_EscapeCancels_AndClickingAwayCommits()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);

        PickRowRename(editor, 1);
        var box = RowBox(editor, 1);
        box.Focus();
        box.SelectAll();
        editor.Type("Nope");
        editor.Press(Key.Escape);

        box.IsEditing.ShouldBeFalse();
        editor.SequenceNames.ShouldBe(new[] { "Attack", "Idle" });
        editor.UndoLabels.ShouldBeEmpty();

        PickRowRename(editor, 1);
        box = RowBox(editor, 1);
        box.Focus();
        box.SelectAll();
        editor.Type("Charge");
        editor.ClickAt(editor.CenterOf((Control)List(editor).ContainerFromIndex(0)!));

        editor.SequenceNames.ShouldBe(new[] { "Attack", "Charge" });
        editor.UndoLabels.ShouldBe(new[] { "Rename Sequence 'Idle' to 'Charge'" });
    }

    [AvaloniaFact]
    public async Task TrackMenuRename_EditsInPlace_AndEnterCommitsOneUndoStep()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);

        PickTrackRename(editor, 1);

        var box = TrackBox(editor)!;
        box.IsEditing.ShouldBeTrue();
        box.Text.ShouldBe("Arms");
        editor.TypeAndEnter(box, "Legs");

        TrackBox(editor).ShouldBeNull();
        SavedTracks(boss).ShouldBe(new[] { "Body", "Legs" });
        editor.UndoLabels.ShouldBe(new[] { "Rename Track 'Arms' to 'Legs'" });
    }

    [AvaloniaFact]
    public async Task TrackMenuRename_EscapeCancels_AndClickingAwayCommits()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);

        PickTrackRename(editor, 0);
        var box = TrackBox(editor)!;
        box.Focus();
        box.SelectAll();
        editor.Type("Torso");
        editor.Press(Key.Escape);

        TrackBox(editor).ShouldBeNull();
        SavedTracks(boss).ShouldBe(new[] { "Body", "Arms" });
        editor.UndoLabels.ShouldBeEmpty();

        PickTrackRename(editor, 0);
        box = TrackBox(editor)!;
        box.Focus();
        box.SelectAll();
        editor.Type("Torso");
        editor.ClickAt(OnHeader(editor, 1));

        SavedTracks(boss).ShouldBe(new[] { "Torso", "Arms" });
        editor.UndoLabels.ShouldBe(new[] { "Rename Track 'Body' to 'Torso'" });
    }
}
