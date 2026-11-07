using AnimationEditor.App.Controls;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Scrolling a long sequence must cost what is on screen, not what is in the sequence (#1644). The pins count the work one
/// render issues instead of timing it, so they hold on any machine.
/// </summary>
public class SequenceTimelineRenderBudgetScenarioTests
{
    private const int Tracks = 8;
    private const int ChainsPerTrack = 10;

    // Eight tracks of ten Walk chains each, one every 5 s: 50 s long, 80 blocks.
    private static string WriteLongSequence(AnimationEditorHarness editor)
    {
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16), (48, 0, 16, 16), (0, 16, 16, 16)));
        string path = Path.Combine(editor.ProjectFolder, "long.seqj");
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        var sequence = new SequenceSave { Name = "Cutscene" };
        for (int t = 0; t < Tracks; t++)
        {
            var track = new SequenceTrackSave { Name = $"Actor {t + 1}" };
            for (int c = 0; c < ChainsPerTrack; c++)
                track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk", Start = c * 5, Loops = 3 });
            sequence.Tracks.Add(track);
        }
        save.Sequences.Add(sequence);
        save.Save(path);
        return path;
    }

    private static SequenceTimelineControl Timeline(AnimationEditorHarness editor) =>
        editor.Control<SequenceTimelineControl>("SequenceTimeline");

    [AvaloniaFact]
    public async Task ScrollingToTheMiddle_IssuesOnlyTheBlocksOnScreen()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteLongSequence(editor));
        var timeline = Timeline(editor);

        timeline.ScrollToSeconds(22);
        editor.Layout();

        var frame = timeline.LastFrame;
        frame.Blocks.ShouldBe(Tracks * ChainsPerTrack);
        frame.BlocksIssued.ShouldBeLessThan(Tracks * 4);
        frame.BlocksIssued.ShouldBeLessThanOrEqualTo(frame.BlocksVisible + Tracks);   // an outline just off the edge still draws
    }

    [AvaloniaFact]
    public async Task ARepeatRender_LaysOutNoTextAgain()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteLongSequence(editor));
        var timeline = Timeline(editor);
        timeline.InvalidateVisual();
        editor.Layout();
        timeline.LastFrame.Texts.ShouldBeGreaterThan(0);

        timeline.InvalidateVisual();
        editor.Layout();

        timeline.LastFrame.TextsCreated.ShouldBe(0);
    }

    [AvaloniaFact]
    public async Task AThemeChange_LaysTheTextOutAgain()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteLongSequence(editor));
        var timeline = Timeline(editor);
        timeline.InvalidateVisual();
        editor.Layout();
        var application = Application.Current!;
        var before = application.RequestedThemeVariant;
        try
        {
            application.RequestedThemeVariant = timeline.ActualThemeVariant == ThemeVariant.Light ? ThemeVariant.Dark : ThemeVariant.Light;
            editor.Layout();

            timeline.LastFrame.TextsCreated.ShouldBeGreaterThan(0);
        }
        finally
        {
            application.RequestedThemeVariant = before;
        }
    }

    [AvaloniaFact]
    public async Task F3_TogglesTheTimelineDiagnostics()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteLongSequence(editor));
        var timeline = Timeline(editor);

        editor.Press(Key.F3);
        timeline.DiagnosticsEnabled.ShouldBeTrue();
        // Avalonia's layout and render time graphs repaint the whole window every frame, so F3 must not turn them on.
        editor.Window.RendererDiagnostics.DebugOverlays.ShouldBe(Avalonia.Rendering.RendererDebugOverlays.Fps);
        // The window-wide lines: whole-frame render time, UI thread stalls, garbage collection.
        timeline.DiagnosticsExtraLines.ShouldNotBeNull().Invoke().Count.ShouldBe(3);

        editor.Press(Key.F3);
        timeline.DiagnosticsEnabled.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task APlainFilmstrip_DrawsItsCellsWithoutAClipOrADrawPerCell()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteLongSequence(editor));
        var timeline = Timeline(editor);

        timeline.ScrollToSeconds(22);
        editor.Layout();

        var frame = timeline.LastFrame;
        frame.CellsIssued.ShouldBeGreaterThan(20);
        frame.Clips.ShouldBe(0);
        frame.FilmstripOps.ShouldBeLessThanOrEqualTo(frame.BlocksIssued);
    }

    [AvaloniaFact]
    public async Task ScrollingAcrossTheStrip_ReusesTheThumbnailAtlas()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteLongSequence(editor));
        var timeline = Timeline(editor);
        timeline.ScrollToSeconds(2);
        editor.Layout();
        int generation = timeline.FilmstripAtlasGeneration;

        timeline.ScrollToSeconds(30);
        editor.Layout();

        timeline.FilmstripAtlasGeneration.ShouldBe(generation);
        timeline.FilmstripAtlasPages.ShouldBe(1);
    }

    [AvaloniaFact]
    public async Task ATextureChange_DropsTheThumbnailAtlas()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteLongSequence(editor));
        var timeline = Timeline(editor);
        editor.Layout();
        int generation = timeline.FilmstripAtlasGeneration;

        editor.Services.ThumbnailService.InvalidatePath(Path.Combine(editor.ProjectFolder, "sheet.png"));

        timeline.FilmstripAtlasGeneration.ShouldBeGreaterThan(generation);
    }

    [AvaloniaFact]
    public async Task AMovingPlayhead_RepaintsItsOwnLayerNotTheLanes()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteLongSequence(editor));
        var timeline = Timeline(editor);
        var layer = editor.Control<TimelinePlayheadOverlay>("SequenceTimelinePlayhead");
        editor.Layout();
        int timelineDraws = timeline.RenderCount, layerDraws = layer.RenderCount;

        timeline.Playhead = 1.5;
        editor.Layout();
        timeline.Playhead = 1.6;
        editor.Layout();

        timeline.RenderCount.ShouldBe(timelineDraws);
        layer.RenderCount.ShouldBeGreaterThan(layerDraws);
    }

    [AvaloniaFact]
    public async Task ScrollingTheTimeline_RepaintsThePlayheadLayerToo()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteLongSequence(editor));
        var timeline = Timeline(editor);
        var layer = editor.Control<TimelinePlayheadOverlay>("SequenceTimelinePlayhead");
        editor.Layout();
        int layerDraws = layer.RenderCount;

        timeline.ScrollToSeconds(10);
        editor.Layout();

        layer.RenderCount.ShouldBeGreaterThan(layerDraws);
    }

    [AvaloniaFact]
    public async Task ScrollingToTheMiddle_IssuesOnlyTheFilmstripCellsOnScreen()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteLongSequence(editor));
        var timeline = Timeline(editor);

        timeline.ScrollToSeconds(22);
        editor.Layout();

        var frame = timeline.LastFrame;
        frame.CellsIssued.ShouldBeGreaterThan(0);
        frame.CellsIssued.ShouldBeLessThanOrEqualTo(frame.CellsVisible + frame.BlocksIssued);
    }
}
