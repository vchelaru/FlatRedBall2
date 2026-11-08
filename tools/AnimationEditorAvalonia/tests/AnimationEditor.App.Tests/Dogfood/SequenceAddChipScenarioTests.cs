using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Adding a library chip's chain to the timeline without dragging it: double-click, Enter and the chip's menu. The rule:
/// the chain starts at the playhead, on a new track at the end of the list (like a drop on the dashed row) unless the
/// menu's "Add to Selected Track" names the one selected track. The undo entry is the drop's.
/// </summary>
public class SequenceAddChipScenarioTests
{
    // Walk: five 0.1 s frames (0.5 s). Run: two frames (0.2 s). Attack has one track, Body, with Walk on it.
    private static string WriteFixture(AnimationEditorHarness editor, bool withSequence = true)
    {
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16), (48, 0, 16, 16), (0, 16, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (0, 32, 16, 16), (16, 32, 16, 16)));
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        if (withSequence)
        {
            var body = new SequenceTrackSave { Name = "Body" };
            body.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk", Start = 0, Loops = 1 });
            var sequence = new SequenceSave { Name = "Attack" };
            sequence.Tracks.Add(body);
            save.Sequences.Add(sequence);
        }
        save.Save(path);
        return path;
    }

    private static SequenceTimelineControl Timeline(AnimationEditorHarness editor) =>
        editor.Control<SequenceTimelineControl>("SequenceTimeline");

    private static LibraryChipView Chip(AnimationEditorHarness editor, string chain) =>
        editor.Control<SequenceLibraryControl>("SequenceLibrary").ChipFor("hero.achx", chain)!;

    private static List<SequenceTrackSave> SavedTracks(string path) => SequenceListSave.FromFile(path).Sequences[0].Tracks;

    // A click on the ruler scrubs the playhead there.
    private static double ScrubTo(AnimationEditorHarness editor, double seconds)
    {
        var timeline = Timeline(editor);
        editor.ClickAt(editor.PointIn(timeline, timeline.Viewport.XAt(seconds), 8));
        return timeline.Playhead;
    }

    [AvaloniaFact]
    public async Task DoubleClickingAChip_AddsANewTrackAtThePlayhead_SelectsTheChain_AsOneUndoEntry()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);
        double playhead = ScrubTo(editor, 0.3);
        playhead.ShouldBeGreaterThan(0.2);

        editor.DoubleClickAt(editor.CenterOf(Chip(editor, "Run")));

        var tracks = SavedTracks(boss);
        tracks.Select(track => track.Name).ShouldBe(new[] { "Body", "Run" });
        tracks[1].Chains.Select(chain => (chain.Chain, chain.Start)).ShouldBe(new[] { ("Run", playhead) });
        editor.UndoLabels.ShouldBe(new[] { "Add Track 'Run'" });
        Timeline(editor).Selection.ShouldBe(new TimelineChainId(1, 0));
        editor.Press(Key.Z, RawInputModifiers.Control);
        SavedTracks(boss).Count.ShouldBe(1);
    }

    [AvaloniaFact]
    public async Task DoubleClickingAChip_WithATrackSelected_StillMakesANewTrack()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);
        Timeline(editor).Select(null, 0);

        editor.DoubleClickAt(editor.CenterOf(Chip(editor, "Run")));

        SavedTracks(boss).Select(track => (track.Name, track.Chains.Count)).ShouldBe(new[] { ("Body", 1), ("Run", 1) });
    }

    [AvaloniaFact]
    public async Task EnterOnAFocusedChip_AddsItsChainLikeADoubleClick()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);
        LibraryChipView walk = Chip(editor, "Walk");
        walk.Focusable.ShouldBeTrue();
        walk.Focus();
        editor.Layout();
        walk.IsFocused.ShouldBeTrue();

        editor.Press(Key.Enter);

        SavedTracks(boss).Select(track => track.Name).ShouldBe(new[] { "Body", "Walk" });
        editor.UndoLabels.ShouldBe(new[] { "Add Track 'Walk'" });
        Timeline(editor).Selection.ShouldBe(new TimelineChainId(1, 0));
    }

    [AvaloniaFact]
    public async Task ChipMenu_AddToTimeline_MakesANewTrack_AndAddToSelectedTrackIsDisabledWithoutASelectedTrack()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);
        LibraryChipView run = Chip(editor, "Run");

        editor.RightClick(run);

        run.ContextMenu!.IsOpen.ShouldBeTrue();
        run.ContextMenu.Items.OfType<MenuItem>().Select(item => (string?)item.Header)
            .ShouldBe(new[] { "Add to Timeline", "Add to Selected Track", "Go to Definition", "Open Source" });
        run.ContextMenu.Items.OfType<MenuItem>().Single(item => (string?)item.Header == "Add to Selected Track").IsEnabled.ShouldBeFalse();
        editor.PickMenuItem(run.ContextMenu, "Add to Timeline");

        SavedTracks(boss).Select(track => track.Name).ShouldBe(new[] { "Body", "Run" });
        editor.UndoLabels.ShouldBe(new[] { "Add Track 'Run'" });
        Timeline(editor).Selection.ShouldBe(new TimelineChainId(1, 0));
    }

    [AvaloniaFact]
    public async Task ChipMenu_AddToSelectedTrack_AddsToTheSelectedTrackAtThePlayhead()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);
        double playhead = ScrubTo(editor, 0.3);
        Timeline(editor).Select(null, 0);
        LibraryChipView run = Chip(editor, "Run");

        editor.RightClick(run);

        run.ContextMenu!.Items.OfType<MenuItem>().Single(item => (string?)item.Header == "Add to Selected Track").IsEnabled.ShouldBeTrue();
        editor.PickMenuItem(run.ContextMenu, "Add to Selected Track");

        var tracks = SavedTracks(boss);
        tracks.Count.ShouldBe(1);
        tracks[0].Chains.Select(chain => (chain.Chain, chain.Start)).ShouldBe(new[] { ("Walk", 0.0), ("Run", playhead) });
        editor.UndoLabels.ShouldBe(new[] { "Add 'Run' to 'Body'" });
        Timeline(editor).Selection.ShouldBe(new TimelineChainId(0, 1));
    }

    [AvaloniaFact]
    public async Task ChipMenu_OpenSource_OpensTheSourceInItsOwnTab()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);
        LibraryChipView run = Chip(editor, "Run");

        editor.RightClick(run);
        editor.PickMenuItem(run.ContextMenu!, "Open Source");
        await editor.WaitUntilAsync(() => editor.TabLabels.Contains("hero.achx"), TimeSpan.FromSeconds(5));

        editor.TabLabels.ShouldBe(new[] { "boss.seqj", "hero.achx" });
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task AddingAChipToAFileWithNoSequence_SaysSoInsteadOfDoingNothing()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, withSequence: false);
        await editor.OpenAsync(boss);

        editor.DoubleClickAt(editor.CenterOf(Chip(editor, "Run")));

        editor.ToastText.ShouldNotBeNull().ShouldContain("sequence");
        editor.UndoLabels.ShouldBeEmpty();
    }
}
