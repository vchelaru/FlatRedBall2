using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;
using static AnimationEditor.App.Tests.Dogfood.SequenceTimelineMultiSelectScenarioTests;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Ctrl+D, Edit &gt; Duplicate and the context menus on timeline chains and tracks. Chains are copied right after the
/// selection on their own tracks, tracks directly in front of (above) the selection; the copies become the selection.
/// Fixture: Body: Walk 0..0.5, Walk 1..1.5, Run 2..2.2. Head: Run 0.5..0.7, Walk 1.5..2. Tail: empty.
/// </summary>
public class SequenceTimelineDuplicateScenarioTests
{
    private static (string Chain, double Start)[] Chains(string path, int track) =>
        SequenceListSave.FromFile(path).Sequences[0].Tracks[track].Chains.Select(c => (c.Chain, c.Start)).ToArray();

    private static Avalonia.Point Header(AnimationEditorHarness editor, int lane) =>
        editor.PointIn(Timeline(editor), 70, Timeline(editor).HeaderCenters(lane).Name);

    private static async Task<(AnimationEditorHarness Editor, string Path)> OpenAsync()
    {
        var editor = new AnimationEditorHarness();
        string path = WriteFixture(editor);
        await editor.OpenAsync(path);
        return (editor, path);
    }

    [AvaloniaFact]
    public async Task CtrlD_DuplicatesTheSelectedChains_RightAfterTheSelection_AndSelectsTheCopies_AsOneUndoEntry()
    {
        var (editor, boss) = await OpenAsync();
        using var _ = editor;
        var timeline = Timeline(editor);
        editor.ClickAt(At(editor, 0, 0.25));
        editor.ClickAt(At(editor, 1, 0.6), RawInputModifiers.Control);

        editor.Press(Key.D, RawInputModifiers.Control);

        // The selection runs from 0 (Walk) to 0.7 (Run, on Head): the copies shift by that and land on their own tracks.
        Chains(boss, 0).ShouldBe(new[] { ("Walk", 0.0), ("Walk", 1.0), ("Run", 2.0), ("Walk", 0.7) });
        Chains(boss, 1).ShouldBe(new[] { ("Run", 0.5), ("Walk", 1.5), ("Run", 1.2) });
        timeline.SelectedChains.ShouldBe(new[] { new TimelineChainId(0, 3), new TimelineChainId(1, 2) });
        editor.UndoLabels.ShouldBe(new[] { "Duplicate 2 chains" });
        editor.Press(Key.Z, RawInputModifiers.Control);
        Chains(boss, 0).Length.ShouldBe(3);
        Chains(boss, 1).Length.ShouldBe(2);
    }

    [AvaloniaFact]
    public async Task CtrlD_OnSelectedTracks_InsertsCopiesInFrontOfThem_AndSelectsThem()
    {
        var (editor, boss) = await OpenAsync();
        using var _ = editor;
        var timeline = Timeline(editor);
        editor.ClickAt(Header(editor, 0));
        editor.ClickAt(Header(editor, 1), RawInputModifiers.Control);

        editor.Press(Key.D, RawInputModifiers.Control);

        SequenceListSave.FromFile(boss).Sequences[0].Tracks.Select(t => t.Name).ShouldBe(new[] { "Body 2", "Head 2", "Body", "Head", "Tail" });
        Chains(boss, 1).ShouldBe(new[] { ("Run", 0.5), ("Walk", 1.5) });
        timeline.SelectedTracks.ShouldBe(new[] { 0, 1 });
        editor.UndoLabels.ShouldBe(new[] { "Duplicate 2 tracks" });
        editor.Press(Key.Z, RawInputModifiers.Control);
        SequenceListSave.FromFile(boss).Sequences[0].Tracks.Count.ShouldBe(3);
    }

    [AvaloniaFact]
    public async Task TheChainContextMenu_OffersDuplicate()
    {
        var (editor, boss) = await OpenAsync();
        using var _ = editor;
        var timeline = Timeline(editor);

        editor.RightClickAt(At(editor, 0, 1.25));
        editor.PickMenuItem(timeline.Menu, "Duplicate");

        Chains(boss, 0).ShouldBe(new[] { ("Walk", 0.0), ("Walk", 1.0), ("Run", 2.0), ("Walk", 1.5) });
        timeline.SelectedChains.ShouldBe(new[] { new TimelineChainId(0, 3) });
    }

    [AvaloniaFact]
    public async Task TheTrackHeaderMenu_OffersDuplicate()
    {
        var (editor, boss) = await OpenAsync();
        using var _ = editor;
        var timeline = Timeline(editor);

        editor.RightClickAt(Header(editor, 1));
        editor.PickMenuItem(timeline.Menu, "Duplicate");

        SequenceListSave.FromFile(boss).Sequences[0].Tracks.Select(t => t.Name).ShouldBe(new[] { "Body", "Head 2", "Head", "Tail" });
        timeline.SelectedTracks.ShouldBe(new[] { 1 });
    }

    [AvaloniaFact]
    public async Task DuplicatingATrack_PutsTheCopyAboveTheOriginalInTheTimeline_AndDrawsItInFrontInThePreview()
    {
        var (editor, boss) = await OpenAsync();
        using var _ = editor;
        var timeline = Timeline(editor);
        // At 0.25 s only Body (Walk 0..0.5) plays.
        editor.ClickAt(editor.PointIn(timeline, timeline.Viewport.XAt(0.25), 8));
        editor.ClickAt(Header(editor, 0));

        editor.Press(Key.D, RawInputModifiers.Control);

        SequenceListSave.FromFile(boss).Sequences[0].Tracks.Select(t => t.Name).ShouldBe(new[] { "Body 2", "Body", "Head", "Tail" });
        timeline.Viewport.LaneTop(0).ShouldBeLessThan(timeline.Viewport.LaneTop(1));
        timeline.Blocks.Where(block => block.Reference.Chain == "Walk" && block.Reference.Start == 0).Select(block => block.Track).ShouldBe(new[] { 0, 1 }, ignoreOrder: true);
        // The preview lists sprites back to front: the original (lane 1) first, the copy (lane 0) last, so the copy is in front.
        editor.Control<PreviewControl>("SequencePreview").Composite!.Select(sprite => sprite.Track).ShouldBe(new[] { 1, 0 });
    }

    [AvaloniaFact]
    public async Task CtrlD_WithTheSequenceListFocused_StillDuplicatesTheSequence()
    {
        var (editor, boss) = await OpenAsync();
        using var _ = editor;
        var list = editor.Control<ListBox>("SequenceList");
        editor.Click((Control)list.ContainerFromIndex(0)!);

        editor.Press(Key.D, RawInputModifiers.Control);

        SequenceListSave.FromFile(boss).Sequences.Select(s => s.Name).ShouldBe(new[] { "Attack", "Attack 2" });
        Chains(boss, 0).Length.ShouldBe(3);
    }

    [AvaloniaFact]
    public async Task CtrlD_WhileATextBoxHasTheFocus_DoesNothingToTheTimeline()
    {
        var (editor, boss) = await OpenAsync();
        using var _ = editor;
        editor.ClickAt(Header(editor, 2));
        editor.Control<SequenceInspectorControl>("SequenceInspector").TrackNameBox.Focus();
        editor.Layout();

        editor.Press(Key.D, RawInputModifiers.Control);

        SequenceListSave.FromFile(boss).Sequences[0].Tracks.Count.ShouldBe(3);
        editor.UndoLabels.ShouldBeEmpty();
    }
}
