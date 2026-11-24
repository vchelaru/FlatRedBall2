using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>How reference tracks are laid out and hit on the timeline.</summary>
public class TimelineReferencesTests
{
    private static SequenceTrackSave ChainTrack(string name, double start = 0) =>
        new() { Name = name, Chains = { new SequenceChainReferenceSave { Source = "a.achx", Chain = name, Start = start } } };

    private static SequenceTrackSave RefTrack(string name, string target, double start = 0, double? length = null) =>
        new() { Name = name, References = [new SequenceReferenceSave { Sequence = target, Start = start, Length = length }] };

    private static List<SequenceSave> File() => new()
    {
        new SequenceSave { Name = "Idle", Tracks = { ChainTrack("Body"), RefTrack("Earth", "Earth", start: 1, length: 2.5) } },
        new SequenceSave { Name = "Earth", Tracks = { ChainTrack("Ring"), ChainTrack("Dust", start: 0.5) } },
    };

    // Every chain cycle is 0.75 s.
    private static double? Cycle(SequenceChainReferenceSave _) => 0.75;

    [Fact]
    public void For_TwoClipsOnOneTrack_NextClipCutsThePreviousOne_AndOnlyTheFirstListsRows()
    {
        var file = File();
        file[0].Tracks[1].References!.Add(new SequenceReferenceSave { Sequence = "Earth", Start = 2 });

        var clips = TimelineReferences.For(file[0], file, Cycle);

        clips.Count.ShouldBe(2);
        clips[0].End.ShouldBe(2, 1e-9);           // 1 .. 3.5 cut at the second clip's start
        clips[1].Start.ShouldBe(2);
        clips[0].Rows.Count.ShouldBeGreaterThan(0);
        clips[1].Rows.ShouldBeEmpty();
    }

    [Fact]
    public void For_ReferenceTrack_ClipIsItsLength()
    {
        var file = File();

        var reference = TimelineReferences.For(file[0], file, Cycle).Single();

        reference.Track.ShouldBe(1);
        reference.TargetLength.ShouldBe(1.25, 1e-9);   // Dust ends last: 0.5 + 0.75
        reference.Start.ShouldBe(1);
        reference.End.ShouldBe(1 + 2 * 1.25, 1e-9);
    }

    [Fact]
    public void For_ReferenceTrack_ListsTheInheritedTracksWithTheirSpans()
    {
        var file = File();

        var rows = TimelineReferences.For(file[0], file, Cycle).Single().Rows;

        rows.Select(r => r.Name).ShouldBe(new[] { "Ring", "Dust" });
        rows[1].Spans.Single().Start.ShouldBe(0.5);
        rows[1].Spans.Single().Length.ShouldBe(0.75, 1e-9);
    }

    [Fact]
    public void For_MissingTarget_IsDrawnAtTheMissingLength_AndCannotBeResized()
    {
        var file = File();
        file[0].Tracks[1].References![0].Sequence = "Gone";

        var reference = TimelineReferences.For(file[0], file, Cycle).Single();

        reference.IsMissing.ShouldBeTrue();
        reference.Length.ShouldBe(TimelineLayout.MissingLength);
        reference.CanResize.ShouldBeFalse();
    }

    [Fact]
    public void LaneHeights_ExpandedReference_GrowsByItsRows_CollapsedStaysBase()
    {
        var file = File();
        var references = TimelineReferences.For(file[0], file, Cycle);

        var collapsed = TimelineLayout.LaneHeights(new List<TimelineBlock>(), 2, _ => false, references, _ => false);
        var expanded = TimelineLayout.LaneHeights(new List<TimelineBlock>(), 2, _ => false, references, _ => true);

        collapsed.ShouldBe(new[] { TimelineViewport.LaneHeight, TimelineViewport.LaneHeight });
        expanded[0].ShouldBe(TimelineViewport.LaneHeight);
        expanded[1].ShouldBe(TimelineViewport.LaneHeight + 2 * TimelineReferences.InheritedRowHeight + 2, 1e-9);
    }

    [Fact]
    public void HitTest_ClipBody_ThenItsRightEdge_AndNotTheInheritedRowsBelow()
    {
        var file = File();
        var references = TimelineReferences.For(file[0], file, Cycle);
        var viewport = new TimelineViewport();
        viewport.SetLaneHeights(TimelineLayout.LaneHeights(new List<TimelineBlock>(), 2, _ => false, references, _ => true));
        double lane = viewport.LaneTop(1);
        double mid = viewport.XAt(2);
        double end = viewport.XAt(references[0].End);

        TimelineReferences.HitTest(viewport, references, 2, mid, lane + 20)!.Value.Part.ShouldBe(TimelineHitPart.Body);
        TimelineReferences.HitTest(viewport, references, 2, end - 1, lane + 20)!.Value.Part.ShouldBe(TimelineHitPart.Edge);
        TimelineReferences.HitTest(viewport, references, 2, mid, lane + TimelineReferences.InheritedTop + 4).ShouldBeNull();
        TimelineReferences.HitTest(viewport, references, 2, viewport.XAt(0.2), lane + 20).ShouldBeNull();
    }

