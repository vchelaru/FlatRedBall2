using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using AnimationEditor.Views.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;
using static AnimationEditor.App.Tests.Dogfood.SequenceTimelineMultiSelectScenarioTests;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// A plain click on empty lane space of the timeline moves the playhead to the exact time clicked and clears the selection;
/// a rubber band, a Ctrl or Shift click, or a press on a chain, a header, the ruler or the new-track row does not.
/// Fixture: Body: Walk 0..0.5, Walk 1..1.5, Run 2..2.2. Head: Run 0.5..0.7, Walk 1.5..2. Tail: empty. The sequence is 2.2 s.
/// </summary>
public class SequenceTimelineClickPlayheadScenarioTests
{
    private static Point EmptyLane(AnimationEditorHarness editor, double time) => At(editor, 2, time);

    private static Point RulerAt(AnimationEditorHarness editor, double time) =>
        editor.PointIn(Timeline(editor), Timeline(editor).Viewport.XAt(time), TimelineViewport.RulerHeight / 2);

    private static PreviewControl Preview(AnimationEditorHarness editor) => editor.Control<PreviewControl>("SequencePreview");

    private static bool IsPlaying(AnimationEditorHarness editor) =>
        editor.Control<Avalonia.Svg.Skia.Svg>("SequencePlayIcon").Path!.Contains("IconPause");

    private static string Shown(AnimationEditorHarness editor) =>
        string.Join(",", Preview(editor).Composite!.Select(sprite => $"{sprite.Track}:{sprite.Chain.Name}#{sprite.FrameIndex}"));

    // What the saved file's scene gives at a time: the preview must show exactly this.
    private static string SceneAt(string path, double time) =>
        string.Join(",", SequenceScene.At(SequenceDocument.Load(path), 0, time)!.Select(sprite => $"{sprite.Track}:{sprite.Chain.Name}#{sprite.FrameIndex}"));

    private static async Task<(AnimationEditorHarness Editor, string Path)> OpenAsync()
    {
        var editor = new AnimationEditorHarness();
        string path = WriteFixture(editor);
        await editor.OpenAsync(path);
        return (editor, path);
    }

    [AvaloniaFact]
    public async Task ClickingEmptyLane_AtSeveralTimes_PutsThePlayheadExactlyThere_AndThePreviewShowsThatTime()
    {
        var (editor, boss) = await OpenAsync();
        using var _ = editor;
        var timeline = Timeline(editor);
        string before = File.ReadAllText(boss);

        foreach (double time in new[] { 0.37, 1.0123, 1.8, 0.0 })
        {
            editor.ClickAt(EmptyLane(editor, time));
            timeline.Playhead.ShouldBe(time, 1e-6);
            Shown(editor).ShouldBe(SceneAt(boss, time));
        }

        editor.UndoLabels.ShouldBeEmpty();
        File.ReadAllText(boss).ShouldBe(before);
    }

    [AvaloniaFact]
    public async Task ClickingEmptyLane_WhileScrolledAndZoomed_UsesTheSameMappingAsTheRuler()
    {
        var (editor, boss) = await OpenAsync();
        using var _ = editor;
        var timeline = Timeline(editor);
        timeline.SetZoomPercent(300);
        timeline.Viewport.SetScrollSeconds(1.0);
        editor.Layout();

        foreach (double time in new[] { 1.3, 1.9, 2.05 })
        {
            editor.ClickAt(EmptyLane(editor, time));
            double byLane = timeline.Playhead;
            byLane.ShouldBe(time, 1e-6);
            editor.ClickAt(RulerAt(editor, time));
            timeline.Playhead.ShouldBe(byLane, 1e-9);
            Shown(editor).ShouldBe(SceneAt(boss, time));
        }
    }

    [AvaloniaFact]
    public async Task ClickingEmptyLane_PastTheSequenceEnd_ClampsToTheEnd()
    {
        var (editor, _) = await OpenAsync();
        using var __ = editor;

        editor.ClickAt(EmptyLane(editor, 5.0));

        Timeline(editor).Playhead.ShouldBe(2.2, 1e-6);
    }

    [AvaloniaFact]
    public async Task ClickingEmptyLane_ClearsTheSelection_AndASmallWobbleStillCountsAsAClick()
    {
        var (editor, _) = await OpenAsync();
        using var __ = editor;
        var timeline = Timeline(editor);
        editor.ClickAt(At(editor, 0, 0.25));
        timeline.SelectedChains.Count.ShouldBe(1);

        editor.Drag(EmptyLane(editor, 1.0), EmptyLane(editor, 1.0) + new Vector(2, 1));

        timeline.SelectedChains.ShouldBeEmpty();
        timeline.Selection.ShouldBeNull();
        timeline.Playhead.ShouldBe(1.0, 1e-6, "the time under the press");
    }

