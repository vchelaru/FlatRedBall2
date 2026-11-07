using System.Text.Json;
using AnimationEditor.App.Controls;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Models;
using AnimationEditor.Core.Paths;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// A <c>.seqj</c> tab remembers its timeline zoom across a restart, a reopen and tab switches (#1665). It is view
/// state: kept in the editor settings, never in the file or the undo history.
/// </summary>
public class SequenceZoomRestoreScenarioTests
{
    private static SequenceTimelineControl Timeline(AnimationEditorHarness editor) => editor.Control<SequenceTimelineControl>("SequenceTimeline");

    private static ZoomControl ZoomField(AnimationEditorHarness editor) => editor.Control<ZoomControl>("SequenceTimelineZoom");

    private static void WriteSeqj(string path)
    {
        SequenceListSave save = new SequenceListSave();
        SequenceSave sequence = new SequenceSave { Name = "Attack" };
        sequence.Tracks.Add(new SequenceTrackSave { Name = "Body" });
        save.Sequences.Add(sequence);
        save.Save(path);
    }

    /// <summary>The editor shows 'percent' on the Zoom field and the viewport, and the three agree.</summary>
    private static void ShouldShowZoom(AnimationEditorHarness editor, int percent)
    {
        SequenceTimelineControl timeline = Timeline(editor);
        ZoomField(editor).Text.ShouldBe($"{percent}%");
        ((double)timeline.Zoom * 100).ShouldBe(percent, 0.5);
        timeline.Viewport.PixelsPerSecond.ShouldBe(TimelineViewport.DefaultPixelsPerSecond * percent / 100.0, 0.5);
        timeline.IsZoomAnimating.ShouldBeFalse();
    }

    /// <summary>A folder holding the sequence files and a settings root, which outlive each editor window so a second one can "restart" on them.</summary>
    private sealed class Session : IDisposable
    {
        public Session()
        {
            Folder = Path.Combine(Path.GetTempPath(), "AnimationEditorDogfood", Guid.NewGuid().ToString("N"));
            SettingsRoot = Path.Combine(Folder, "settings");
            Directory.CreateDirectory(Folder);
            Boss = Path.Combine(Folder, "boss.seqj");
            Minion = Path.Combine(Folder, "minion.seqj");
        }

        public string Folder { get; }
        public string SettingsRoot { get; }
        public string Boss { get; }
        public string Minion { get; }

        public AnimationEditorHarness Start()
        {
            if (!File.Exists(Boss)) { WriteSeqj(Boss); WriteSeqj(Minion); }
            return new AnimationEditorHarness(SettingsRoot);
        }

        public AnimationEditorHarness Restart()
        {
            var editor = Start();
            editor.WaitUntil(() => editor.TabLabels.Count >= 1, TimeSpan.FromSeconds(5));
            editor.Layout();
            return editor;
        }

        public string SettingsPath => AppSettingsLocation.ForApplicationDataRoot(SettingsRoot).FullPath;

        public AppSettingsModel StoredSettings() => JsonSerializer.Deserialize<AppSettingsModel>(File.ReadAllText(SettingsPath))!;

        public string Key(string path) => new FilePath(path).Standardized;

