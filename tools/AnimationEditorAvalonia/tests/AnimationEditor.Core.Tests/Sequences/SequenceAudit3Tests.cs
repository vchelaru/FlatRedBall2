using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>
/// Third audit of the sequence timeline work, for #1682 (a non-looping chain plays once and holds its last frame):
/// behaviors that still passed their tests when the production line was broken (see plan/sequence-test-audit-3.md).
/// </summary>
public class SequenceAudit3Tests
{
    private const double Height = 25;

    // ---- filmstrip: the hold

    private static FilmstripFrame[] Frames(params double[] durations) =>
        durations.Select(d => new FilmstripFrame(d, 32, 32)).ToArray();

    [Fact]
    public void Cells_ALoopingChainCutMidCycle_HasNoHold_AndItsClippedLastCellKeepsTheWindowOfItsFullWidth()
    {
        // Two 0.1 s frames, 2.25 cycles: the last cell is cut at 0.45 s, 5 px of its natural 10 px. The sprite is 25 px wide,
        // so the cell is a window centered on it, and clipping the cell must not move that window.
        var cells = FilmstripLayout.Cells(Frames(0.1, 0.1), false, false, 0.45, 100, Height, loop: true);

        cells.SelectMany(c => c.Visits).ShouldAllBe(v => !v.IsHold);
        cells[4].Width.ShouldBe(5, 1e-9);
        cells[4].SpriteX.ShouldBe(-7.5, 1e-9);
    }

    [Fact]
    public void Cells_ANonLoopingChainPlayedExactlyOneCycle_HasNoHold_WhateverTheRoundingOfTheNumbers()
    {
        // 0.25 + 0.25 is exactly 0.5 in binary, so there is no float noise to hide behind.
        var exact = FilmstripLayout.Cells(Frames(0.25, 0.25), false, false, 0.5, 100, Height, loop: false);
        var hair = FilmstripLayout.Cells(Frames(0.25, 0.25), false, false, 0.5 + 5e-10, 100, Height, loop: false);

        exact.SelectMany(c => c.Visits).ShouldAllBe(v => !v.IsHold);
        hair.SelectMany(c => c.Visits).ShouldAllBe(v => !v.IsHold);
    }

    [Fact]
    public void Cells_ANonLoopingChainPlayedJustPastItsCycle_HoldsItsLastFrame()
    {
        var cells = FilmstripLayout.Cells(Frames(0.25, 0.25), false, false, 0.5 + 1e-6, 100, Height, loop: false);

        cells.Last().Visits.Single().IsHold.ShouldBeTrue();
        cells.Last().End.ShouldBe(0.5 + 1e-6, 1e-9);
    }

    [Fact]
    public void Cells_AHeldCell_IsAWideCell_SoItsSpriteLeftAlignsAtTheFramesStart()
    {
        // The last frame is 10 px natural, the hold makes the cell 70 px: a wide cell, not a 25 px sprite squeezed into a window.
        var cells = FilmstripLayout.Cells(Frames(0.1, 0.1, 0.1), false, false, 0.9, 100, Height, loop: false);

        cells[2].Width.ShouldBe(70, 1e-9);
        cells[2].SpriteWidth.ShouldBe(25, 1e-9);
        cells[2].SpriteX.ShouldBe(0, 1e-9);
    }

    [Fact]
    public void Cells_ANonLoopingOneFramePingPong_HoldsOnlyTheShowingThatEndsTheCycle()
    {
        // 0.2 s frame, ping-pong cycle 0.4 s: the two showings are one double cell; the block is 1.0 s, so the second is held.
        var cells = FilmstripLayout.Cells(Frames(0.2), true, false, 1.0, 100, Height, loop: false);

        var cell = cells.Single();
        cell.Visits.Select(v => v.IsHold).ShouldBe(new[] { false, true });
        cell.Visits[0].End.ShouldBe(0.2, 1e-9);
        cell.Visits[1].End.ShouldBe(1.0, 1e-9);
        cell.End.ShouldBe(1.0, 1e-9);
    }

    [Fact]
    public void Cells_ANonLoopingChainWhoseLastFrameHasNoLength_StretchesThatFrameOverTheHold()
    {
        // The scene holds frame n-1 whatever its length (Locate falls through to the last frame), so the strip must too.
        var cells = FilmstripLayout.Cells(Frames(0.1, 0.1, 0), false, false, 0.9, 100, Height, loop: false);

        Order(cells).ShouldBe(new[] { 0, 1, 2 });
        cells[2].X.ShouldBe(20, 1e-9);
        cells[2].Width.ShouldBe(70, 1e-9);
        cells[2].Visits.Single().IsHold.ShouldBeTrue();
    }

