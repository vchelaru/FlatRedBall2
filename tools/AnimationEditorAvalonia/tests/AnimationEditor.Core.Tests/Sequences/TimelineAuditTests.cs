using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>Hit-testing priority, viewport clamps, clock edges and title fallbacks the first round left unpinned.</summary>
public class TimelineAuditTests
{
    private static SequenceChainReferenceSave Ref(string chain, double start, float loops = 1) =>
        new() { Source = "hero.achx", Chain = chain, Start = start, Loops = loops };

    [Fact]
    public void HitTest_WhereOneChainEndsAndTheNextStarts_TheLaterChainWins()
    {
        // First plays 0..1 s and Second starts at 1 s: the shared pixel grabs Second's body, not First's resize edge.
        var track = new SequenceTrackSave { Chains = { Ref("First", 0), Ref("Second", 1) } };
        var blocks = TimelineLayout.Blocks(new SequenceSave { Tracks = { track } }, _ => 1.0);
        var viewport = new TimelineViewport();
        double bodyY = viewport.LaneTop(0) + TimelineLayout.BodyTop + 2;

        var hit = TimelineLayout.HitTest(viewport, blocks, 1, viewport.XAt(1), bodyY)!.Value;

        hit.Block.Reference.Chain.ShouldBe("Second");
        hit.Part.ShouldBe(TimelineHitPart.Body);
    }

    [Fact]
    public void HitTest_ACutChainsBodyEnd_IsNotAResizeEdge_ButItsTailEndIs()
    {
        // Walk asks for 3 s and is cut at 2 s by Run: resizing from the cut point would mean nothing.
        var track = new SequenceTrackSave { Chains = { Ref("Walk", 0, loops: 3), Ref("Run", 2) } };
        var blocks = TimelineLayout.Blocks(new SequenceSave { Tracks = { track } }, _ => 1.0);
        var viewport = new TimelineViewport();
        double bodyY = viewport.LaneTop(0) + TimelineLayout.BodyTop + 2;
        double tailY = viewport.LaneTop(0) + TimelineLayout.TailTop + 2;

        // Just before 2 s in the body band is Walk's body, not an edge.
        var cutPoint = TimelineLayout.HitTest(viewport, blocks, 1, viewport.XAt(2) - 2, bodyY)!.Value;
        cutPoint.Block.Reference.Chain.ShouldBe("Walk");
        cutPoint.Part.ShouldBe(TimelineHitPart.Body);
        TimelineLayout.HitTest(viewport, blocks, 1, viewport.XAt(3), tailY)!.Value.Part.ShouldBe(TimelineHitPart.Edge);
    }

    [Fact]
    public void ZoomAround_PastEitherLimit_StaysWithinTheLimits()
    {
        var viewport = new TimelineViewport();

        viewport.ZoomAround(TimelineViewport.HeaderWidth + 50, 1_000_000);
        viewport.PixelsPerSecond.ShouldBe(TimelineViewport.MaxPixelsPerSecond);
        viewport.ZoomAround(TimelineViewport.HeaderWidth + 50, 1e-9);
        viewport.PixelsPerSecond.ShouldBe(TimelineViewport.MinPixelsPerSecond);
    }

    [Fact]
    public void ScrollHorizontally_BeforeTheStart_StopsAtZero()
    {
        var viewport = new TimelineViewport();
        viewport.ScrollHorizontally(240);

        viewport.ScrollHorizontally(-100_000);

        viewport.ScrollSeconds.ShouldBe(0);
    }

    [Fact]
    public void ScrollVertically_IsKeptWithinTheContent_AndNothingScrollsWhenEverythingFits()
    {
        var viewport = new TimelineViewport();
        double content = viewport.ContentHeight(trackCount: 10);

        viewport.ScrollVertically(1_000_000, trackCount: 10, viewHeight: 200);
        viewport.ScrollY.ShouldBe(content - 200);
        viewport.ScrollVertically(-1_000_000, trackCount: 10, viewHeight: 200);
        viewport.ScrollY.ShouldBe(0);
        viewport.ScrollVertically(500, trackCount: 1, viewHeight: 400);
        viewport.ScrollY.ShouldBe(0);
    }

    [Fact]
    public void Advance_WhilePaused_DoesNotMoveThePlayhead()
    {
        var clock = new SequenceClock();
        clock.Seek(1, length: 4);

        clock.Advance(0.5, length: 4, loop: true);

        clock.Time.ShouldBe(1);
        clock.IsPlaying.ShouldBeFalse();
    }

    [Theory]
    [InlineData("chars/..", "chars/..")]
    [InlineData("chars/.", "chars/.")]
    public void Format_ADotSegmentIsNotAFileName(string source, string expected) =>
        SourceTitle.Format(source).ShouldBe(expected);
}
