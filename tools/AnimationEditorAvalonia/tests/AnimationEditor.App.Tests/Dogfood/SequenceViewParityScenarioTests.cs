using System.Text.Json;
using AnimationEditor.App.Controls;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Models;
using AnimationEditor.Core.Paths;
using AnimationEditor.Core.Sequences;
using AnimationEditor.Views.Controls;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// A <c>.seqj</c> tab's preview and timeline match the <c>.achx</c> tab's: a Speed field on the transport, the playhead moved
/// from the keyboard, the View zoom items and Ctrl+Plus/Minus following the focused pane, a Fit for the timeline, and the
/// preview's view and pane sizes remembered.
/// </summary>
public class SequenceViewParityScenarioTests
{
    // hero.achx on sheet.png: Walk, five 0.1 s frames (0.5 s); Run, two (0.2 s). Walk on lane 0, Run from 0.25 on lane 1.
    private static string WriteFixture(string folder, Func<string, string>? writePng = null)
    {
        string path = Path.Combine(folder, "boss.seqj");
        SequenceListSave save = new SequenceListSave { Sources = { "hero.achx" } };
        SequenceSave sequence = new SequenceSave { Name = "Attack" };
        sequence.Tracks.Add(TrackOf("Body", ("Walk", 0)));
        sequence.Tracks.Add(TrackOf("Arm", ("Run", 0.25)));
        save.Sequences.Add(sequence);
        save.Save(path);
        return path;
    }