    [Fact]
    public void Cells_ANonLoopingChainAskedForAThousandMillionCycles_IsStillOneCycleAndAHold()
    {
        var cells = FilmstripLayout.Cells(Frames(0.1, 0.1, 0.1), false, false, 3e8, 100, Height, loop: false);

        Order(cells).ShouldBe(new[] { 0, 1, 2 });
        cells.Last().End.ShouldBe(3e8, 1);
    }

    [Fact]
    public void Cells_ALoopingChainAskedForAThousandMillionCycles_StopsAtTheCellCap()
    {
        var cells = FilmstripLayout.Cells(Frames(0.1, 0.1, 0.1), false, false, 3e8, 100, Height, loop: true);

        cells.Count.ShouldBeInRange(1, 50_000);
    }

    private static int[] Order(IReadOnlyList<FilmstripCell> cells) => cells.Select(c => c.Frame).ToArray();

    // ---- hover card: the hold line

    private static FilmstripCell Cell(int frame, bool merged, params FilmstripVisit[] visits) =>
        new(frame, visits, visits[0].Start, visits[^1].End, 0, 10, 25, 0, merged);

    [Fact]
    public void At_TheHeldReturnOfAPingPong_SaysItIsHeld_AfterTheHalf()
    {
        var forward = new FilmstripVisit(0, 0, 0.1, 0);
        var held = new FilmstripVisit(0, 0.1, 1.2, 0, IsReturn: true, IsHold: true);

        FilmstripReadout.At("Chomp", 3, 0, Cell(0, false, forward, held), held).Lines
            .ShouldBe(new[] { "1 / 3", "1.10 s", "at 0.10 s", "return", "held" });
    }

    [Fact]
    public void At_AHeldTailThatIsTheOnlyShowingOfItsCell_SaysHeldWithoutAHalf()
    {
        var held = new FilmstripVisit(0, 0.5, 1.2, 0, IsReturn: true, IsHold: true);

        FilmstripReadout.At("Chomp", 3, 0, Cell(0, false, held), held).Lines.ShouldBe(new[] { "1 / 3", "0.70 s", "at 0.50 s", "held" });
    }

    [Fact]
    public void At_AHeldTailInsideAMergedGroup_ListsTheFramesThenSaysHeld()
    {
        var first = new FilmstripVisit(1, 0.1, 0.11, 0);
        var held = new FilmstripVisit(2, 0.11, 0.9, 0, IsHold: true);

        FilmstripReadout.At("Once", 3, 0, Cell(2, true, first, held), held).Lines
            .ShouldBe(new[] { "3 / 3", "0.79 s", "at 0.11 s", "merged: 2, 3", "held" });
    }

    // ---- events

    private static EventFrame F(double duration, params string[] names) => new(duration, names);

    private static (string Name, double Time, bool IsReturn)[] Fired(IReadOnlyList<EventOccurrence> occurrences) =>
        occurrences.Select(o => (o.Name, Math.Round(o.Time, 6), o.IsReturn)).ToArray();

    [Fact]
    public void For_ANonLoopingChain_GroupsOnlyTheFirstCycle()
    {
        var frames = new[] { F(0.1, "A"), F(0.2), F(0.1, "B") };

        EventMarkers.For(frames, false, false, played: 1.0, pixelsPerSecond: 100, markerWidth: 9, loop: false).Count.ShouldBe(2);
        EventMarkers.For(frames, false, false, played: 1.0, pixelsPerSecond: 100, markerWidth: 9).Count.ShouldBe(5);
    }

    [Fact]
    public void Occurrences_ANonLoopingPingPong_FiresItsThereAndBackOnce_NotAgainInTheHold()
    {
        var frames = new[] { F(0.1, "A"), F(0.1, "B"), F(0.1, "C") };

        var fired = Fired(EventMarkers.Occurrences(frames, true, false, played: 3.0, loop: false));

        // 0 1 2 | 2 1 0: the turnaround adds nothing, the way back mirrors, and the held final frame 0 fires nothing more.
        fired.ShouldBe(new[] { ("A", 0.0, false), ("B", 0.1, false), ("C", 0.2, false), ("B", 0.4, true), ("A", 0.5, true) });
    }

