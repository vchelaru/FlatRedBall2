using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>
/// A rubber band dragged over many chains. Each pointer move recomputes what the band touches; the selection (and so every
/// listener) must change only when that set does, and a move must not cost more than the chains it can touch.
/// </summary>
public class TimelineMarqueeSelectorTests
{
    // Tracks of `perTrack` chains each, 0.25 s apart (0.2 s long, so none is cut): 0.00-0.20, 0.25-0.45, ...
    private static (TimelineViewport Viewport, IReadOnlyList<TimelineBlock> Blocks, int[] Starts) Big(int tracks, int perTrack)
    {
        var sequence = new SequenceSave();
        for (int t = 0; t < tracks; t++)
        {
            var track = new SequenceTrackSave { Name = "T" + t };
            // Listed out of time order, so the block order is the playback order and not the list order.
            for (int i = perTrack - 1; i >= 0; i--)
                track.Chains.Add(new SequenceChainReferenceSave { Chain = "Walk", Start = i * 0.25 });
            sequence.Tracks.Add(track);
        }
        var blocks = TimelineLayout.Blocks(sequence, _ => 0.2);
        return (new TimelineViewport(), blocks, TimelineLayout.TrackStarts(blocks, tracks));
    }

    private static double Body(TimelineViewport viewport, int lane) =>
        viewport.LaneTop(lane) + (TimelineLayout.BodyTop + TimelineLayout.BodyBottom) / 2;

    [Fact]
    public void TrackStarts_GivesWhereEachTracksBlocksBegin_AndTheTotalLast()
    {
        var sequence = new SequenceSave();
        sequence.Tracks.Add(new SequenceTrackSave { Chains = { new() { Start = 0 }, new() { Start = 1 } } });
        sequence.Tracks.Add(new SequenceTrackSave());
        sequence.Tracks.Add(new SequenceTrackSave { Chains = { new() { Start = 0 } } });
        var blocks = TimelineLayout.Blocks(sequence, _ => 0.5);

        TimelineLayout.TrackStarts(blocks, 3).ShouldBe(new[] { 0, 2, 2, 3 });
    }

    [Fact]
    public void Update_FindsTheSameChainsAsTheBruteForceHitTest_OnEveryTrack()
    {
        var (viewport, blocks, starts) = Big(tracks: 4, perTrack: 40);
        var selection = new TimelineSelection();
        var selector = new TimelineMarqueeSelector();
        selector.Begin(System.Array.Empty<TimelineChainId>());
        double x1 = viewport.XAt(2.1), x2 = viewport.XAt(6.3), y1 = Body(viewport, 1), y2 = Body(viewport, 3);

        selector.Update(selection, viewport, blocks, starts, x1, y1, x2, y2).ShouldBeTrue();

        selection.Chains.ShouldBe(TimelineMarquee.Chains(viewport, blocks, x1, y1, x2, y2).OrderBy(id => id.Track).ThenBy(id => id.Chain));
        selection.Count.ShouldBeGreaterThan(30);
    }

    [Fact]
    public void Update_ReportsAChangeOnlyWhenTheTouchedSetChanges_NeverForAMoveThatTouchesTheSame()
    {
        var (viewport, blocks, starts) = Big(tracks: 3, perTrack: 200);
        var selection = new TimelineSelection();
        var selector = new TimelineMarqueeSelector();
        selector.Begin(System.Array.Empty<TimelineChainId>());
        int changes = 0, moves = 0;
        double top = Body(viewport, 0), bottom = Body(viewport, 2);

        // 400 moves of the right edge, one pixel at a time: many land inside the same chain or the gap after it.
        for (int px = 0; px < 400; px++)
        {
            moves++;
            if (selector.Update(selection, viewport, blocks, starts, viewport.XAt(0) + 1, top, viewport.XAt(0) + 1 + px, bottom)) changes++;
        }

        // Chains are 0.2 s = 24 px long, 30 px apart: each is touched once per 30 px, so at most 14 sets for 400 px.
        changes.ShouldBeLessThanOrEqualTo(15);
        changes.ShouldBeLessThan(moves / 10);
        // The same rectangle again changes nothing.
        selector.Update(selection, viewport, blocks, starts, viewport.XAt(0) + 1, top, viewport.XAt(0) + 400, bottom).ShouldBeFalse();
        selector.Update(selection, viewport, blocks, starts, viewport.XAt(0) + 1, top, viewport.XAt(0) + 400, bottom).ShouldBeFalse();
    }

    [Fact]
    public void Update_WithABase_AddsTheTouchedChainsToIt_AndShrinksBackToTheBaseWhenTheBandLeaves()
    {
        var (viewport, blocks, starts) = Big(tracks: 2, perTrack: 20);
        var selection = new TimelineSelection();
        var held = new[] { new TimelineChainId(1, 15) };
        selection.SelectChains(held);
        var selector = new TimelineMarqueeSelector();
        selector.Begin(held);
        double y = Body(viewport, 0);

        selector.Update(selection, viewport, blocks, starts, viewport.XAt(0.1), y, viewport.XAt(0.6), y).ShouldBeTrue();
        selection.Count.ShouldBe(4);   // the three chains at 0, 0.25 and 0.5 s, and the held one
        selection.Contains(held[0]).ShouldBeTrue();

        selector.Update(selection, viewport, blocks, starts, viewport.XAt(40), y, viewport.XAt(41), y).ShouldBeTrue();
        selection.Chains.ShouldBe(held);
        selector.Update(selection, viewport, blocks, starts, viewport.XAt(40), y, viewport.XAt(41), y).ShouldBeFalse();
    }

    [Fact]
    public void Update_OverThousandsOfChains_OnlyEverVisitsTheOnesInTheRectangle()
    {
        var (viewport, blocks, starts) = Big(tracks: 8, perTrack: 800);
        var selection = new TimelineSelection();
        var selector = new TimelineMarqueeSelector();
        selector.Begin(System.Array.Empty<TimelineChainId>());

        // A band over the first second of one track: 4 chains, found without scanning the other 6400.
        selector.Update(selection, viewport, blocks, starts, viewport.XAt(0.01), Body(viewport, 5), viewport.XAt(0.95), Body(viewport, 5));

        selection.Count.ShouldBe(4);
        selection.Chains.ShouldAllBe(id => id.Track == 5);
        selector.LastExamined.ShouldBeLessThan(40, "binary search on the one track, not a scan of 6400 blocks");
    }

    [Fact]
    public void Throttle_LetsTheFirstChangeThrough_HoldsTheRestWithinTheInterval_AndFlushesWhatWasHeld()
    {
        var throttle = new SelectionChangeThrottle(intervalTicks: 100);

        throttle.Request(0).ShouldBeTrue();
        throttle.Request(10).ShouldBeFalse();
        throttle.Request(99).ShouldBeFalse();
        throttle.Request(100).ShouldBeTrue();
        throttle.Request(150).ShouldBeFalse();
        throttle.Flush().ShouldBeTrue("a change was held back");
        throttle.Flush().ShouldBeFalse("nothing is held now");
    }

    [Fact]
    public void Throttle_AfterAFlush_TheNextDragStartsFresh()
    {
        var throttle = new SelectionChangeThrottle(intervalTicks: 100);
        throttle.Request(0);
        throttle.Request(10);
        throttle.Flush();

        throttle.Reset();

        throttle.Request(20).ShouldBeTrue();
    }
}
