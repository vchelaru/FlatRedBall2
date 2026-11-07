using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using AnimationEditor.Views.Controls;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>The "+ new track" cell under the track headers: one click adds an empty track and selects it.</summary>
public class SequenceNewTrackScenarioTests
{
    private static string WriteFixture(AnimationEditorHarness editor, params string[] trackNames)
    {
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        var sequence = new SequenceSave { Name = "Attack" };
        foreach (string name in trackNames) sequence.Tracks.Add(new SequenceTrackSave { Name = name });
        save.Sequences.Add(sequence);
        save.Save(path);
        return path;
    }

    private static SequenceTimelineControl Timeline(AnimationEditorHarness editor) =>
        editor.Control<SequenceTimelineControl>("SequenceTimeline");

    /// <summary>A window point inside the "+ new track" cell of the header column.</summary>
    private static Point NewTrackCell(AnimationEditorHarness editor, int trackCount)
    {
        var timeline = Timeline(editor);
        return editor.PointIn(timeline, 30, timeline.Viewport.LaneTop(trackCount) + TimelineViewport.LaneHeight / 2);
    }

    [AvaloniaFact]
    public async Task ClickingNewTrack_AddsAnEmptyTrack_SelectsIt_AndUndoRedoKeepIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, "Track");
        await editor.OpenAsync(boss);
        var timeline = Timeline(editor);

        editor.ClickAt(NewTrackCell(editor, 1));

        SequenceListSave.FromFile(boss).Sequences[0].Tracks.Select(t => (t.Name, t.Chains.Count)).ShouldBe(new[] { ("Track", 0), ("Track 2", 0) });
        editor.UndoLabels.ShouldBe(new[] { "Add Track 'Track 2'" });
        timeline.SelectedTrack.ShouldBe(1);
        editor.Control<SequenceInspectorControl>("SequenceInspector").TrackNameBox.Text.ShouldBe("Track 2");

        // The new track's name is open for editing; Escape keeps the default and gives the keys back to the timeline.
        editor.Press(Key.Escape);
        editor.Press(Key.Z, RawInputModifiers.Control);
        SequenceListSave.FromFile(boss).Sequences[0].Tracks.Count.ShouldBe(1);
        editor.Press(Key.Y, RawInputModifiers.Control);
        SequenceListSave.FromFile(boss).Sequences[0].Tracks.Select(t => t.Name).ShouldBe(new[] { "Track", "Track 2" });
        timeline.Blocks.ShouldBeEmpty();
    }

    private static InlineRenameBox? TrackBox(AnimationEditorHarness editor) =>
        Timeline(editor).GetVisualChildren().OfType<InlineRenameBox>().SingleOrDefault();

    private static List<string> SavedTracks(string path) =>
        SequenceListSave.FromFile(path).Sequences[0].Tracks.Select(track => track.Name).ToList();

    [AvaloniaFact]
    public async Task ClickingNewTrack_OpensItsNameForEditing_WithTheDefaultSelected_AndTypingEnterRenamesIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, "Body");
        await editor.OpenAsync(boss);
        var timeline = Timeline(editor);

        editor.ClickAt(NewTrackCell(editor, 1));
        await editor.WaitAsync(TimeSpan.FromMilliseconds(100));

        timeline.IsRenamingTrack.ShouldBeTrue();
        var box = TrackBox(editor)!;
        box.Text.ShouldBe("Track");
        box.SelectedText.ShouldBe("Track");
        editor.Type("Legs");
        editor.Press(Key.Enter);

        timeline.IsRenamingTrack.ShouldBeFalse();
        SavedTracks(boss).ShouldBe(new[] { "Body", "Legs" });
        editor.UndoLabels.ShouldBe(new[] { "Add Track 'Track'", "Rename Track 'Track' to 'Legs'" });
        editor.Press(Key.Z, RawInputModifiers.Control);
        SavedTracks(boss).ShouldBe(new[] { "Body", "Track" });
        editor.Press(Key.Z, RawInputModifiers.Control);
        SavedTracks(boss).ShouldBe(new[] { "Body" });
    }

    [AvaloniaFact]
    public async Task EscapeOnTheNewTracksName_KeepsTheDefault_AndATakenOrEmptyNameShowsTheBanner()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, "Body");
        await editor.OpenAsync(boss);
        var timeline = Timeline(editor);

        editor.ClickAt(NewTrackCell(editor, 1));
        await editor.WaitAsync(TimeSpan.FromMilliseconds(100));
        editor.Type("Nope");
        editor.Press(Key.Escape);
        timeline.IsRenamingTrack.ShouldBeFalse();
        SavedTracks(boss).ShouldBe(new[] { "Body", "Track" });

        editor.ClickAt(NewTrackCell(editor, 2));
        await editor.WaitAsync(TimeSpan.FromMilliseconds(100));
        editor.Type("Body");
        editor.Press(Key.Enter);
        editor.ErrorBannerText.ShouldNotBeNull().ShouldContain("already exists");
        SavedTracks(boss).ShouldBe(new[] { "Body", "Track", "Track 2" });

        editor.ClickAt(NewTrackCell(editor, 3));
        await editor.WaitAsync(TimeSpan.FromMilliseconds(100));
        TrackBox(editor)!.Text = "  ";
        editor.Press(Key.Enter);
        editor.ErrorBannerText.ShouldNotBeNull().ShouldContain("needs a name");
        SavedTracks(boss).ShouldBe(new[] { "Body", "Track", "Track 2", "Track 3" });
        editor.UndoLabels.ShouldBe(new[] { "Add Track 'Track'", "Add Track 'Track 2'", "Add Track 'Track 3'" });
    }

    [AvaloniaFact]
    public async Task HoveringTheNewTrackCell_HighlightsItWithTheNormalCursor_AndLeavingClearsIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor, "Body"));
        var timeline = Timeline(editor);

        editor.Hover(NewTrackCell(editor, 1));
        timeline.IsNewTrackHot.ShouldBeTrue();
        timeline.ShownCursor.ShouldBe(TimelineCursor.Default);

        editor.Hover(editor.PointIn(timeline, 30, timeline.Viewport.LaneTop(0) + 20));
        timeline.IsNewTrackHot.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task ClickingTheNewTrackRowLane_AddsNothing()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor, "Body"));
        var timeline = Timeline(editor);

        editor.ClickAt(editor.PointIn(timeline, 400, timeline.Viewport.LaneTop(1) + 20));

        editor.UndoLabels.ShouldBeEmpty();
    }
}
