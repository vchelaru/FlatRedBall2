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

    private static SequenceTrackSave RefTrack(string name, string target, double start = 0, float loops = 1) =>
        new() { Name = name, Reference = new SequenceReferenceSave { Sequence = target, Start = start, Loops = loops } };

    private static List<SequenceSave> File() => new()
    {
        new SequenceSave { Name = "Idle", Tracks = { ChainTrack("Body"), RefTrack("Earth", "Earth", start: 1, loops: 2) } },
        new SequenceSave { Name = "Earth", Tracks = { ChainTrack("Ring"), ChainTrack("Dust", start: 0.5) } },
    };

    // Every chain cycle is 0.75 s.
    private static double? Cycle(SequenceChainReferenceSave _) => 0.75;

    [Fact]
    public void For_ReferenceTrack_ClipIsLoopsCyclesOfTheTargetsLength()
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
        file[0].Tracks[1].Reference!.Sequence = "Gone";

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
}
