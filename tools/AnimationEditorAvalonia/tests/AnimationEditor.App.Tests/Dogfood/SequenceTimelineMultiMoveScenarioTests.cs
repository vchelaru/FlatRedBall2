using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;
using static AnimationEditor.App.Tests.Dogfood.SequenceTimelineMultiSelectScenarioTests;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Moving and deleting a multi-selection on the timeline. Dragging any selected chain moves them all by one time shift
/// and one lane shift (clamped at the edges); Delete removes them all. Each gesture is one undo entry.
/// Fixture: Body: Walk 0..0.5, Walk 1..1.5, Run 2..2.2. Head: Run 0.5..0.7, Walk 1.5..2. Tail: empty.
/// </summary>
public class SequenceTimelineMultiMoveScenarioTests
{
    private static SequenceTrackSave SavedTrack(string path, int track) => SequenceListSave.FromFile(path).Sequences[0].Tracks[track];

    private static (string Chain, double Start)[] Chains(string path, int track) =>
        SavedTrack(path, track).Chains.Select(c => (c.Chain, c.Start)).ToArray();

    private static async Task<(AnimationEditorHarness Editor, string Path)> OpenAsync()
    {
        var editor = new AnimationEditorHarness();
        string path = WriteFixture(editor);
        await editor.OpenAsync(path);
        return (editor, path);
    }

    [AvaloniaFact]
    public async Task DraggingOneSelectedChain_MovesTheWholeSelectionByTheSameTime_AsOneUndoEntry()
    {
        var (editor, boss) = await OpenAsync();
        using var _ = editor;
        var timeline = Timeline(editor);
        editor.ClickAt(At(editor, 0, 0.25));
        editor.ClickAt(At(editor, 1, 1.75), RawInputModifiers.Control);

        // Alt skips snapping: 60 px at 120 px/s is exactly half a second.
        // (Pressing a few pixels off the last click, so it is not a double-click.)
        editor.Drag(At(editor, 1, 1.75, dx: 5), At(editor, 1, 1.75, dx: 65), RawInputModifiers.Alt);

        Chains(boss, 0).ShouldBe(new[] { ("Walk", 0.5), ("Walk", 1.0), ("Run", 2.0) });
        Chains(boss, 1).ShouldBe(new[] { ("Run", 0.5), ("Walk", 2.0) });
        editor.UndoLabels.ShouldBe(new[] { "Move 2 Chains" });
        timeline.SelectedChains.ShouldBe(new[] { new TimelineChainId(0, 0), new TimelineChainId(1, 1) });
        editor.Press(Key.Z, RawInputModifiers.Control);
        Chains(boss, 0)[0].ShouldBe(("Walk", 0.0));
        Chains(boss, 1)[1].ShouldBe(("Walk", 1.5));
        editor.Press(Key.Y, RawInputModifiers.Control);
        Chains(boss, 1)[1].ShouldBe(("Walk", 2.0));
    }

    [AvaloniaFact]
    public async Task TheEarliestChain_StopsAtZero_AndTheOthersKeepTheirDistanceFromIt()
    {
        var (editor, boss) = await OpenAsync();
        using var _ = editor;
        editor.ClickAt(At(editor, 1, 0.6));
        editor.ClickAt(At(editor, 1, 1.75), RawInputModifiers.Control);

        // 1.5 s to the left: Run (0.5) can only go 0.5 s, so Walk (1.5) goes 0.5 s too.
        editor.Drag(At(editor, 1, 1.75, dx: 5), At(editor, 1, 1.75, dx: -175), RawInputModifiers.Alt);

        Chains(boss, 1).ShouldBe(new[] { ("Run", 0.0), ("Walk", 1.0) });
    }

