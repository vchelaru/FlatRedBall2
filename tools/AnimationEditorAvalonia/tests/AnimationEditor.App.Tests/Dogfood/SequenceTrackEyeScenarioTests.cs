using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using AnimationEditor.Views.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FlatRedBall2.Animation;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// The eye on a track header is the track's visibility: it hides the track in the sequence preview, is saved in the
/// <c>.seqj</c> as <c>"visible": false</c> (runtimes may skip such a track), is one undo entry per toggle, and a hidden
/// track keeps its lane and blocks on the timeline, drawn faint.
/// </summary>
public class SequenceTrackEyeScenarioTests
{
    // Action has Moon (z 0) and BlackMoon (z 1), both playing Walk from 0. Sun plays Flash, which has a frame event, so its
    // lane is taller than the others.
    private static void WriteSeqj(string folder, string path, params string[] hidden)
    {
        var chains = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        var walk = new AnimationChainSave { Name = "Walk" };
        walk.Frames.Add(new AnimationFrameSave { TextureName = "hero.png", FrameLength = 0.1f, RightCoordinate = 16, BottomCoordinate = 16 });
        chains.AnimationChains.Add(walk);
        var flash = new AnimationChainSave { Name = "Flash" };
        flash.Frames.Add(new AnimationFrameSave { TextureName = "hero.png", FrameLength = 0.1f, RightCoordinate = 16, BottomCoordinate = 16 });
        flash.Frames[0].Events.Add(new AnimationFrameEvent { Name = "Blink" });
        chains.AnimationChains.Add(flash);
        chains.Save(Path.Combine(folder, "hero.achx"));
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        var action = new SequenceSave { Name = "Action" };
        foreach (var (name, chain) in new[] { ("Moon", "Walk"), ("BlackMoon", "Walk"), ("Sun", "Flash") })
        {
            var track = new SequenceTrackSave { Name = name, Visible = !hidden.Contains(name) };
            track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = chain, Start = 0, Loops = 1 });
            action.Tracks.Add(track);
        }
        save.Sequences.Add(action);
        save.Save(path);
    }

    private static string WriteFixture(AnimationEditorHarness editor, params string[] hidden)
    {
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        WriteSeqj(editor.ProjectFolder, path, hidden);
        return path;
    }

    private static SequenceTimelineControl Timeline(AnimationEditorHarness editor) =>
        editor.Control<SequenceTimelineControl>("SequenceTimeline");

    private static Point OnEye(AnimationEditorHarness editor, int lane) =>
        editor.PointIn(Timeline(editor), TimelineLayout.EyeCenterX, Timeline(editor).HeaderCenters(lane).Eye);

    private static Point OnName(AnimationEditorHarness editor, int lane) =>
        editor.PointIn(Timeline(editor), 60, Timeline(editor).HeaderCenters(lane).Name);

    private static List<int> ShownTracks(AnimationEditorHarness editor) =>
        editor.Control<PreviewControl>("SequencePreview").Composite!.Select(sprite => sprite.Track).ToList();

    private static List<string> Menu(SequenceTimelineControl timeline) =>
        timeline.Menu.Items.OfType<MenuItem>().Select(item => (string)item.Header!).ToList();

    [AvaloniaFact]
    public async Task ClickingTheEye_HidesThatTrackInThePreview_SavesVisibleFalse_AndIsOneUndoStep()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);
        // A file without the field: everything shows.
        ShownTracks(editor).ShouldBe(new[] { 0, 1, 2 });
        File.ReadAllText(boss).ShouldNotContain("visible");

        editor.ClickAt(OnEye(editor, 0));

        Timeline(editor).IsTrackHidden(0).ShouldBeTrue();
        ShownTracks(editor).ShouldBe(new[] { 1, 2 });
        Timeline(editor).SelectedTrack.ShouldBeNull();
        Timeline(editor).IsRenamingTrack.ShouldBeFalse();
        editor.UndoLabels.ShouldBe(new[] { "Hide Track 'Moon'" });
        var saved = SequenceListSave.FromFile(boss).Sequences[0].Tracks;
        saved.Select(t => t.Visible).ShouldBe(new[] { false, true, true });
        File.ReadAllText(boss).ShouldContain("\"visible\": false");

        editor.Press(Key.Z, RawInputModifiers.Control);
        ShownTracks(editor).ShouldBe(new[] { 0, 1, 2 });
        File.ReadAllText(boss).ShouldNotContain("visible");

        editor.Press(Key.Y, RawInputModifiers.Control);
        ShownTracks(editor).ShouldBe(new[] { 1, 2 });
        File.ReadAllText(boss).ShouldContain("\"visible\": false");

        editor.ClickAt(OnEye(editor, 0));
        ShownTracks(editor).ShouldBe(new[] { 0, 1, 2 });
        editor.UndoLabels.ShouldBe(new[] { "Hide Track 'Moon'", "Show Track 'Moon'" });
    }

    [AvaloniaFact]
    public async Task HiddenTrack_KeepsItsLaneAndBlocksOnTheTimeline_AndCanStillBeEdited()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        var timeline = Timeline(editor);
        int blocks = timeline.Blocks.Count;
        double secondLaneTop = timeline.Viewport.LaneTop(1);

        editor.ClickAt(OnEye(editor, 0));

        timeline.Blocks.Count.ShouldBe(blocks);
        timeline.Viewport.LaneTop(1).ShouldBe(secondLaneTop);
        editor.ClickAt(editor.PointIn(timeline, timeline.Viewport.XAt(0.05), timeline.Viewport.LaneTop(0) + TimelineLayout.FilmTop + 5));
        timeline.Selection.ShouldBe(new TimelineChainId(0, 0));
    }

    [AvaloniaFact]
    public async Task HoveringTheEye_ShowsItsStateAndTooltip_AndLeavingClearsThem()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        var timeline = Timeline(editor);

        editor.Hover(OnEye(editor, 1));
        (timeline.HoverEyeTrack, timeline.EyeTooltip).ShouldBe((1, "Visible"));
        SequenceTimelineControl.EyeTooltipNote.ShouldBe("Saved in the file; runtimes may skip hidden tracks");

        editor.ClickAt(OnEye(editor, 1));
        (timeline.HoverEyeTrack, timeline.EyeTooltip).ShouldBe((1, "Hidden"));

        editor.Hover(OnName(editor, 1));
        (timeline.HoverEyeTrack, timeline.EyeTooltip).ShouldBe((null, null));
    }

    [AvaloniaFact]
    public async Task DoubleClickingTheEye_TogglesTwice_AndNeverStartsARename()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));

        editor.DoubleClickAt(OnEye(editor, 0));

        Timeline(editor).IsRenamingTrack.ShouldBeFalse();
        Timeline(editor).IsTrackHidden(0).ShouldBeFalse();
        Timeline(editor).SelectedTrack.ShouldBeNull();
        editor.UndoLabels.ShouldBe(new[] { "Hide Track 'Moon'", "Show Track 'Moon'" });
    }

    [AvaloniaFact]
    public async Task HeaderMenu_HidesAndShowsTheTrack_AndOffersShowAllTracksOnlyWhileSomethingIsHidden()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        var timeline = Timeline(editor);

        editor.RightClickAt(OnName(editor, 0));
        Menu(timeline).ShouldBe(new[] { "Rename Track…", "Duplicate Track", "Hide Track", "Delete Track" });
        editor.PickMenuItem(timeline.Menu, "Hide Track");

        ShownTracks(editor).ShouldBe(new[] { 1, 2 });
        editor.RightClickAt(OnName(editor, 0));
        Menu(timeline).ShouldBe(new[] { "Rename Track…", "Duplicate Track", "Show Track", "Show All Tracks", "Delete Track" });
        editor.PickMenuItem(timeline.Menu, "Show Track");
        editor.RightClickAt(OnName(editor, 1));
        editor.PickMenuItem(timeline.Menu, "Hide Track");
        editor.RightClickAt(OnName(editor, 0));
        editor.PickMenuItem(timeline.Menu, "Hide Track");
        ShownTracks(editor).ShouldBe(new[] { 2 });

        editor.RightClickAt(OnName(editor, 0));
        editor.PickMenuItem(timeline.Menu, "Show All Tracks");

        ShownTracks(editor).ShouldBe(new[] { 0, 1, 2 });
        editor.UndoLabels.Last().ShouldBe("Show All Tracks");
        editor.Press(Key.Z, RawInputModifiers.Control);
        ShownTracks(editor).ShouldBe(new[] { 2 });
    }

    [AvaloniaFact]
    public async Task AllTracksHidden_PreviewsEmpty_WithoutErrors_AndTheSequenceKeepsItsLength()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        string length = editor.Control<TextBlock>("SequenceTimeText").Text!;

        for (int lane = 0; lane < 3; lane++) editor.ClickAt(OnEye(editor, lane));

        ShownTracks(editor).ShouldBeEmpty();
        editor.ErrorBannerText.ShouldBeNull();
        editor.Control<TextBlock>("SequenceTimeText").Text.ShouldBe(length);
    }

    [AvaloniaFact]
    public async Task HiddenTrack_SurvivesClosingAndReopeningTheTab_BecauseTheFileSaysSo()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);
        editor.ClickAt(OnEye(editor, 0));

        editor.CloseTab("boss.seqj");
        await editor.OpenAsync(boss);

        Timeline(editor).IsTrackHidden(0).ShouldBeTrue();
        Timeline(editor).IsTrackHidden(1).ShouldBeFalse();
        ShownTracks(editor).ShouldBe(new[] { 1, 2 });
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task OpeningAFileWithAHiddenTrack_ShowsItHiddenFromTheStart()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, "BlackMoon");

        await editor.OpenAsync(boss);

        Timeline(editor).IsTrackHidden(1).ShouldBeTrue();
        ShownTracks(editor).ShouldBe(new[] { 0, 2 });
        File.ReadAllText(boss).ShouldContain("\"visible\": false");
    }

    [AvaloniaFact]
    public async Task RenamingAHiddenTrack_KeepsItHidden_AndUndoAndRedoKeepItHiddenToo()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);
        editor.ClickAt(OnEye(editor, 0));

        editor.DoubleClickAt(OnName(editor, 0));
        var box = Timeline(editor).GetVisualChildren().OfType<InlineRenameBox>().Single();
        editor.TypeAndEnter(box, "WhiteMoon");

        var track = SequenceListSave.FromFile(boss).Sequences[0].Tracks[0];
        (track.Name, track.Visible).ShouldBe(("WhiteMoon", false));
        ShownTracks(editor).ShouldBe(new[] { 1, 2 });

        editor.Press(Key.Z, RawInputModifiers.Control);
        Timeline(editor).IsTrackHidden(0).ShouldBeTrue();
        editor.Press(Key.Y, RawInputModifiers.Control);
        Timeline(editor).IsTrackHidden(0).ShouldBeTrue();
        ShownTracks(editor).ShouldBe(new[] { 1, 2 });
    }

    [AvaloniaFact]
    public async Task InspectorVisibleCheckbox_FollowsTheEye_AndUnchecksThroughTheSameEditPath()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);
        var inspector = editor.Control<SequenceInspectorControl>("SequenceInspector");
        editor.ClickAt(OnName(editor, 1));
        inspector.TrackVisibleBox.IsChecked.ShouldBe(true);

        editor.ClickAt(OnEye(editor, 1));
        inspector.TrackVisibleBox.IsChecked.ShouldBe(false);
        editor.ClickAt(OnEye(editor, 1));
        editor.Click(inspector.TrackVisibleBox);

        inspector.TrackVisibleBox.IsChecked.ShouldBe(false);
        Timeline(editor).IsTrackHidden(1).ShouldBeTrue();
        ShownTracks(editor).ShouldBe(new[] { 0, 2 });
        editor.UndoLabels.Last().ShouldBe("Hide Track 'BlackMoon'");
        File.ReadAllText(boss).ShouldContain("\"visible\": false");
    }

    [AvaloniaFact]
    public async Task HeaderEyeAndName_ShareOneLine_AtTheSameHeightOnEveryLane_TallOrNot()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        var timeline = Timeline(editor);
        // Sun (lane 2) has an event row; Moon (lane 0) does not.
        timeline.Viewport.LaneHeightOf(2).ShouldBeGreaterThan(timeline.Viewport.LaneHeightOf(0));

        foreach (int lane in new[] { 0, 1, 2 })
        {
            var (eye, name) = timeline.HeaderCenters(lane);
            Math.Abs(eye - name).ShouldBeLessThanOrEqualTo(1);
        }
        double OffsetIn(int lane) => timeline.HeaderCenters(lane).Name - timeline.Viewport.LaneTop(lane);
        OffsetIn(2).ShouldBe(OffsetIn(0), 1);
        OffsetIn(1).ShouldBe(OffsetIn(0), 1);
    }
}