        public void EditSettings(Action<AppSettingsModel> edit)
        {
            AppSettingsModel settings = StoredSettings();
            edit(settings);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings));
        }

        public void Dispose()
        {
            try { Directory.Delete(Folder, recursive: true); } catch { /* a watcher may still hold it */ }
        }
    }

    [AvaloniaFact]
    public async Task TheZoom_IsStillThereAfterARestart_WithNoUndoEntryAndTheFileUntouched()
    {
        using Session session = new Session();
        byte[] before;
        using (AnimationEditorHarness editor = session.Start())
        {
            await editor.OpenAsync(session.Boss);
            before = File.ReadAllBytes(session.Boss);
            ZoomField(editor).StepUp();
            ZoomField(editor).StepUp();
            ShouldShowZoom(editor, 200);
            editor.UndoLabels.ShouldBeEmpty();
        }

        using (AnimationEditorHarness editor = session.Restart())
        {
            ShouldShowZoom(editor, 200);
            editor.UndoLabels.ShouldBeEmpty();
        }

        File.ReadAllBytes(session.Boss).ShouldBe(before);
        session.StoredSettings().TimelineZooms[session.Key(session.Boss)].ShouldBe(200);
    }

    [AvaloniaFact]
    public async Task ReopeningTheFile_AfterClosingItsTab_RestoresTheZoom()
    {
        using Session session = new Session();
        using AnimationEditorHarness editor = session.Start();
        await editor.OpenAsync(session.Boss);
        Timeline(editor).SetZoomPercent(400);

        editor.CloseTab("boss.seqj");
        await editor.OpenAsync(session.Minion);
        ShouldShowZoom(editor, 100);
        editor.CloseTab("minion.seqj");
        await editor.OpenAsync(session.Boss);

        ShouldShowZoom(editor, 400);
    }

    [AvaloniaFact]
    public async Task EachTab_KeepsItsOwnZoom_WhenSwitchingBetweenTwoFiles()
    {
        using Session session = new Session();
        using AnimationEditorHarness editor = session.Start();
        await editor.OpenAsync(session.Boss);
        Timeline(editor).SetZoomPercent(200);
        await editor.OpenAsync(session.Minion);
        ShouldShowZoom(editor, 100);
        Timeline(editor).SetZoomPercent(50);

        editor.ClickTab("boss.seqj");
        ShouldShowZoom(editor, 200);
        editor.ClickTab("minion.seqj");
        ShouldShowZoom(editor, 50);
        editor.ClickTab("boss.seqj");
        ShouldShowZoom(editor, 200);
    }

    [AvaloniaFact]
    public async Task ZoomingBackTo100_DropsTheEntry()
    {
        using Session session = new Session();
        using AnimationEditorHarness editor = session.Start();
        await editor.OpenAsync(session.Boss);
        Timeline(editor).SetZoomPercent(300);
        session.StoredSettings().TimelineZooms.ShouldContainKey(session.Key(session.Boss));

        Timeline(editor).SetZoomPercent(100);

        session.StoredSettings().TimelineZooms.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task TypedValueAndPlusMinusButtons_AreRemembered()
    {
        using Session session = new Session();
        using AnimationEditorHarness editor = session.Start();
        await editor.OpenAsync(session.Boss);

        Timeline(editor).SetZoomPercent(137);
        session.StoredSettings().TimelineZooms[session.Key(session.Boss)].ShouldBe(137);
        ZoomField(editor).StepUp();
        session.StoredSettings().TimelineZooms[session.Key(session.Boss)].ShouldBe(150);
        ZoomField(editor).StepDown();
        ZoomField(editor).StepDown();
        session.StoredSettings().TimelineZooms.ShouldBeEmpty();
    }

    [AvaloniaTheory]
    [InlineData(99999, 1667)]
    [InlineData(1, 8)]
    [InlineData(-30, 8)]
    public async Task AStoredZoomOutsideTheLimits_IsClampedOnRestore_AndTheFieldAndViewportAgree(int stored, int shown)
    {
        using Session session = new Session();
        using (AnimationEditorHarness editor = session.Start())
            await editor.OpenAsync(session.Boss);
        session.EditSettings(settings => settings.TimelineZooms[session.Key(session.Boss)] = stored);

        using (AnimationEditorHarness editor = session.Restart())
        {
            ZoomField(editor).Text.ShouldBe($"{shown}%");
            double pps = Timeline(editor).Viewport.PixelsPerSecond;
            pps.ShouldBeInRange(TimelineViewport.MinPixelsPerSecond, TimelineViewport.MaxPixelsPerSecond);
            (pps * 100 / TimelineViewport.DefaultPixelsPerSecond).ShouldBe(shown, 0.5);
        }
    }

    [AvaloniaFact]
    public async Task WheelZoom_WritesTheSettingsOnlyWhenTheEaseHasSettled()
    {
        using Session session = new Session();
        using AnimationEditorHarness editor = session.Start();
        await editor.OpenAsync(session.Boss);
        SequenceTimelineControl timeline = Timeline(editor);
        Point point = editor.PointIn(timeline, TimelineViewport.HeaderWidth + 300, timeline.Viewport.LaneTop(0) + 10);

        for (int i = 0; i < 3; i++) editor.Wheel(point, 1, RawInputModifiers.Control);
        timeline.IsZoomAnimating.ShouldBeTrue();
        // Mid-ease ticks must not write: the zoom has not arrived anywhere yet.
        for (int i = 0; i < 4 && timeline.StepZoomAnimation(1f / 60f); i++)
            session.StoredSettings().TimelineZooms.ShouldBeEmpty($"tick {i + 1}");

        timeline.SettleZoomAnimation();

        session.StoredSettings().TimelineZooms[session.Key(session.Boss)].ShouldBe(300);
    }

    [AvaloniaFact]
    public async Task LeavingTheTabMidEase_StillRemembersWhereTheZoomWasHeading()
    {
        using Session session = new Session();
        using AnimationEditorHarness editor = session.Start();
        await editor.OpenAsync(session.Boss);
        SequenceTimelineControl timeline = Timeline(editor);
        await editor.OpenAsync(session.Minion);
        editor.ClickTab("boss.seqj");
        Point point = editor.PointIn(timeline, TimelineViewport.HeaderWidth + 300, timeline.Viewport.LaneTop(0) + 10);
        editor.Wheel(point, 1, RawInputModifiers.Control);
        timeline.IsZoomAnimating.ShouldBeTrue();

        editor.ClickTab("minion.seqj");

        session.StoredSettings().TimelineZooms[session.Key(session.Boss)].ShouldBe(150);
        ShouldShowZoom(editor, 100);
    }

    [AvaloniaFact]
    public async Task SwitchingToAShorterZoomedFile_DoesNotLeaveTheViewPastItsEnd()
    {
        using Session session = new Session();
        using AnimationEditorHarness editor = session.Start();
        await editor.OpenAsync(session.Boss);
        SequenceTimelineControl timeline = Timeline(editor);
        await editor.OpenAsync(session.Minion);
        Timeline(editor).SetZoomPercent(800);
        editor.ClickTab("boss.seqj");
        timeline.Viewport.SetScrollSeconds(500);

        editor.ClickTab("minion.seqj");

        ShouldShowZoom(editor, 800);
        // The furthest the horizontal bar lets the view start: the sequence plus its end margin, less one screenful.
        double view = (timeline.Bounds.Width - TimelineViewport.HeaderWidth) / timeline.Viewport.PixelsPerSecond;
        double furthest = Math.Max(0, timeline.SequenceLength + TimelineScrollBars.EndMarginPixels / timeline.Viewport.PixelsPerSecond - view);
        timeline.Viewport.ScrollSeconds.ShouldBeLessThanOrEqualTo(furthest + 1e-9);
    }
}