    [Fact]
    public void Occurrences_ANonLoopingReverseChain_FiresLastToFirst_ThenNothingInTheHold()
    {
        var frames = new[] { F(0.1, "A"), F(0.1, "B"), F(0.1, "C") };

        Fired(EventMarkers.Occurrences(frames, false, true, played: 2.0, loop: false)).Select(f => f.Name).ShouldBe(new[] { "C", "B", "A" });
    }

    [Fact]
    public void Occurrences_ANonLoopingOneFrameChain_FiresOnceAtTheStart_WhereALoopingOneFiresEveryCycle()
    {
        var frames = new[] { F(0.5, "Boom") };

        Fired(EventMarkers.Occurrences(frames, false, false, played: 2.0, loop: false)).ShouldBe(new[] { ("Boom", 0.0, false) });
        EventMarkers.Occurrences(frames, false, false, played: 2.0, loop: true).Count.ShouldBe(4);
    }

    // ---- the edge drag magnets

    private static SequenceChainReferenceSave Ref(string chain, double start, float loops = 1) =>
        new() { Source = "hero.achx", Chain = chain, Start = start, Loops = loops };

    [Fact]
    public void ResizeEdge_OfAChainThatDoesNotRepeatAndStartsLater_StillOffersFrameBoundariesInItsFirstCycleOnly()
    {
        var walk = Ref("Walk", 2);
        var sequence = new SequenceSave();
        sequence.Tracks.Add(new SequenceTrackSave { Chains = { walk } });
        var blocks = TimelineLayout.Blocks(sequence, _ => 0.5);
        var boundaries = new[] { 0.1, 0.2, 0.3, 0.4 };
        var others = TimelineSnapTargets.Others(blocks, walk, 99);

        var firstCycle = TimelineSnapTargets.ResizeEdge(blocks[0], 2.3 + 3 / 1500.0, others, boundaries, 1500, false, repeats: false);
        var held = TimelineSnapTargets.ResizeEdge(blocks[0], 2.6 + 3 / 1500.0, others, boundaries, 1500, false, repeats: false);

        firstCycle.Snap.Target!.Value.Kind.ShouldBe(SnapKind.FrameBoundary);
        firstCycle.Snap.Time.ShouldBe(2.3, 1e-9);
        held.Snap.Target.ShouldBeNull();
    }

    [Fact]
    public void ResizeEdge_OfAChainThatDoesNotRepeat_StillSnapsToItsWholeCyclesInTheHold()
    {
        var walk = Ref("Walk", 0);
        var sequence = new SequenceSave();
        sequence.Tracks.Add(new SequenceTrackSave { Chains = { walk } });
        var blocks = TimelineLayout.Blocks(sequence, _ => 0.5);
        var others = TimelineSnapTargets.Others(blocks, walk, 99);

        var result = TimelineSnapTargets.ResizeEdge(blocks[0], 1.5 + 2 / 1500.0, others, new[] { 0.1, 0.2 }, 1500, false, repeats: false);

        result.Snap.Target!.Value.Kind.ShouldBe(SnapKind.OwnCycle);
        result.Loops.ShouldBe(3f, 1e-4f);
    }

    // ---- the scene

    // Once: three 0.1 s frames, Loop off. Spin: the same with Loop on. Back: Once reversed. BackBounce: Back with ping-pong.
    private static SequenceDocument Load(TestHelpers.TempDir dir, params SequenceTrackSave[] tracks)
    {
        var list = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        list.AnimationChains.Add(Chain("Once", loop: false));
        list.AnimationChains.Add(Chain("Spin", loop: true));
        list.AnimationChains.Add(Chain("Back", loop: false, reverse: true));
        list.AnimationChains.Add(Chain("BackBounce", loop: false, reverse: true, pingPong: true));
        list.SaveJson(Path.Combine(dir.Path, "hero.achj"));

        var save = new SequenceListSave { Sources = { "hero.achj" } };
        var sequence = new SequenceSave { Name = "Attack" };
        sequence.Tracks.AddRange(tracks);
        save.Sequences.Add(sequence);
        string path = Path.Combine(dir.Path, "boss.seqj");
        save.Save(path);
        return SequenceDocument.Load(path);
    }

