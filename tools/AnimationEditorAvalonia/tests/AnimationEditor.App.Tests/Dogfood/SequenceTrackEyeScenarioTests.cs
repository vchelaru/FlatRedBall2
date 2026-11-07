using System.Text.Json;
using AnimationEditor.App.Controls;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Models;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// The eye on a track header hides the track in the sequence preview. It is view state: remembered per file in the
/// editor settings, never written to the <c>.seqj</c>, never an undo entry, and a hidden track keeps its lane and
/// blocks on the timeline.
/// </summary>
public class SequenceTrackEyeScenarioTests
{
    // Action has Moon (z 0) and BlackMoon (z 1), both playing Walk from 0.
    private static void WriteSeqj(string folder, string path)
    {
        var chains = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        var walk = new AnimationChainSave { Name = "Walk" };
        walk.Frames.Add(new AnimationFrameSave { TextureName = "hero.png", FrameLength = 0.1f, RightCoordinate = 16, BottomCoordinate = 16 });
        chains.AnimationChains.Add(walk);
        chains.Save(Path.Combine(folder, "hero.achx"));
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        var action = new SequenceSave { Name = "Action" };
        foreach (var (name, z) in new[] { ("Moon", 0), ("BlackMoon", 1) })
        {
            var track = new SequenceTrackSave { Name = name, Z = z };
            track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk", Start = 0, Loops = 1 });
            action.Tracks.Add(track);
        }
        save.Sequences.Add(action);
        save.Save(path);
    }

    private static string WriteFixture(AnimationEditorHarness editor)
    {
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        WriteSeqj(editor.ProjectFolder, path);
        return path;
    }

    private static SequenceTimelineControl Timeline(AnimationEditorHarness editor) =>
        editor.Control<SequenceTimelineControl>("SequenceTimeline");

    private static Point OnEye(AnimationEditorHarness editor, int lane) =>
        editor.PointIn(Timeline(editor), TimelineLayout.EyeCenterX, Timeline(editor).Viewport.LaneTop(lane) + TimelineLayout.EyeCenterY);

    private static Point OnName(AnimationEditorHarness editor, int lane) =>
        editor.PointIn(Timeline(editor), 60, Timeline(editor).Viewport.LaneTop(lane) + 10);

    private static List<int> ShownTracks(AnimationEditorHarness editor) =>
        editor.Control<PreviewControl>("SequencePreview").Composite!.Select(sprite => sprite.Track).ToList();

    private static AppSettingsModel StoredSettings(AnimationEditorHarness editor) =>
        JsonSerializer.Deserialize<AppSettingsModel>(
            File.ReadAllText(AppSettingsLocation.ForApplicationDataRoot(editor.Services.SettingsRoot).FullPath))!;