    [Fact]
    public void For_NestedReferenceInTheTarget_IsOnePrefixedRowPerTrack_SpanningItsClip()
    {
        var file = new List<SequenceSave>
        {
            new() { Name = "Idle", Tracks = { RefTrack("Mid", "Mid") } },
            new() { Name = "Mid", Tracks = { ChainTrack("Own"), RefTrack("Fx", "Earth", start: 0.5, length: 1.5) } },
            new() { Name = "Earth", Tracks = { ChainTrack("Ring") } },
        };

        var rows = TimelineReferences.For(file[0], file, Cycle).Single().Rows;

        rows.Select(r => r.Name).ShouldBe(new[] { "Own", "Fx/Ring" });
        rows[1].Spans.Single().Start.ShouldBe(0.5);
        rows[1].Spans.Single().Length.ShouldBe(1.5, 1e-9);
    }

    [Fact]
    public void For_SelfReference_IsBroken_NamesTheCycle_AndCannotBeResizedOrExpanded()
    {
        var file = new List<SequenceSave> { new() { Name = "A", Tracks = { ChainTrack("Body"), RefTrack("Me", "A") } } };

        var reference = TimelineReferences.For(file[0], file, Cycle).Single();

        reference.IsBroken.ShouldBeTrue();
        reference.IsMissing.ShouldBeFalse();
        reference.Cycle.ShouldBe(new[] { "A", "A" });
        reference.CanResize.ShouldBeFalse();
        reference.Expandable.ShouldBeFalse();
        reference.Length.ShouldBe(TimelineLayout.MissingLength);
    }

    [Fact]
    public void For_EmptyTarget_IsNotBroken_ButHasNoCycleToResize()
    {
        var file = new List<SequenceSave>
        {
            new() { Name = "Idle", Tracks = { RefTrack("Nothing", "Empty", length: 3) } },
            new() { Name = "Empty" },
        };

        var reference = TimelineReferences.For(file[0], file, Cycle).Single();

        reference.IsBroken.ShouldBeFalse();
        reference.TargetLength.ShouldBe(0);
        reference.Length.ShouldBe(TimelineLayout.MissingLength);
        reference.CanResize.ShouldBeFalse();
        reference.Expandable.ShouldBeFalse();
    }

    [Fact]
    public void For_HiddenTrackInTheTarget_ItsRowIsNotVisible()
    {
        var file = File();
        file[1].Tracks[1].Visible = false;

        var rows = TimelineReferences.For(file[0], file, Cycle).Single().Rows;

        rows.Select(r => r.Visible).ShouldBe(new[] { true, false });
    }

    [Fact]
    public void For_LengthShorterThanTheTarget_ClipIsThatPartOfTheTarget()
    {
        var file = File();
        file[0].Tracks[1].References![0].Length = 0.625;

        TimelineReferences.For(file[0], file, Cycle).Single().Length.ShouldBe(0.625, 1e-9);
    }

    [Fact]
    public void For_SeveralReferenceTracks_OnePerLaneWithItsIndex()
    {
        var file = File();
        file[0].Tracks.Add(RefTrack("Gone", "Missing"));
        file[0].Tracks.Insert(0, RefTrack("Again", "Earth"));

        var references = TimelineReferences.For(file[0], file, Cycle);

        references.Select(r => (r.Track, r.IsMissing)).ShouldBe(new[] { (0, false), (2, false), (3, true) });
    }

    [Fact]
    public void HitTest_BrokenClip_IsBodyAtItsRightEdge_NeverTheResizeEdge()
    {
        var file = new List<SequenceSave> { new() { Name = "Idle", Tracks = { ChainTrack("Body"), RefTrack("Gone", "Missing", start: 1) } } };
        var references = TimelineReferences.For(file[0], file, Cycle);
        var viewport = new TimelineViewport();
        viewport.SetLaneHeights(TimelineLayout.LaneHeights(new List<TimelineBlock>(), 2, _ => false, references, _ => true));
        double lane = viewport.LaneTop(1);

        TimelineReferences.HitTest(viewport, references, 2, viewport.XAt(references[0].End) - 1, lane + 20)!.Value.Part.ShouldBe(TimelineHitPart.Body);
    }
}
