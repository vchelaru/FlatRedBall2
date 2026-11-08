using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Rendering;
using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>
/// Second audit of the sequence timeline work: behaviors that still passed their tests when the production line was
/// broken (see plan/sequence-test-audit-2.md). Each test names the line it pins.
/// </summary>
public class SequenceAudit2Tests
{
    // ---- selection and multi-move

    [Fact]
    public void SelectChains_PrimaryThatIsNotInTheSet_FallsBackToTheLastChain()
    {
        var selection = new TimelineSelection();

        selection.SelectChains(new[] { new TimelineChainId(0, 0), new TimelineChainId(0, 1) }, primary: new TimelineChainId(3, 3));

        selection.PrimaryChain.ShouldBe(new TimelineChainId(0, 1));
    }

    [Fact]
    public void MultiMovePlan_NothingDragged_MovesNothing()
    {
        TimelineMultiMove.Plan(Array.Empty<TimelineMultiMove.Origin>(), requestedTime: 3, requestedTracks: 2, trackCount: 4)
            .ShouldBe(new TimelineMultiMove.Result(0, 0));
    }

    [Fact]
    public void Marquee_ReachesAZeroLengthChain_ThatIsDrawnOnePixelWide()
    {
        var viewport = new TimelineViewport();
        var block = new TimelineBlock(0, 0, new SequenceChainReferenceSave { Start = 2 }, Played: 0, Full: 0, CycleLength: 0, IsMissing: false);
        double x = viewport.XAt(2);
        double y = viewport.LaneTop(0) + TimelineLayout.BodyTop + 2;

        // The band starts half a pixel inside the one pixel the chain is drawn with.
        TimelineMarquee.Chains(viewport, new[] { block }, x + 0.5, y, x + 5, y + 1).ShouldBe(new[] { new TimelineChainId(0, 0) });
    }

    [Fact]
    public void MarqueeSelector_ABandEdgeExactlyOnAChainsEdge_TouchesThatChain_OnBothSides()
    {
        // Chains at 0..0.25, 0.5..0.75, 1..1.25. The band runs from the end of the first to the start of the second.
        var sequence = new SequenceSave();
        var track = new SequenceTrackSave { Name = "T" };
        foreach (double start in new[] { 0.0, 0.5, 1.0 }) track.Chains.Add(new SequenceChainReferenceSave { Chain = "Walk", Start = start });
        sequence.Tracks.Add(track);
        var blocks = TimelineLayout.Blocks(sequence, _ => 0.25);
        var viewport = new TimelineViewport();
        var selection = new TimelineSelection();
        var selector = new TimelineMarqueeSelector();
        selector.Begin(Array.Empty<TimelineChainId>());
        double y = viewport.LaneTop(0) + TimelineLayout.BodyTop + 2;

        selector.Update(selection, viewport, blocks, TimelineLayout.TrackStarts(blocks, 1), viewport.XAt(0.25), y, viewport.XAt(0.5), y + 1);

        selection.Chains.ShouldBe(new[] { new TimelineChainId(0, 0), new TimelineChainId(0, 1) });
    }

    [Fact]
    public void SnapTargetsOthers_ANeighbourCutByTheSecondOfSeveralDraggedChains_OffersNoPlayedEnd()
    {
        // X (3 s) is cut by Y at 1; Z (3 s) is cut by W at 5. Y and W are dragged together, so neither cut end is a place to snap to.
        var sequence = new SequenceSave();
        var first = new SequenceTrackSave { Name = "T0" };
        first.Chains.Add(new SequenceChainReferenceSave { Chain = "X", Start = 0, Loops = 3 });
        first.Chains.Add(new SequenceChainReferenceSave { Chain = "Y", Start = 1, Loops = 1 });
        var second = new SequenceTrackSave { Name = "T1" };
        second.Chains.Add(new SequenceChainReferenceSave { Chain = "Z", Start = 4, Loops = 3 });
        second.Chains.Add(new SequenceChainReferenceSave { Chain = "W", Start = 5, Loops = 1 });
        sequence.Tracks.Add(first);
        sequence.Tracks.Add(second);
        var blocks = TimelineLayout.Blocks(sequence, _ => 1);
        var dragged = new[] { first.Chains[1], second.Chains[1] };

        var targets = TimelineSnapTargets.Others(blocks, dragged, playhead: 100);

        targets.Where(t => t is { Kind: SnapKind.ChainEdge, Track: 0 }).Select(t => t.Time).ShouldBe(new[] { 0.0, 3.0 });
        targets.Where(t => t is { Kind: SnapKind.ChainEdge, Track: 1 }).Select(t => t.Time).ShouldBe(new[] { 4.0, 7.0 });
    }