    [AvaloniaFact]
    public async Task ARubberBandFromEmptyLane_SelectsAndLeavesThePlayheadWhereItWas()
    {
        var (editor, _) = await OpenAsync();
        using var __ = editor;
        var timeline = Timeline(editor);
        editor.ClickAt(RulerAt(editor, 0.4));

        editor.DragHold(At(editor, 1, 0.2), At(editor, 0, 1.2));
        editor.DragRelease(At(editor, 0, 1.2));

        timeline.SelectedChains.Count.ShouldBe(3);
        timeline.Playhead.ShouldBe(0.4, 1e-6);
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task ARubberBandReleasedBackWhereItStarted_DoesNotMoveThePlayhead()
    {
        var (editor, _) = await OpenAsync();
        using var __ = editor;
        var timeline = Timeline(editor);
        editor.ClickAt(RulerAt(editor, 0.4));
        var start = EmptyLane(editor, 1.0);

        editor.DragHold(start, start + new Vector(60, -30));
        editor.DragRelease(start);

        timeline.Playhead.ShouldBe(0.4, 1e-6);
    }

    [AvaloniaFact]
    public async Task CtrlClickAndShiftClick_OnEmptyLane_DoNotMoveThePlayhead()
    {
        var (editor, _) = await OpenAsync();
        using var __ = editor;
        var timeline = Timeline(editor);
        editor.ClickAt(RulerAt(editor, 0.4));
        editor.ClickAt(At(editor, 0, 0.25));

        editor.ClickAt(EmptyLane(editor, 1.5), RawInputModifiers.Control);
        timeline.Playhead.ShouldBe(0.4, 1e-6);
        timeline.SelectedChains.ShouldBe(new[] { new TimelineChainId(0, 0) }, "Ctrl on empty space keeps the selection");

        editor.ClickAt(EmptyLane(editor, 1.5), RawInputModifiers.Shift);
        timeline.Playhead.ShouldBe(0.4, 1e-6);
    }

    [AvaloniaFact]
    public async Task PressingAChain_AHeader_TheNewTrackRow_OrARightClick_LeavesThePlayheadAlone()
    {
        var (editor, boss) = await OpenAsync();
        using var __ = editor;
        var timeline = Timeline(editor);
        editor.ClickAt(RulerAt(editor, 0.4));

        editor.ClickAt(At(editor, 0, 0.25));
        timeline.Selection.ShouldBe(new TimelineChainId(0, 0));
        editor.ClickAt(editor.PointIn(timeline, 70, timeline.HeaderCenters(1).Name));
        timeline.SelectedTrack.ShouldBe(1);
        editor.ClickAt(editor.PointIn(timeline, timeline.Viewport.XAt(1.0), timeline.Viewport.LaneTop(TimelineViewport.NewTrackLane) + 20));
        editor.RightClickAt(EmptyLane(editor, 1.5));
        editor.Press(Key.Escape);

        timeline.Playhead.ShouldBe(0.4, 1e-6);
        editor.UndoLabels.ShouldBeEmpty();
        SequenceListSave.FromFile(boss).Sequences[0].Tracks.Count.ShouldBe(3);
    }

    [AvaloniaFact]
    public async Task TheNewTrackHeaderCell_StillAddsATrack_WithoutMovingThePlayhead()
    {
        var (editor, boss) = await OpenAsync();
        using var __ = editor;
        var timeline = Timeline(editor);
        editor.ClickAt(RulerAt(editor, 0.4));

        editor.ClickAt(editor.PointIn(timeline, 30, timeline.Viewport.LaneTop(TimelineViewport.NewTrackLane) + TimelineViewport.LaneHeight / 2));

        SequenceListSave.FromFile(boss).Sequences[0].Tracks.Count.ShouldBe(4);
        timeline.Playhead.ShouldBe(0.4, 1e-6);
    }

    [AvaloniaFact]
    public async Task TheRuler_StillScrubs()
    {
        var (editor, _) = await OpenAsync();
        using var __ = editor;

        editor.ClickAt(RulerAt(editor, 1.25));

        Timeline(editor).Playhead.ShouldBe(1.25, 1e-6);
    }

    [AvaloniaFact]
    public async Task ClickingEmptyLane_WhilePlaying_JumpsAndKeepsPlaying()
    {
        var (editor, _) = await OpenAsync();
        using var __ = editor;
        var timeline = Timeline(editor);
        editor.Click(editor.Control<Button>("SequencePlayButton"));
        (await editor.WaitUntilAsync(() => timeline.Playhead > 0.02, TimeSpan.FromSeconds(3))).ShouldBeTrue();
        IsPlaying(editor).ShouldBeTrue();

        editor.ClickAt(EmptyLane(editor, 1.6));

        IsPlaying(editor).ShouldBeTrue();
        timeline.Playhead.ShouldBeGreaterThanOrEqualTo(1.6 - 1e-6);
        timeline.Playhead.ShouldBeLessThan(2.2);
    }

    [AvaloniaFact]
    public async Task CtrlV_AfterClickingEmptyLane_PutsTheEarliestPastedChainAtTheClickedTime()
    {
        var (editor, boss) = await OpenAsync();
        using var __ = editor;
        editor.ClickAt(At(editor, 0, 1.25));
        editor.Press(Key.C, RawInputModifiers.Control);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));
        editor.ClickAt(EmptyLane(editor, 2.1));
        Timeline(editor).Playhead.ShouldBe(2.1, 1e-6);

        editor.Press(Key.V, RawInputModifiers.Control);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));
        editor.Layout();

        var body = SequenceListSave.FromFile(boss).Sequences[0].Tracks[0].Chains;
        body.Count.ShouldBe(4);
        body.Last().Chain.ShouldBe("Walk");
        body.Last().Start.ShouldBe(2.1, 1e-6);
        editor.UndoLabels.ShouldBe(new[] { "Paste 'Walk' on 'Body'" });
    }
}