    private static AnimationChainSave Chain(string name, bool loop, bool reverse = false, bool pingPong = false)
    {
        var chain = new AnimationChainSave { Name = name, Loop = loop, Reverse = reverse, PingPong = pingPong };
        for (int i = 0; i < 3; i++)
            chain.Frames.Add(new AnimationFrameSave { TextureName = "t.png", FrameLength = 0.1f, RightCoordinate = 1, BottomCoordinate = 1 });
        return chain;
    }

    private static SequenceTrackSave Track(params (string Chain, double Start, float Loops)[] chains)
    {
        var track = new SequenceTrackSave { Name = "Body" };
        foreach (var (chain, start, loops) in chains)
            track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achj", Chain = chain, Start = start, Loops = loops });
        return track;
    }

    [Fact]
    public void At_ANonLoopingChainWithLessThanOneCycle_PlaysThatPartAndNeverHolds()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, Track(("Once", 0, 0.5f)));

        // The block is 0.15 s: frame 1 is showing at 0.12 s, and at 0.16 s the block is over rather than holding frame 1.
        SequenceScene.At(document, 0, 0.12).Single().FrameIndex.ShouldBe(1);
        SequenceScene.At(document, 0, 0.16).ShouldBeEmpty();
    }

    [Fact]
    public void At_AHeldChainCutByTheNextOne_HoldsUntilTheNextStarts_ThenYieldsToIt()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, Track(("Once", 0, 3), ("Spin", 0.5, 1)));

        var held = SequenceScene.At(document, 0, 0.45).Single();
        held.Chain.Name.ShouldBe("Once");
        held.FrameIndex.ShouldBe(2);
        var next = SequenceScene.At(document, 0, 0.5).Single();
        next.Chain.Name.ShouldBe("Spin");
        next.FrameIndex.ShouldBe(0);
    }

    [Fact]
    public void At_TwoChainsStartingTogether_TheEarlierOneInTheListNeverHoldsEvenWhenItDoesNotLoop()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, Track(("Once", 0, 3), ("Spin", 0, 1)));

        var sprite = SequenceScene.At(document, 0, 0.05).Single();
        sprite.Chain.Name.ShouldBe("Spin");
    }

    [Fact]
    public void At_ANonLoopingReverseChain_HoldsFrameZero_WhereItsCycleEnds()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, Track(("Back", 0, 2)));

        SequenceScene.At(document, 0, 0.05).Single().FrameIndex.ShouldBe(2);
        SequenceScene.At(document, 0, 0.25).Single().FrameIndex.ShouldBe(0);
        var held = SequenceScene.At(document, 0, 0.45).Single();
        held.FrameIndex.ShouldBe(0);
        held.ElapsedInFrame.ShouldBe(0, 1e-6);
        SequenceScene.At(document, 0, 0.61).ShouldBeEmpty();
    }

    [Fact]
    public void At_ANonLoopingReversePingPong_HoldsTheLastFrame_WhereItsCycleEnds()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, Track(("BackBounce", 0, 2)));

        // A reversed ping-pong runs 2 1 0 | 0 1 2 (0.6 s), so the hold is frame 2 at its full length.
        SequenceScene.At(document, 0, 0.55).Single().FrameIndex.ShouldBe(2);
        var held = SequenceScene.At(document, 0, 1.1).Single();
        held.FrameIndex.ShouldBe(2);
        held.ElapsedInFrame.ShouldBe(0.1, 1e-6);
    }

    [Fact]
    public void At_AChainWhoseLoopFlagIsTurnedOffUnderAnOpenSequence_HoldsFromThenOn()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, Track(("Spin", 0, 3)));
        var spin = document.FindChain(document.Save.Sequences[0].Tracks[0].Chains[0])!;

        SequenceScene.At(document, 0, 0.35).Single().FrameIndex.ShouldBe(0);
        spin.Loop = false;
        SequenceScene.At(document, 0, 0.35).Single().FrameIndex.ShouldBe(2);
        spin.Loop = true;
        SequenceScene.At(document, 0, 0.35).Single().FrameIndex.ShouldBe(0);
    }

    [Fact]
    public void At_ANonLoopingChainOfAThousandMillionCycles_HoldsItsLastFrameDeepIntoTheBlock()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, Track(("Once", 0, 1e9f)));

        SequenceScene.At(document, 0, 5e7).Single().FrameIndex.ShouldBe(2);
        SequenceScene.At(document, 0, 5e7).Single().ElapsedInFrame.ShouldBe(0.1, 1e-6);
    }
}