    // ---- clipboard envelopes

    private const string OneChain = "[{\"source\":\"hero.achx\",\"chain\":\"Walk\",\"start\":0,\"loops\":1}]";

    [Fact]
    public void TimelineClipboard_VersionZero_IsADamagedCopy()
    {
        string text = "{\"kind\":\"frb2-animation-editor-chains\",\"version\":0,\"sources\":[],\"chains\":" + OneChain + "}";

        TimelineClipboard.TryRead(text, out _, out var error).ShouldBeFalse();

        error.ShouldNotBeNull().ShouldContain("version");
    }

    [Fact]
    public void TimelineClipboard_SourcesThatAreNotPaths_AreADamagedCopy()
    {
        string text = "{\"kind\":\"frb2-animation-editor-chains\",\"version\":1,\"sources\":[1],\"chains\":" + OneChain + "}";

        TimelineClipboard.TryRead(text, out _, out var error).ShouldBeFalse();

        error.ShouldNotBeNull().ShouldContain("sources");
    }

    [Fact]
    public void TimelineClipboard_TrackListWithoutTheChainToTrackList_IsADamagedCopy()
    {
        string text = "{\"kind\":\"frb2-animation-editor-chains\",\"version\":1,\"sources\":[],\"chains\":" + OneChain
            + ",\"sourceTracks\":[{\"name\":\"A\",\"lane\":0}]}";

        TimelineClipboard.TryRead(text, out _, out var error).ShouldBeFalse();

        error.ShouldNotBeNull().ShouldContain("track information");
    }

    [Fact]
    public void TimelineClipboard_ACopiedTrackWithNoVisibleField_IsVisible()
    {
        string text = "{\"kind\":\"frb2-animation-editor-chains\",\"version\":1,\"sources\":[],\"chains\":" + OneChain
            + ",\"sourceTracks\":[{\"name\":\"A\",\"lane\":0}],\"chainTracks\":[0]}";

        TimelineClipboard.TryRead(text, out var content, out _).ShouldBeTrue();

        content!.SourceTracks.Single().Visible.ShouldBeTrue();
    }

    [Fact]
    public void SequenceClipboard_VersionZero_IsADamagedCopy()
    {
        string text = "{\"kind\":\"frb2-animation-editor-sequence\",\"version\":0,\"sources\":[],\"sequence\":{\"name\":\"A\",\"tracks\":[]}}";

        SequenceClipboard.TryRead(text, out _, out var error).ShouldBeFalse();

        error.ShouldNotBeNull().ShouldContain("version");
    }

    // ---- paste and duplicate

    private static SequenceChainReferenceSave Chain(string name, double start, string source = "gone.achx", float loops = 1) =>
        new() { Source = source, Chain = name, Start = start, Loops = loops };

    private static SequenceTrackSave Track(string name, params SequenceChainReferenceSave[] chains)
    {
        var track = new SequenceTrackSave { Name = name };
        track.Chains.AddRange(chains);
        return track;
    }

    // No source file exists, so every chain is missing and drawn one second long.
    private static SequenceDocument Open(string folder, params SequenceTrackSave[] tracks)
    {
        Directory.CreateDirectory(folder);
        var sequence = new SequenceSave { Name = "Idle" };
        sequence.Tracks.AddRange(tracks);
        var save = new SequenceListSave { Sources = { "gone.achx" } };
        save.Sequences.Add(sequence);
        string path = Path.Combine(folder, "boss.seqj");
        save.Save(path);
        return SequenceDocument.Load(path);
    }

