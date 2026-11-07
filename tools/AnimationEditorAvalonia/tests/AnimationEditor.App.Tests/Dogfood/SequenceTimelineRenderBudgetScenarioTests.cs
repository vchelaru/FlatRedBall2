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

        editor.Press(Key.F3);
        timeline.DiagnosticsEnabled.ShouldBeFalse();
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