    private static SequenceTrackSave TrackOf(string name, params (string Chain, double Start)[] chains)
    {
        SequenceTrackSave track = new SequenceTrackSave { Name = name };
        foreach ((string chain, double start) in chains)
            track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = chain, Start = start, Loops = 1 });
        return track;
    }

    private static void WriteHero(AnimationEditorHarness editor)
    {
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16), (48, 0, 16, 16), (0, 16, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (0, 32, 16, 16), (16, 32, 16, 16)));
    }

    private static async Task<string> OpenFixtureAsync(AnimationEditorHarness editor)
    {
        WriteHero(editor);
        string path = WriteFixture(editor.ProjectFolder);
        await editor.OpenAsync(path);
        editor.Layout();
        return path;
    }

    private static SequenceTimelineControl Timeline(AnimationEditorHarness editor) => editor.Control<SequenceTimelineControl>("SequenceTimeline");

    private static PreviewControl Preview(AnimationEditorHarness editor) => editor.Control<PreviewControl>("SequencePreview");

    private static ZoomControl PreviewZoom(AnimationEditorHarness editor) => editor.Control<ZoomControl>("SequencePreviewZoom");

    private static ZoomControl TimelineZoom(AnimationEditorHarness editor) => editor.Control<ZoomControl>("SequenceTimelineZoom");

    private static void FocusTimeline(AnimationEditorHarness editor)
    {
        Timeline(editor).Focus();
        editor.Layout();
    }

    private static bool IsPlaying(AnimationEditorHarness editor) =>
        editor.Control<Avalonia.Svg.Skia.Svg>("SequencePlayIcon").Path!.Contains("IconPause");

    private sealed class FakeTime
    {
        public long Now;
        public TickClock Clock => new(() => Now, 1000);
    }

    // ── Playback speed ────────────────────────────────────────────────────────

    [AvaloniaFact]
    public async Task SpeedField_IsTheAchxSpeedField_SameRangeStepsFormatAndTooltip()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await OpenFixtureAsync(editor);

        FlankerNumericField sequenceSpeed = editor.Control<FlankerNumericField>("SequenceSpeedInput");
        FlankerNumericField achxSpeed = editor.Control<FlankerNumericField>("SpeedInput");

        sequenceSpeed.Value.ShouldBe(1.0m);
        (sequenceSpeed.Minimum, sequenceSpeed.Maximum, sequenceSpeed.Increment, sequenceSpeed.FormatString, sequenceSpeed.Width)
            .ShouldBe((achxSpeed.Minimum, achxSpeed.Maximum, achxSpeed.Increment, achxSpeed.FormatString, achxSpeed.Width));
        ToolTip.GetTip(sequenceSpeed).ShouldBe(ToolTip.GetTip(achxSpeed));
        sequenceSpeed.IsEffectivelyVisible.ShouldBeTrue();
    }

    [AvaloniaFact]
    public async Task Speed_ChangesTheClockRate_OfTheSelectedSequence()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await OpenFixtureAsync(editor);
        FakeTime time = new FakeTime();
        editor.Window.SetSequencePlaybackClock(time.Clock);

        editor.Click(editor.Control<Button>("SequencePlayButton"));
        time.Now += 100;
        editor.Window.TickSequencePlayback();
        Timeline(editor).Playhead.ShouldBe(0.1, 1e-6);

        editor.TypeFlanker("SequenceSpeedInput", "2");
        time.Now += 100;
        editor.Window.TickSequencePlayback();
        Timeline(editor).Playhead.ShouldBe(0.3, 1e-6);

        editor.TypeFlanker("SequenceSpeedInput", "0.5");
        time.Now += 100;
        editor.Window.TickSequencePlayback();
        Timeline(editor).Playhead.ShouldBe(0.35, 1e-6);
    }

    // ── Keyboard playhead stepping ────────────────────────────────────────────

    [AvaloniaFact]
    public async Task RightAndLeft_StepThePlayheadAcrossEveryTracksFrameBoundaries_AndThePreviewFollows()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await OpenFixtureAsync(editor);
        FocusTimeline(editor);

        // Walk's frames start every 0.1 s; Run joins at 0.25 and has its own cut at 0.35.
        double[] expected = { 0.1, 0.2, 0.25, 0.3, 0.35, 0.4, 0.45, 0.5 };
        foreach (double time in expected)
        {
            editor.Press(Key.Right);
            Timeline(editor).Playhead.ShouldBe(time, 1e-6);
        }
        editor.Press(Key.Right);
        Timeline(editor).Playhead.ShouldBe(0.5, 1e-6);

        editor.Press(Key.Left);
        Timeline(editor).Playhead.ShouldBe(0.45, 1e-6);
        // At 0.3 Walk shows its fourth frame and Run its first.
        for (int i = 0; i < 3; i++) editor.Press(Key.Left);
        Timeline(editor).Playhead.ShouldBe(0.3, 1e-6);
        Preview(editor).Composite!.Select(sprite => $"{sprite.Chain.Name}:{sprite.FrameIndex}").OrderBy(text => text)
            .ShouldBe(new[] { "Run:0", "Walk:3" });
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task HomeAndEnd_JumpToTheStartAndToTheSequenceEnd()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await OpenFixtureAsync(editor);
        FocusTimeline(editor);

        editor.Press(Key.End);
        Timeline(editor).Playhead.ShouldBe(Timeline(editor).SequenceLength, 1e-6);
        Timeline(editor).SequenceLength.ShouldBe(0.5, 1e-6);
        editor.Press(Key.Home);
        Timeline(editor).Playhead.ShouldBe(0);
    }

    [AvaloniaFact]
    public async Task SteppingWhilePlaying_KeepsPlaying_AndKeysDoNothingOutsideThePreviewAndTimeline()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await OpenFixtureAsync(editor);
        FakeTime time = new FakeTime();
        editor.Window.SetSequencePlaybackClock(time.Clock);
        FocusTimeline(editor);

        editor.Click(editor.Control<Button>("SequencePlayButton"));
        FocusTimeline(editor);
        IsPlaying(editor).ShouldBeTrue();
        editor.Press(Key.Right);
        Timeline(editor).Playhead.ShouldBe(0.1, 1e-6);
        IsPlaying(editor).ShouldBeTrue();
        time.Now += 50;
        editor.Window.TickSequencePlayback();
        Timeline(editor).Playhead.ShouldBe(0.15, 1e-6);

        // The sequence list owns the arrow keys when it has the focus.
        editor.Control<ListBox>("SequenceList").ContainerFromIndex(0)!.Focus();
        editor.Layout();
        editor.Press(Key.End);
        Timeline(editor).Playhead.ShouldBe(0.15, 1e-6);
    }

    [AvaloniaFact]
    public async Task Right_WithATrackSelected_StillStepsThePlayhead_AndEditsNothing()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await OpenFixtureAsync(editor);
        FocusTimeline(editor);
        Timeline(editor).SelectTracks(new[] { 0 });

        editor.Press(Key.Right);

        Timeline(editor).Playhead.ShouldBe(0.1, 1e-6);
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task TheShortcutsPanel_ListsTheStepAndJumpKeys()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await OpenFixtureAsync(editor);

        string[] ids = editor.Window.Hotkeys.Select(hotkey => hotkey.Id).ToArray();

        ids.ShouldContain("playhead-step-back");
        ids.ShouldContain("playhead-step-forward");
        ids.ShouldContain("playhead-start");
        ids.ShouldContain("playhead-end");
    }

    // ── Zoom ──────────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public async Task CtrlPlusAndMinus_ZoomWhicheverSequencePaneHasTheFocus()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await OpenFixtureAsync(editor);

        FocusTimeline(editor);
        float previewBefore = Preview(editor).Zoom;
        float timelineBefore = Timeline(editor).Zoom;
        editor.Press(Key.OemPlus, RawInputModifiers.Control);
        Timeline(editor).Zoom.ShouldBeGreaterThan(timelineBefore);
        Preview(editor).Zoom.ShouldBe(previewBefore);

        Preview(editor).Focus();
        editor.Layout();
        float timelineAfter = Timeline(editor).Zoom;
        editor.Press(Key.OemPlus, RawInputModifiers.Control);
        Preview(editor).Zoom.ShouldBeGreaterThan(previewBefore);
        Timeline(editor).Zoom.ShouldBe(timelineAfter);
        editor.Press(Key.OemMinus, RawInputModifiers.Control);
        Preview(editor).Zoom.ShouldBe(previewBefore, 1e-4);
    }

    [AvaloniaFact]
    public async Task ViewMenuZoomItems_ZoomThePreview_AndTheTimeline_OnASequenceTab()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await OpenFixtureAsync(editor);
        foreach (string name in new[] { "MenuPreviewZoomIn", "MenuPreviewZoomOut", "MenuTimelineZoomIn", "MenuTimelineZoomOut", "MenuTimelineFit" })
            editor.Control<MenuItem>(name).IsEnabled.ShouldBeTrue(name);
        editor.Control<MenuItem>("MenuWireframeZoomIn").IsEnabled.ShouldBeFalse();

        float previewBefore = Preview(editor).Zoom;
        editor.ClickMenu("MenuPreviewZoomIn");
        Preview(editor).Zoom.ShouldBeGreaterThan(previewBefore);

        float timelineBefore = Timeline(editor).Zoom;
        editor.ClickMenu("MenuTimelineZoomIn");
        Timeline(editor).Zoom.ShouldBeGreaterThan(timelineBefore);
        editor.ClickMenu("MenuTimelineZoomOut");
        Timeline(editor).Zoom.ShouldBe(timelineBefore, 1e-4);
    }

    [AvaloniaFact]
    public async Task FitButton_ZoomsTheTimelineSoTheWholeSequenceShows_AndTheMenuItemDoesTheSame()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await OpenFixtureAsync(editor);
        SequenceTimelineControl timeline = Timeline(editor);
        // Zoom in and scroll away first, so Fit has something to undo.
        TimelineZoom(editor).StepUp();
        TimelineZoom(editor).StepUp();
        timeline.Viewport.SetScrollSeconds(0.3);

        editor.Click(editor.Control<Button>("SequenceTimelineFit"));

        double expected = TimelineViewport.PixelsPerSecondToFit(timeline.SequenceLength, timeline.Bounds.Width);
        timeline.Viewport.PixelsPerSecond.ShouldBe(expected, 0.5);
        timeline.Viewport.ScrollSeconds.ShouldBe(0);
        timeline.Viewport.XAt(timeline.SequenceLength).ShouldBeLessThan(timeline.Bounds.Width);
        TimelineZoom(editor).Text.ShouldBe($"{(int)Math.Round(timeline.Zoom * 100)}%");

        TimelineZoom(editor).StepUp();
        editor.ClickMenu("MenuTimelineFit");
        timeline.Viewport.PixelsPerSecond.ShouldBe(expected, 0.5);
    }

    // ── Remembered view ───────────────────────────────────────────────────────

    private sealed class Session : IDisposable
    {
        public Session()
        {
            Folder = Path.Combine(Path.GetTempPath(), "AnimationEditorDogfood", Guid.NewGuid().ToString("N"));
            SettingsRoot = Path.Combine(Folder, "settings");
            Directory.CreateDirectory(Folder);
            Boss = Path.Combine(Folder, "boss.seqj");
        }

        public string Folder { get; }
        public string SettingsRoot { get; }
        public string Boss { get; }

        public AnimationEditorHarness Start()
        {
            AnimationEditorHarness editor = new AnimationEditorHarness(SettingsRoot);
            if (!File.Exists(Boss))
            {
                // The harness writes into its own folder; the session keeps the files beside the settings.
                WriteHero(editor);
                File.Copy(Path.Combine(editor.ProjectFolder, "sheet.png"), Path.Combine(Folder, "sheet.png"));
                File.Copy(Path.Combine(editor.ProjectFolder, "hero.achx"), Path.Combine(Folder, "hero.achx"));
                WriteFixture(Folder);
            }
            return editor;
        }

        public AnimationEditorHarness Restart()
        {
            AnimationEditorHarness editor = new AnimationEditorHarness(SettingsRoot);
            editor.WaitUntil(() => editor.TabLabels.Count >= 1, TimeSpan.FromSeconds(5));
            editor.Layout();
            return editor;
        }

        public string SettingsPath => AppSettingsLocation.ForApplicationDataRoot(SettingsRoot).FullPath;

        public AppSettingsModel StoredSettings() => JsonSerializer.Deserialize<AppSettingsModel>(File.ReadAllText(SettingsPath))!;

        public string Key(string path) => new FilePath(path).Standardized;

        public void Dispose()
        {
            try { Directory.Delete(Folder, recursive: true); } catch { /* a watcher may still hold it */ }
        }
    }

    [AvaloniaFact]
    public async Task PreviewZoomAndPan_AreRememberedPerFile_AcrossARestart_WithNoUndoEntry()
    {
        using Session session = new Session();
        (float X, float Y) pan;
        using (AnimationEditorHarness editor = session.Start())
        {
            await editor.OpenAsync(session.Boss);
            editor.Layout();
            PreviewZoom(editor).StepUp();
            PreviewZoom(editor).StepUp();
            Preview(editor).Zoom.ShouldBe(2f, 1e-4f);
            Avalonia.Point from = editor.CenterOf(Preview(editor));
            editor.MiddleDrag(from, new Avalonia.Point(from.X + 40, from.Y + 25));
            pan = Preview(editor).PanOffset;
            (pan.X != 0 || pan.Y != 0).ShouldBeTrue("the drag should have panned the preview");
            editor.UndoLabels.ShouldBeEmpty();
        }

        SequencePreviewView stored = session.StoredSettings().SequencePreviewViews[session.Key(session.Boss)];
        stored.ZoomPercent.ShouldBe(200);
        stored.PanX.ShouldBe(pan.X, 1e-3f);

        using (AnimationEditorHarness editor = session.Restart())
        {
            Preview(editor).Zoom.ShouldBe(2f, 1e-4f);
            Preview(editor).PanOffset.X.ShouldBe(pan.X, 1e-3f);
            Preview(editor).PanOffset.Y.ShouldBe(pan.Y, 1e-3f);
            PreviewZoom(editor).Text.ShouldBe("200%");
            editor.UndoLabels.ShouldBeEmpty();
        }
    }

    [AvaloniaFact]
    public async Task PreviewToggles_AndTheBoundingBoxSetting_AreRemembered()
    {
        using Session session = new Session();
        using (AnimationEditorHarness editor = session.Start())
        {
            await editor.OpenAsync(session.Boss);
            editor.Control<ToggleButton>("SequenceShowOrigin").IsChecked = true;
            editor.Control<ToggleButton>("SequenceShowShapes").IsChecked = false;
            editor.Control<ToggleButton>("SequenceShowBoundingBox").IsChecked = false;
            // The one Bounding Box setting: the achx toggle follows it.
            editor.Control<ToggleButton>("ShowBoundingBoxCheck").IsChecked.ShouldBe(false);
        }

        using (AnimationEditorHarness editor = session.Restart())
        {
            editor.Control<ToggleButton>("SequenceShowOrigin").IsChecked.ShouldBe(true);
            editor.Control<ToggleButton>("SequenceShowShapes").IsChecked.ShouldBe(false);
            editor.Control<ToggleButton>("SequenceShowBoundingBox").IsChecked.ShouldBe(false);
            Preview(editor).ShowOrigin.ShouldBeTrue();
            Preview(editor).ShowShapes.ShouldBeFalse();
            Preview(editor).ShowBoundingBox.ShouldBeFalse();
        }
    }

    [AvaloniaFact]
    public async Task PaneSizes_AreRememberedAcrossARestart()
    {
        using Session session = new Session();
        using (AnimationEditorHarness editor = session.Start())
        {
            await editor.OpenAsync(session.Boss);
            Grid pane = editor.Control<Grid>("SequenceEditorPane");
            pane.RowDefinitions[2].Height = new GridLength(333, GridUnitType.Pixel);
            editor.Control<Grid>("SequenceTopGrid").ColumnDefinitions[0].Width = new GridLength(260, GridUnitType.Pixel);
        }

        session.StoredSettings().SequencePreviewPaneHeight.ShouldBe(333);
        session.StoredSettings().SequenceLibraryWidth.ShouldBe(260);

        using (AnimationEditorHarness editor = session.Restart())
        {
            editor.Control<Grid>("SequenceEditorPane").RowDefinitions[2].Height.Value.ShouldBe(333);
            editor.Control<Grid>("SequenceTopGrid").ColumnDefinitions[0].Width.Value.ShouldBe(260);
        }
    }
}