    [AvaloniaFact]
    public async Task DraggingToAnotherLane_MovesEverySelectedChainTheSameNumberOfLanes_ClampedAtTheEdge()
    {
        var (editor, boss) = await OpenAsync();
        using var _ = editor;
        var timeline = Timeline(editor);
        editor.ClickAt(At(editor, 0, 0.25));
        editor.ClickAt(At(editor, 1, 1.75), RawInputModifiers.Control);

        // Lanes 0 and 1 of 3; the pointer goes two lanes down, so both shift one lane: the limit for the lower one.
        editor.Drag(At(editor, 0, 0.25), At(editor, 2, 0.25), RawInputModifiers.Alt);

        Chains(boss, 0).ShouldBe(new[] { ("Walk", 1.0), ("Run", 2.0) });
        Chains(boss, 1).ShouldBe(new[] { ("Run", 0.5), ("Walk", 0.0) });
        Chains(boss, 2).ShouldBe(new[] { ("Walk", 1.5) });
        editor.UndoLabels.ShouldBe(new[] { "Move 2 Chains" });
        timeline.SelectedChains.ShouldBe(new[] { new TimelineChainId(1, 1), new TimelineChainId(2, 0) });
        editor.Press(Key.Z, RawInputModifiers.Control);
        Chains(boss, 2).ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task TheDraggedChain_IsThePrimaryForSnapping_AndTheOthersFollowItsDelta()
    {
        var (editor, boss) = await OpenAsync();
        using var _ = editor;
        var timeline = Timeline(editor);
        editor.ClickAt(At(editor, 1, 0.6));
        editor.ClickAt(At(editor, 0, 2.1), RawInputModifiers.Control);

        // Run (2..2.2) is dragged 0.475 s left, 3 px short of 1.5 where Body's second chain ends.
        editor.DragHold(At(editor, 0, 2.1, dx: 5), At(editor, 0, 2.1, dx: -52));
        timeline.SnapGuideTime!.Value.ShouldBe(1.5, 1e-9);
        editor.DragRelease(At(editor, 0, 2.1, dx: -52));

        Chains(boss, 0)[2].Start.ShouldBe(1.5, 1e-9);
        // The other chain (Run at 0.5) took the same -0.5.
        Chains(boss, 1)[0].Start.ShouldBe(0.0, 1e-9);
    }

    [AvaloniaFact]
    public async Task EscapeDuringAMultiDrag_PutsEverythingBack_WithNoUndoEntry_AndKeepsTheSelection()
    {
        var (editor, boss) = await OpenAsync();
        using var _ = editor;
        var timeline = Timeline(editor);
        editor.ClickAt(At(editor, 0, 0.25));
        editor.ClickAt(At(editor, 1, 1.75), RawInputModifiers.Control);

        editor.DragHold(At(editor, 1, 1.75, dx: 5), At(editor, 2, 1.75, dx: 65), RawInputModifiers.Alt);
        editor.Press(Key.Escape);
        editor.DragRelease(At(editor, 2, 1.75, dx: 65));

        Chains(boss, 0).ShouldBe(new[] { ("Walk", 0.0), ("Walk", 1.0), ("Run", 2.0) });
        Chains(boss, 1).ShouldBe(new[] { ("Run", 0.5), ("Walk", 1.5) });
        editor.UndoLabels.ShouldBeEmpty();
        timeline.SelectedChains.ShouldBe(new[] { new TimelineChainId(0, 0), new TimelineChainId(1, 1) });
    }

    [AvaloniaFact]
    public async Task PressingASelectedChainWithoutDragging_NarrowsTheSelectionToIt()
    {
        var (editor, _) = await OpenAsync();
        using var __ = editor;
        var timeline = Timeline(editor);
        editor.ClickAt(At(editor, 0, 0.25));
        editor.ClickAt(At(editor, 1, 1.75), RawInputModifiers.Control);

        editor.ClickAt(At(editor, 0, 0.25));

        timeline.SelectedChains.ShouldBe(new[] { new TimelineChainId(0, 0) });
    }

    [AvaloniaFact]
    public async Task DraggingTheRightEdge_OfOneChainInAMultiSelection_ResizesOnlyThatChain()
    {
        var (editor, boss) = await OpenAsync();
        using var _ = editor;
        var timeline = Timeline(editor);
        editor.ClickAt(At(editor, 0, 0.25));
        editor.ClickAt(At(editor, 0, 1.25), RawInputModifiers.Control);

        editor.Drag(At(editor, 0, 0.5, dx: -2), At(editor, 0, 0.5, dx: 28), RawInputModifiers.Alt);

        SavedTrack(boss, 0).Chains[0].Loops.ShouldBeGreaterThan(1f);
        SavedTrack(boss, 0).Chains[1].Loops.ShouldBe(1f);
        editor.UndoLabels.Count.ShouldBe(1);
        editor.UndoLabels[0].ShouldStartWith("Set Cycles");
        timeline.SelectedChains.ShouldBe(new[] { new TimelineChainId(0, 0) });
    }

    [AvaloniaFact]
    public async Task Delete_RemovesEverySelectedChain_AsOneUndoEntry_AndUndoBringsThemBack()
    {
        var (editor, boss) = await OpenAsync();
        using var _ = editor;
        var timeline = Timeline(editor);
        editor.ClickAt(At(editor, 0, 0.25));
        editor.ClickAt(At(editor, 0, 2.1), RawInputModifiers.Control);
        editor.ClickAt(At(editor, 1, 1.75), RawInputModifiers.Control);

        editor.Press(Key.Delete);

        Chains(boss, 0).ShouldBe(new[] { ("Walk", 1.0) });
        Chains(boss, 1).ShouldBe(new[] { ("Run", 0.5) });
        editor.UndoLabels.ShouldBe(new[] { "Remove 3 Chains" });
        timeline.SelectedChains.ShouldBeEmpty();
        editor.Press(Key.Z, RawInputModifiers.Control);
        Chains(boss, 0).Length.ShouldBe(3);
        Chains(boss, 1).Length.ShouldBe(2);
    }

    [AvaloniaFact]
    public async Task Delete_RemovesEverySelectedTrack_AsOneUndoEntry()
    {
        var (editor, boss) = await OpenAsync();
        using var _ = editor;
        var timeline = Timeline(editor);
        editor.ClickAt(editor.PointIn(timeline, 70, timeline.HeaderCenters(0).Name));
        editor.ClickAt(editor.PointIn(timeline, 70, timeline.HeaderCenters(2).Name), RawInputModifiers.Control);

        editor.Press(Key.Delete);

        SequenceListSave.FromFile(boss).Sequences[0].Tracks.Select(t => t.Name).ShouldBe(new[] { "Head" });
        editor.UndoLabels.ShouldBe(new[] { "Delete 2 Tracks" });
        timeline.SelectedTracks.ShouldBeEmpty();
        editor.Press(Key.Z, RawInputModifiers.Control);
        SequenceListSave.FromFile(boss).Sequences[0].Tracks.Count.ShouldBe(3);
    }

    [AvaloniaFact]
    public async Task RightClickingASelectedChain_KeepsTheSelection_AndRemoveTakesAllOfIt()
    {
        var (editor, boss) = await OpenAsync();
        using var _ = editor;
        var timeline = Timeline(editor);
        editor.ClickAt(At(editor, 0, 0.25));
        editor.ClickAt(At(editor, 1, 1.75), RawInputModifiers.Control);

        editor.RightClickAt(At(editor, 1, 1.75));
        timeline.SelectedChains.Count.ShouldBe(2);
        editor.PickMenuItem(timeline.Menu, "Remove 2 Chains");

        Chains(boss, 0).Length.ShouldBe(2);
        Chains(boss, 1).ShouldBe(new[] { ("Run", 0.5) });
        editor.UndoLabels.ShouldBe(new[] { "Remove 2 Chains" });
    }
}