    private static TimelineClipboardContent Read(string text)
    {
        TimelineClipboard.TryRead(text, out var content, out _).ShouldBeTrue();
        return content!;
    }

    private static string[] Names(SequenceDocument document) => document.Save.Sequences[0].Tracks.Select(t => t.Name).ToArray();

    private static (string Chain, double Start)[] Chains(SequenceDocument document, int track) =>
        document.Save.Sequences[0].Tracks[track].Chains.Select(c => (c.Chain, c.Start)).ToArray();

    [Fact]
    public void PasteChains_WhenTheSelectedTrackIsNoLongerThere_LandsByTrackName()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir.Path, Track("Body"), Track("Head", Chain("Hat", 0)));
        var content = Read(document.CopyChains(0, new[] { new TimelineChainId(1, 0) }));

        document.PasteChains(new UndoManager(), 0, content, selectedTrack: 7, time: 3);

        Names(document).ShouldBe(new[] { "Body", "Head" });
        Chains(document, 1).ShouldBe(new[] { ("Hat", 0.0), ("Hat", 3.0) });
    }

    [Fact]
    public void PasteChains_ThatMakeATrackWhoseNameIsTaken_GiveItAFreeName()
    {
        using var dir = new TestHelpers.TempDir();
        var source = Open(Path.Combine(dir.Path, "a"), Track("X", Chain("Walk", 0)), Track("B", Chain("Run", 0)));
        var destination = Open(Path.Combine(dir.Path, "b"), Track("A"), Track("B"));
        var content = Read(source.CopyChains(0, new[] { new TimelineChainId(0, 0), new TimelineChainId(1, 0) }));

        // Anchored on the second track: X lands on B, and B's lane is one further down, past the last track.
        destination.PasteChains(new UndoManager(), 0, content, selectedTrack: 1, time: 0);

        Names(destination).ShouldBe(new[] { "A", "B", "B 2" });
        Chains(destination, 2).ShouldBe(new[] { ("Run", 0.0) });
    }

    [Fact]
    public void PasteChains_AtANegativeTime_PutsTheEarliestChainAtZero()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir.Path, Track("Body", Chain("Walk", 2)));
        var content = Read(document.CopyChains(0, new[] { new TimelineChainId(0, 0) }));

        document.PasteChains(new UndoManager(), 0, content, selectedTrack: 0, time: -5);

        Chains(document, 0).ShouldBe(new[] { ("Walk", 2.0), ("Walk", 0.0) });
    }

    [Fact]
    public void PasteChains_FromACopyWithNoTrackInformationOrName_MakesATrackNamedTrack()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir.Path, Track("Head"));
        var content = Read("{\"kind\":\"frb2-animation-editor-chains\",\"version\":1,\"sources\":[],\"chains\":" + OneChain + "}");

        document.PasteChains(new UndoManager(), 0, content, selectedTrack: null, time: 0);

        Names(document).ShouldBe(new[] { "Head", "Track" });
    }

    [Fact]
    public void PasteChains_OfTwoChainsFromTheSameRefusedSource_ReportsItOnce()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir.Path, Track("Head"));
        string chains = "[{\"source\":\"x.png\",\"chain\":\"A\",\"start\":0,\"loops\":1},{\"source\":\"x.png\",\"chain\":\"B\",\"start\":1,\"loops\":1}]";
        var content = Read("{\"kind\":\"frb2-animation-editor-chains\",\"version\":1,\"sources\":[],\"chains\":" + chains + "}");

        var result = document.PasteChains(new UndoManager(), 0, content, selectedTrack: 0, time: 0);

        result.Errors.Count.ShouldBe(1);
        result.Errors[0].ShouldContain("x.png");
    }

    [Fact]
    public void PasteTracks_ThatShareAnUnnumberedAndANumberedName_GetDistinctNames()
    {
        using var dir = new TestHelpers.TempDir();
        var source = Open(Path.Combine(dir.Path, "a"), Track("A"), Track("A 2"));
        var destination = Open(Path.Combine(dir.Path, "b"), Track("A"));
        var content = Read(source.CopyTracks(0, new[] { 0, 1 }));

        destination.PasteTracks(new UndoManager(), 0, content, afterTrack: null);

        Names(destination).ShouldBe(new[] { "A", "A 2", "A 3" });
    }

    [Fact]
    public void DuplicateTracks_OfTwoTracksWhoseCopiesWouldCollide_GetDistinctNames()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir.Path, Track("A"), Track("A 2"));

        document.DuplicateTracks(new UndoManager(), 0, new[] { 0, 1 });

        Names(document).ShouldBe(new[] { "A", "A 2", "A 3", "A 4" });
    }

    [Fact]
    public void DuplicateChains_OfAChainThatIsCutByTheNextOne_ShiftsByItsFullLength()
    {
        using var dir = new TestHelpers.TempDir();
        // A missing chain is one second long; Run starts at 0.4, so Walk plays only 0.4 s of its 1 s.
        var document = Open(dir.Path, Track("Body", Chain("Walk", 0), Chain("Run", 0.4)));

        document.DuplicateChains(new UndoManager(), 0, new[] { new TimelineChainId(0, 0) });

        Chains(document, 0).ShouldBe(new[] { ("Walk", 0.0), ("Run", 0.4), ("Walk", 1.0) });
    }

    [Fact]
    public void RenameSequence_KeepSelection_RedoStillSelectsTheRenamedOne_WhereverTheSelectionHasGone()
    {
        using var dir = new TestHelpers.TempDir();
        var save = new SequenceListSave();
        foreach (string name in new[] { "A", "B", "C" }) save.Sequences.Add(new SequenceSave { Name = name });
        string path = Path.Combine(dir.Path, "boss.seqj");
        save.Save(path);
        var document = SequenceDocument.Load(path);
        var undo = new UndoManager();
        document.SelectedSequence = 2;
        document.RenameSequence(undo, 0, "Alpha", keepSelection: true);
        undo.Undo();
        document.SelectedSequence = 2;

        undo.Redo();

        document.SelectedSequence.ShouldBe(0);
    }

    // ---- reload

    private static void WriteSource(string folder, string chainName, DateTime stamp)
    {
        var list = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        var chain = new AnimationChainSave { Name = chainName };
        chain.Frames.Add(new AnimationFrameSave { TextureName = "sheet.png", FrameLength = 0.1f });
        list.AnimationChains.Add(chain);
        string path = Path.Combine(folder, "hero.achj");
        list.SaveJson(path);
        File.SetLastWriteTimeUtc(path, stamp);
    }

    private static SequenceDocument OpenWithSource(string folder)
    {
        var save = new SequenceListSave { Sources = { "hero.achj" } };
        save.Sequences.Add(new SequenceSave { Name = "Attack" });
        string path = Path.Combine(folder, "boss.seqj");
        save.Save(path);
        return SequenceDocument.Load(path);
    }

    [Fact]
    public void ReloadChangedSources_RewrittenToTheSameLength_IsSeenByItsNewTimestamp()
    {
        using var dir = new TestHelpers.TempDir();
        var first = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        WriteSource(dir.Path, "Walk", first);
        var document = OpenWithSource(dir.Path);

        WriteSource(dir.Path, "Runs", first.AddSeconds(5));

        document.ReloadChangedSources().ShouldBeTrue();
        document.SourceChains("hero.achj")!.AnimationChains.Single().Name.ShouldBe("Runs");
    }

    [Fact]
    public void ReloadChangedSources_RewrittenToAnotherLengthUnderTheSameTimestamp_IsSeenByItsLength()
    {
        using var dir = new TestHelpers.TempDir();
        var stamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        WriteSource(dir.Path, "Walk", stamp);
        var document = OpenWithSource(dir.Path);

        WriteSource(dir.Path, "WalkSlowly", stamp);

        document.ReloadChangedSources().ShouldBeTrue();
        document.SourceChains("hero.achj")!.AnimationChains.Single().Name.ShouldBe("WalkSlowly");
    }

    // ---- culling, scroll bars, ruler, layout

    private static TimelineBlock Block(int track, double start, double played, double full) =>
        new(track, 0, new SequenceChainReferenceSave { Start = start }, played, full, CycleLength: 1, IsMissing: false);

    [Fact]
    public void IsBlockVisible_ZeroLengthBlockJustLeftOfTheLanes_IsStillDrawnOnePixelWide()
    {
        var viewport = new TimelineViewport();
        viewport.SetScrollSeconds(2.5 / viewport.PixelsPerSecond);   // time 0 is now 2.5 px left of the lanes

        TimelineCulling.IsBlockVisible(viewport, Block(0, 0, 0, 0), viewWidth: 1120, viewHeight: 400).ShouldBeTrue();
    }

    [Fact]
    public void IsBlockVisible_BlockEndingJustLeftOfTheLanes_StillShowsItsSelectedOutline()
    {
        var viewport = new TimelineViewport();
        // The one second block ends 1.5 px left of the lanes; the 2 px outline reaches back in.
        viewport.SetScrollSeconds((viewport.PixelsPerSecond + 1.5) / viewport.PixelsPerSecond);

        TimelineCulling.IsBlockVisible(viewport, Block(0, 0, 1, 1), viewWidth: 1120, viewHeight: 400).ShouldBeTrue();
    }

    [Fact]
    public void IsBlockVisible_TallLaneWhoseBottomIsStillBelowTheRuler_IsTrue()
    {
        var viewport = new TimelineViewport();
        double tall = TimelineViewport.LaneHeight + TimelineLayout.EventRowHeight;
        viewport.SetLaneHeights(new[] { tall, tall, tall, tall });
        // The first lane's base height is scrolled up under the ruler, but its event row is not.
        viewport.ScrollVertically(TimelineViewport.LaneHeight + 5, trackCount: 4, viewHeight: TimelineViewport.RulerHeight + 150);
        viewport.ScrollY.ShouldBe(TimelineViewport.LaneHeight + 5);

        TimelineCulling.IsBlockVisible(viewport, Block(0, 0, 1, 1), viewWidth: 1120, viewHeight: TimelineViewport.RulerHeight + 150).ShouldBeTrue();
    }

    [Fact]
    public void ScrollBars_NegativeScroll_ReadsAsZero()
    {
        TimelineScrollBars.Horizontal(-5, 100, TimelineViewport.HeaderWidth + 600, 20).Value.ShouldBe(0);
        TimelineScrollBars.Vertical(-5, contentHeight: 300, controlHeight: 200).Value.ShouldBe(0);
    }

    [Fact]
    public void ScrollBars_VerticalScrollPastTheContent_ReadsAsTheMaximum()
    {
        var range = TimelineScrollBars.Vertical(999, contentHeight: 300, controlHeight: 200);

        range.Maximum.ShouldBe(100);
        range.Value.ShouldBe(100);
    }

    [Fact]
    public void LaneHeights_SeveralBlocksWithEventsOnOneTrack_AddOneEventRow()
    {
        var blocks = new[] { Block(0, 0, 1, 1), Block(0, 2, 1, 1), Block(1, 0, 1, 1) };

        var heights = TimelineLayout.LaneHeights(blocks, 2, block => block.Track == 0);

        heights.ShouldBe(new[] { TimelineViewport.LaneHeight + TimelineLayout.EventRowHeight, TimelineViewport.LaneHeight });
    }

    [Fact]
    public void FormatTick_HalfSecondSteps_KeepTheirFraction()
    {
        TimelineRuler.FormatTick(1.5, step: 0.5).ShouldBe("1.5s");
    }

    [Fact]
    public void StepZoom_NeverLeavesTheZoomLimits_EvenWhenAPresetIsOutsideThem()
    {
        var presets = ZoomPresetStepper.Presets;

        ZoomPresetStepper.StepZoom(0.08f, -1, presets, minZoom: 0.1f, maxZoom: 4f).ShouldBe(0.1f);
        ZoomPresetStepper.StepZoom(7f, 1, presets, minZoom: 0.1f, maxZoom: 4f).ShouldBe(4f);
    }

    [Fact]
    public void ForCopy_ANameEndingInADigitThenALetter_IsNotNumbered()
    {
        NameGenerator.ForCopy(new[] { "Walk 2b" }, "Walk 2b").ShouldBe("Walk 2b 2");
    }

    // ---- event markers

    private static EventFrame F(double duration, params string[] names) => new(duration, names);

    [Fact]
    public void Occurrences_AReversedPingPong_MarksItsFirstHalfAsTheReturn()
    {
        var frames = new[] { F(1, "a"), F(1, "b") };

        var forward = EventMarkers.Occurrences(frames, pingPong: true, reverse: false, played: 4);
        var reversed = EventMarkers.Occurrences(frames, pingPong: true, reverse: true, played: 4);

        forward.Select(o => (o.Name, o.IsReturn)).ShouldBe(new[] { ("a", false), ("b", false), ("a", true) });
        reversed.Select(o => (o.Name, o.IsReturn)).ShouldBe(new[] { ("b", true), ("a", true), ("b", false) });
    }

    [Fact]
    public void Occurrences_AFrameWithANegativeDuration_TakesNoTime()
    {
        var occurrences = EventMarkers.Occurrences(new[] { F(-1, "x"), F(1, "y") }, pingPong: false, reverse: false, played: 1);

        occurrences.Single(o => o.Name == "y").Time.ShouldBe(0);
    }

    [Fact]
    public void FramesOf_AnEventWithNoName_ReadsAsAnEmptyName()
    {
        var frame = new AnimationFrameSave { FrameLength = 0.1f };
        frame.Events.Add(new FlatRedBall2.Animation.AnimationFrameEvent { Name = null! });

        EventMarkers.FramesOf(new AnimationChainSave { Frames = { frame } })[0].Names.ShouldBe(new[] { string.Empty });
    }

    [Fact]
    public void Group_AMergedMarker_IsPlacedAtItsFirstOccurrence()
    {
        var occurrences = new[] { new EventOccurrence("a", 0, 0.00, 0, false), new EventOccurrence("b", 1, 0.01, 0, false) };

        var marker = EventMarkers.Group(occurrences, pixelsPerSecond: 100).Single();

        marker.Time.ShouldBe(0.00);
        marker.Count.ShouldBe(2);
    }

    [Fact]
    public void MarkerAt_JustPastTheMarkerWidth_FindsNothing()
    {
        var markers = EventMarkers.Group(new[] { new EventOccurrence("a", 0, 1, 0, false) }, pixelsPerSecond: 100);
        double x = markers[0].X;

        EventMarkers.MarkerAt(markers, x + EventMarkers.MarkerPixels - 0.01).ShouldBe(markers[0]);
        EventMarkers.MarkerAt(markers, x + EventMarkers.MarkerPixels).ShouldBeNull();
    }

    // ---- hover cards

    [Fact]
    public void EventReadout_MoreThanEightEventsInOneMarker_ListsTheFirstEightThenAnEllipsis()
    {
        var occurrences = Enumerable.Range(0, 9).Select(i => new EventOccurrence("e" + i, i, i * 0.01, 0, false)).ToArray();
        var marker = new EventMarker(0, 0, occurrences);

        var card = EventReadout.At("Walk", 10, 0, marker);

        card.Lines.Count.ShouldBe(10);
        card.Lines[0].ShouldBe("9 events");
        card.Lines[8].ShouldStartWith("e7");
        card.Lines[9].ShouldBe("…");
    }

    [Fact]
    public void FilmstripReadout_AMergedCellThatShowsOneFrameTwice_ListsThatFrameOnce()
    {
        var visits = new[] { new FilmstripVisit(0, 0, 0.01, 0), new FilmstripVisit(1, 0.01, 0.02, 0), new FilmstripVisit(0, 0.02, 0.03, 0, IsReturn: true) };
        var cell = new FilmstripCell(0, visits, 0, 0.03, 0, 3, 25, 0, IsMerged: true);

        FilmstripReadout.At("Walk", 2, 0, cell, visits[0]).Lines.Last().ShouldBe("merged: 1, 2");
    }

    // ---- filmstrip

    private static FilmstripFrame Frame(double duration, double width = 10, double height = 10) => new(duration, width, height);

    [Fact]
    public void Cells_AOneFrameLoopWithoutPingPong_DrawsOneCellPerCycle()
    {
        var cells = FilmstripLayout.Cells(new[] { Frame(1) }, pingPong: false, reverse: false, played: 2, pixelsPerSecond: 100, cellHeight: 30);

        cells.Count.ShouldBe(2);
    }

    [Fact]
    public void Cells_AMergedPingPongTurnaround_CountsBothShowingsWhenChoosingTheLongestFrame()
    {
        // 1 px per frame showing, so they gather into one cell; frame 1 is shown twice in a row (the turnaround), frame 0 once at each end.
        var cells = FilmstripLayout.Cells(new[] { Frame(0.01), Frame(0.01) }, pingPong: true, reverse: false, played: 0.04, pixelsPerSecond: 100, cellHeight: 30);

        cells.Single().Frame.ShouldBe(1);
    }

    [Fact]
    public void Cells_FramesOfEqualLengthInOneCell_ShowTheFirst()
    {
        var cells = FilmstripLayout.Cells(new[] { Frame(0.01), Frame(0.01) }, pingPong: false, reverse: false, played: 0.02, pixelsPerSecond: 100, cellHeight: 30);

        cells.Single().Frame.ShouldBe(0);
    }

    [Fact]
    public void Cells_AFrameWithNoTexture_IsAsWideAsTheCellIsTall()
    {
        var cells = FilmstripLayout.Cells(new[] { Frame(1, width: 0, height: 0) }, pingPong: false, reverse: false, played: 1, pixelsPerSecond: 100, cellHeight: 30);

        cells.Single().SpriteWidth.ShouldBe(30);
    }

    [Fact]
    public void Cells_ACutThatEndsExactlyOnAFrameBoundary_DrawsNoCellForTheNextFrame()
    {
        var cells = FilmstripLayout.Cells(new[] { Frame(1), Frame(1) }, pingPong: false, reverse: false, played: 1, pixelsPerSecond: 100, cellHeight: 30);

        // The next frame would start where the cut is: it has no cell, and does not ride along as a zero-width visit.
        cells.Select(c => c.Frame).ShouldBe(new[] { 0 });
        cells.Single().Visits.Count.ShouldBe(1);
        cells.Single().IsMerged.ShouldBeFalse();
    }

    [Fact]
    public void FramesOf_UvsStoredRightToLeft_StillMeasureAPositiveWidth()
    {
        var chain = new AnimationChainSave { Frames = { new AnimationFrameSave { LeftCoordinate = 0.5f, RightCoordinate = 0f, TopCoordinate = 0f, BottomCoordinate = 1f, FrameLength = 0.1f } } };

        FilmstripLayout.FramesOf(chain, _ => (100, 50))[0].SpriteWidth.ShouldBe(50);
    }

    [Fact]
    public void CellAt_TheExactRightEdgeOfTheLastCell_IsNotInIt()
    {
        var cells = FilmstripLayout.Cells(new[] { Frame(1) }, pingPong: false, reverse: false, played: 1, pixelsPerSecond: 100, cellHeight: 30);

        FilmstripLayout.CellAt(cells, 99.99).ShouldBe(cells[0]);
        FilmstripLayout.CellAt(cells, 100).ShouldBeNull();
    }

    [Fact]
    public void VisitAt_ExactlyWhereOneShowingEnds_IsTheNextShowing()
    {
        var cells = FilmstripLayout.Cells(new[] { Frame(1), Frame(1) }, pingPong: true, reverse: false, played: 4, pixelsPerSecond: 100, cellHeight: 30);
        var merged = cells.Single(c => c.Frame == 1);

        FilmstripLayout.VisitAt(merged, 2.0).Start.ShouldBe(2.0);
    }
}
