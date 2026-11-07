using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;
using Avalonia.Headless.XUnit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Selecting more than one chain or track on the timeline: Ctrl+click toggles, Shift+click takes a range, a rubber band
/// over empty lane space takes what it touches, Ctrl+A takes all, Escape clears. Chains and tracks never mix.
/// </summary>
public class SequenceTimelineMultiSelectScenarioTests
{
    // Walk is 0.5 s a cycle, Run 0.2 s.
    // Body: Walk 0..0.5, Walk 1..1.5, Run 2..2.2. Head: Run 0.5..0.7, Walk 1.5..2. Tail: empty.
    internal static string WriteFixture(AnimationEditorHarness editor)
    {
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16), (48, 0, 16, 16), (0, 16, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (0, 32, 16, 16), (16, 32, 16, 16)));
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        var sequence = new SequenceSave { Name = "Attack" };
        sequence.Tracks.Add(Track("Body", ("Walk", 0, 1), ("Walk", 1, 1), ("Run", 2, 1)));
        sequence.Tracks.Add(Track("Head", ("Run", 0.5, 1), ("Walk", 1.5, 1)));
        sequence.Tracks.Add(Track("Tail"));
        save.Sequences.Add(sequence);
        save.Save(path);
        return path;
    }

    internal static SequenceTrackSave Track(string name, params (string Chain, double Start, float Loops)[] chains)
    {
        var track = new SequenceTrackSave { Name = name };
        foreach (var (chain, start, loops) in chains)
            track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = chain, Start = start, Loops = loops });
        return track;
    }

    internal static SequenceTimelineControl Timeline(AnimationEditorHarness editor) =>
        editor.Control<SequenceTimelineControl>("SequenceTimeline");

    /// <summary>A window point on <paramref name="lane"/> at <paramref name="time"/>, in the chain-body band.</summary>
    internal static Point At(AnimationEditorHarness editor, int lane, double time, double dx = 0)
    {
        var timeline = Timeline(editor);
        double y = timeline.Viewport.LaneTop(lane) + (TimelineLayout.BodyTop + TimelineLayout.BodyBottom) / 2;
        return editor.PointIn(timeline, timeline.Viewport.XAt(time) + dx, y);
    }

    private static Point Header(AnimationEditorHarness editor, int lane)
    {
        var timeline = Timeline(editor);
        return editor.PointIn(timeline, 70, timeline.HeaderCenters(lane).Name);
    }

    private static SequenceInspectorControl Inspector(AnimationEditorHarness editor) =>
        editor.Control<SequenceInspectorControl>("SequenceInspector");

    [AvaloniaFact]
    public async Task CtrlClick_AddsAChainToTheSelection_AndRemovesItWhenClickedAgain()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        var timeline = Timeline(editor);

        editor.ClickAt(At(editor, 0, 0.25));
        editor.ClickAt(At(editor, 1, 1.75), RawInputModifiers.Control);
        editor.ClickAt(At(editor, 0, 1.25), RawInputModifiers.Control);

        timeline.SelectedChains.ShouldBe(new[] { new TimelineChainId(0, 0), new TimelineChainId(0, 1), new TimelineChainId(1, 1) });
        editor.ClickAt(At(editor, 0, 0.25), RawInputModifiers.Control);
        timeline.SelectedChains.ShouldBe(new[] { new TimelineChainId(0, 1), new TimelineChainId(1, 1) });
        editor.UndoLabels.ShouldBeEmpty("selection is view state, not an undo entry");
    }

    [AvaloniaFact]
    public async Task PlainClick_OnAnotherChain_CollapsesAMultiSelectionToThatChain()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        var timeline = Timeline(editor);
        editor.ClickAt(At(editor, 0, 0.25));
        editor.ClickAt(At(editor, 0, 1.25), RawInputModifiers.Control);

        editor.ClickAt(At(editor, 1, 0.6));

        timeline.SelectedChains.ShouldBe(new[] { new TimelineChainId(1, 0) });
        timeline.Selection.ShouldBe(new TimelineChainId(1, 0));
    }

    [AvaloniaFact]
    public async Task ShiftClick_SelectsTheChainsBetween_OnTheSameTrack()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        var timeline = Timeline(editor);

        editor.ClickAt(At(editor, 0, 0.25));
        editor.ClickAt(At(editor, 0, 2.1), RawInputModifiers.Shift);

        timeline.SelectedChains.ShouldBe(new[] { new TimelineChainId(0, 0), new TimelineChainId(0, 1), new TimelineChainId(0, 2) });
    }

    [AvaloniaFact]
    public async Task CtrlClickAndShiftClick_OnTrackHeaders_SelectTracks_AndDropTheChains()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        var timeline = Timeline(editor);
        editor.ClickAt(At(editor, 0, 0.25));

        editor.ClickAt(Header(editor, 0), RawInputModifiers.Control);
        timeline.SelectedChains.ShouldBeEmpty();
        timeline.SelectedTracks.ShouldBe(new[] { 0 });
        editor.ClickAt(Header(editor, 2), RawInputModifiers.Control);
        timeline.SelectedTracks.ShouldBe(new[] { 0, 2 });
        editor.ClickAt(Header(editor, 0), RawInputModifiers.Shift);
        timeline.SelectedTracks.ShouldBe(new[] { 0, 1, 2 });
        editor.ClickAt(At(editor, 0, 0.25), RawInputModifiers.Control);
        timeline.SelectedTracks.ShouldBeEmpty();
        timeline.SelectedChains.ShouldBe(new[] { new TimelineChainId(0, 0) });
    }

    [AvaloniaFact]
    public async Task DraggingARubberBand_OverEmptyLaneSpace_SelectsTheChainsItTouches()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        var timeline = Timeline(editor);
        editor.ClickAt(At(editor, 0, 2.1));

        editor.DragHold(At(editor, 1, 0.2), At(editor, 0, 1.2));

        timeline.IsMarqueeActive.ShouldBeTrue();
        timeline.SelectedChains.ShouldBe(new[] { new TimelineChainId(0, 0), new TimelineChainId(0, 1), new TimelineChainId(1, 0) });
        editor.DragRelease(At(editor, 0, 1.2));
        timeline.IsMarqueeActive.ShouldBeFalse();
        timeline.SelectedChains.Count.ShouldBe(3);
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task CtrlRubberBand_AddsToTheSelectionAlreadyMade()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        var timeline = Timeline(editor);
        editor.ClickAt(At(editor, 0, 2.1));

        editor.Drag(At(editor, 1, 0.2), At(editor, 1, 0.6), RawInputModifiers.Control);

        timeline.SelectedChains.ShouldBe(new[] { new TimelineChainId(0, 2), new TimelineChainId(1, 0) });
    }

    [AvaloniaFact]
    public async Task ClickingEmptyLaneSpace_ClearsTheSelection()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        var timeline = Timeline(editor);
        editor.ClickAt(At(editor, 0, 0.25));
        editor.ClickAt(At(editor, 0, 1.25), RawInputModifiers.Control);

        editor.ClickAt(At(editor, 2, 3.0));

        timeline.SelectedChains.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task CtrlA_SelectsTheChainsOfTheSelectedTrack_ThenEveryChain_AndEscapeClears()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        var timeline = Timeline(editor);
        editor.ClickAt(At(editor, 1, 0.6));

        editor.Press(Key.A, RawInputModifiers.Control);
        timeline.SelectedChains.ShouldBe(new[] { new TimelineChainId(1, 0), new TimelineChainId(1, 1) });
        editor.Press(Key.A, RawInputModifiers.Control);
        timeline.SelectedChains.Count.ShouldBe(5);

        editor.Press(Key.Escape);
        timeline.SelectedChains.ShouldBeEmpty();
        timeline.Selection.ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task Inspector_ForAMultiSelection_SaysHowManyAreSelected_AndOneChainShowsItsFieldsAsBefore()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        var inspector = Inspector(editor);
        editor.ClickAt(At(editor, 0, 0.25));
        inspector.ChainPanel.IsVisible.ShouldBeTrue();

        editor.ClickAt(At(editor, 0, 1.25), RawInputModifiers.Control);
        editor.ClickAt(At(editor, 1, 0.6), RawInputModifiers.Control);

        inspector.MultiPanel.IsVisible.ShouldBeTrue();
        inspector.MultiCountText.Text.ShouldBe("3 chains selected");
        inspector.ChainPanel.IsVisible.ShouldBeFalse();
        inspector.TrackPanel.IsVisible.ShouldBeFalse();
        inspector.SequencePanel.IsVisible.ShouldBeFalse();

        editor.ClickAt(Header(editor, 0));
        editor.ClickAt(Header(editor, 1), RawInputModifiers.Control);
        inspector.MultiCountText.Text.ShouldBe("2 tracks selected");
        editor.ClickAt(Header(editor, 1), RawInputModifiers.Control);
        inspector.TrackPanel.IsVisible.ShouldBeTrue();
        inspector.MultiPanel.IsVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task ASelection_SurvivesARefreshAndAnUndo_WhereItsChainsStillExist()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        var timeline = Timeline(editor);
        editor.ClickAt(At(editor, 0, 0.25));
        editor.ClickAt(At(editor, 1, 0.6), RawInputModifiers.Control);
        var selected = new[] { new TimelineChainId(0, 0), new TimelineChainId(1, 0) };

        // An edit elsewhere rebuilds the timeline; the chains are still where they were.
        timeline.ToggleTrackVisibility(2);
        timeline.SelectedChains.ShouldBe(selected);
        editor.Press(Key.Z, RawInputModifiers.Control);
        timeline.SelectedChains.ShouldBe(selected);
        editor.Press(Key.Y, RawInputModifiers.Control);
        timeline.SelectedChains.ShouldBe(selected);
    }

    [AvaloniaFact]
    public async Task NavigationHistory_RecordsThePrimaryChainOfAMultiSelection()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        var timeline = Timeline(editor);
        editor.ClickAt(At(editor, 0, 0.25));
        editor.ClickAt(At(editor, 1, 0.6), RawInputModifiers.Control);
        timeline.Selection.ShouldBe(new TimelineChainId(1, 0), "the chain last clicked is the primary");

        await editor.OpenAsync(Path.Combine(editor.ProjectFolder, "hero.achx"));
        editor.Press(Key.Left, RawInputModifiers.Alt);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

        editor.Tabs.ActiveTab!.DisplayName.ShouldBe("boss.seqj");
        timeline.Selection.ShouldBe(new TimelineChainId(1, 0));
    }
}