    [AvaloniaFact]
    public async Task ClickingTheEye_HidesThatTrackInThePreview_WithoutSelectingEditingOrSaving()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);
        byte[] before = File.ReadAllBytes(boss);
        ShownTracks(editor).ShouldBe(new[] { 0, 1 });

        editor.ClickAt(OnEye(editor, 0));

        Timeline(editor).IsTrackHidden(0).ShouldBeTrue();
        ShownTracks(editor).ShouldBe(new[] { 1 });
        Timeline(editor).SelectedTrack.ShouldBeNull();
        Timeline(editor).IsRenamingTrack.ShouldBeFalse();
        editor.UndoLabels.ShouldBeEmpty();
        File.ReadAllBytes(boss).ShouldBe(before);

        editor.ClickAt(OnEye(editor, 0));

        ShownTracks(editor).ShouldBe(new[] { 0, 1 });
        File.ReadAllBytes(boss).ShouldBe(before);
    }

    [AvaloniaFact]
    public async Task HiddenTrack_KeepsItsLaneAndBlocksOnTheTimeline()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        var timeline = Timeline(editor);
        int blocks = timeline.Blocks.Count;
        double secondLaneTop = timeline.Viewport.LaneTop(1);

        editor.ClickAt(OnEye(editor, 0));

        timeline.Blocks.Count.ShouldBe(blocks);
        timeline.Viewport.LaneTop(1).ShouldBe(secondLaneTop);
        // The hidden track's chain can still be selected for editing.
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
        (timeline.HoverEyeTrack, timeline.EyeTooltip).ShouldBe((1, "Hide in preview"));

        editor.ClickAt(OnEye(editor, 1));
        (timeline.HoverEyeTrack, timeline.EyeTooltip).ShouldBe((1, "Show in preview"));

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
    }

    [AvaloniaFact]
    public async Task HeaderMenu_HidesAndShowsTheTrack_AndOffersShowAllOnlyWhileSomethingIsHidden()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        var timeline = Timeline(editor);

        editor.RightClickAt(OnName(editor, 0));
        timeline.Menu.Items.OfType<MenuItem>().Select(item => (string?)item.Header)
            .ShouldBe(new[] { "Rename Track…", "Hide in preview", "Delete Track" });
        editor.PickMenuItem(timeline.Menu, "Hide in preview");

        ShownTracks(editor).ShouldBe(new[] { 1 });
        editor.RightClickAt(OnName(editor, 0));
        timeline.Menu.Items.OfType<MenuItem>().Select(item => (string?)item.Header)
            .ShouldBe(new[] { "Rename Track…", "Show in preview", "Show all tracks", "Delete Track" });
        editor.PickMenuItem(timeline.Menu, "Show in preview");
        editor.RightClickAt(OnName(editor, 1));
        editor.PickMenuItem(timeline.Menu, "Hide in preview");
        editor.RightClickAt(OnName(editor, 0));
        editor.PickMenuItem(timeline.Menu, "Hide in preview");
        ShownTracks(editor).ShouldBeEmpty();

        editor.RightClickAt(OnName(editor, 0));
        editor.PickMenuItem(timeline.Menu, "Show all tracks");

        ShownTracks(editor).ShouldBe(new[] { 0, 1 });
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task AllTracksHidden_PreviewsEmpty_WithoutErrors_AndTheSequenceKeepsItsLength()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        string length = editor.Control<TextBlock>("SequenceTimeText").Text!;

        editor.ClickAt(OnEye(editor, 0));
        editor.ClickAt(OnEye(editor, 1));

        ShownTracks(editor).ShouldBeEmpty();
        editor.ErrorBannerText.ShouldBeNull();
        editor.Control<TextBlock>("SequenceTimeText").Text.ShouldBe(length);
    }

    [AvaloniaFact]
    public async Task HiddenState_SurvivesClosingAndReopeningTheTab_AndTheOtherTrackStaysShown()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);
        editor.ClickAt(OnEye(editor, 0));

        editor.CloseTab("boss.seqj");
        await editor.OpenAsync(boss);

        Timeline(editor).IsTrackHidden(0).ShouldBeTrue();
        Timeline(editor).IsTrackHidden(1).ShouldBeFalse();
        ShownTracks(editor).ShouldBe(new[] { 1 });
        StoredSettings(editor).HiddenSequenceTracks[new AnimationEditor.Core.Paths.FilePath(boss).Standardized]
            .Select(entry => (entry.Sequence, entry.Track)).ShouldBe(new[] { ("Action", "Moon") });
    }

    [AvaloniaFact]
    public void HiddenState_SurvivesARestartOfTheEditor()
    {
        string folder = Path.Combine(Path.GetTempPath(), "AnimationEditorDogfood", Guid.NewGuid().ToString("N"));
        string settingsRoot = Path.Combine(folder, "settings");
        Directory.CreateDirectory(folder);
        string boss = Path.Combine(folder, "boss.seqj");
        WriteSeqj(folder, boss);
        try
        {
            using (AnimationEditorHarness first = new AnimationEditorHarness(settingsRoot))
            {
                first.OpenAsync(boss).GetAwaiter().GetResult();
                first.ClickAt(OnEye(first, 1));
                ShownTracks(first).ShouldBe(new[] { 0 });
            }

            using AnimationEditorHarness second = new AnimationEditorHarness(settingsRoot);
            second.WaitUntil(() => second.TabLabels.Count == 1, TimeSpan.FromSeconds(5));
            second.Layout();

            Timeline(second).IsTrackHidden(1).ShouldBeTrue();
            ShownTracks(second).ShouldBe(new[] { 0 });
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); } catch { /* a watcher may still hold it */ }
        }
    }

    [AvaloniaFact]
    public async Task RenamingAHiddenTrack_KeepsItHidden_AndUndoAndRedoKeepItHiddenToo()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);
        editor.ClickAt(OnEye(editor, 0));

        editor.DoubleClickAt(OnName(editor, 0));
        var box = Timeline(editor).GetVisualChildren().OfType<AnimationEditor.Views.Controls.InlineRenameBox>().Single();
        editor.TypeAndEnter(box, "WhiteMoon");

        SequenceListSave.FromFile(boss).Sequences[0].Tracks[0].Name.ShouldBe("WhiteMoon");
        Timeline(editor).IsTrackHidden(0).ShouldBeTrue();
        ShownTracks(editor).ShouldBe(new[] { 1 });
        StoredSettings(editor).HiddenSequenceTracks.Values.Single().Single().Track.ShouldBe("WhiteMoon");

        editor.Press(Key.Z, RawInputModifiers.Control);
        Timeline(editor).IsTrackHidden(0).ShouldBeTrue();
        ShownTracks(editor).ShouldBe(new[] { 1 });
        editor.Press(Key.Y, RawInputModifiers.Control);
        Timeline(editor).IsTrackHidden(0).ShouldBeTrue();
        ShownTracks(editor).ShouldBe(new[] { 1 });
    }
}
