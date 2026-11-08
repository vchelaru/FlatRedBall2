using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

/// <summary>Second audit of the unified snap: the ties and the offered marks the first tests left unpinned.</summary>
public class TimelineSnapAudit2Tests
{
    private static SnapTarget T(double time, SnapKind kind = SnapKind.ChainEdge) => new(time, kind);

    [Fact]
    public void Move_StartAndEndEquallyFarFromTheirTargets_TheStartSnapWins()
    {
        // 128 px/s: reach 0.0625 s. Dragged start 1.0625 is 0.0625 from the target at 1.0; its end (length 1) at 2.0625 is 0.0625 from 2.125.
        var targets = new[] { T(1.0), T(2.125) };

        var result = TimelineSnap.Move(1.0625, 1.0, targets, 128, bypass: false);

        result.Time.ShouldBe(1.0);
        result.Target.ShouldBe(T(1.0));
    }

    [Fact]
    public void CycleMarks_NearTheChainsOwnStart_NeverOffersTheStartItself()
    {
        // The first whole-cycle mark is one cycle after the start; the start is where the chain already begins.
        var marks = TimelineSnap.CycleMarks(start: 2, cycleLength: 1, time: 2.1, pixelsPerSecond: 100);

        marks.Select(mark => mark.Time).ShouldBe(new[] { 3.0 });
    }
}
