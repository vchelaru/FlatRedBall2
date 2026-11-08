using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

public class PlayheadStepsTests
{
    private static AnimationChainSave ChainOf(string name, bool loop, params float[] lengths)
    {
        var chain = new AnimationChainSave { Name = name, Loop = loop };
        foreach (float length in lengths) chain.Frames.Add(new AnimationFrameSave { FrameLength = length });
        return chain;
    }

    private static readonly AnimationChainSave Walk = ChainOf("Walk", false, 0.1f, 0.1f, 0.1f, 0.1f, 0.1f);
    private static readonly AnimationChainSave Run = ChainOf("Run", false, 0.1f, 0.1f);
    private static readonly AnimationChainSave Spin = ChainOf("Spin", true, 0.1f, 0.2f);

    private static SequenceChainReferenceSave Ref(string chain, double start, float loops = 1) =>
        new() { Source = "hero.achx", Chain = chain, Start = start, Loops = loops };

    private static AnimationChainSave? Find(SequenceChainReferenceSave reference) =>
        reference.Chain switch { "Walk" => Walk, "Run" => Run, "Spin" => Spin, _ => null };

    private static double? Cycle(SequenceChainReferenceSave reference) =>
        Find(reference) is { } chain ? chain.Frames.Sum(frame => (double)frame.FrameLength) : null;

    private static IReadOnlyList<double> Boundaries(params SequenceTrackSave[] tracks)
    {
        var sequence = new SequenceSave();
        sequence.Tracks.AddRange(tracks);
        var blocks = TimelineLayout.Blocks(sequence, Cycle);
        double length = SequenceTimeline.Length(sequence, reference => Cycle(reference) ?? 0);
        return PlayheadSteps.Boundaries(sequence, blocks, Find, length).Select(time => Math.Round(time, 4)).ToList();
    }

    private static SequenceTrackSave Track(bool visible, params SequenceChainReferenceSave[] chains) =>
        new() { Visible = visible, Chains = chains.ToList() };

    [Fact]
    public void Boundaries_UnionTheFrameStartsAndChainEdgesOfEveryTrack()
    {
        // Walk 0..0.5 on one track, Run 0.25..0.45 on another.
        Boundaries(Track(true, Ref("Walk", 0)), Track(true, Ref("Run", 0.25)))
            .ShouldBe(new[] { 0, 0.1, 0.2, 0.25, 0.3, 0.35, 0.4, 0.45, 0.5 });
    }

    [Fact]
    public void Boundaries_LeaveOutAHiddenTrack_AndAMissingChain()
    {
        Boundaries(Track(true, Ref("Walk", 0)), Track(false, Ref("Run", 0.25)), Track(true, Ref("Gone", 0.05)))
            .ShouldBe(new[] { 0, 0.1, 0.2, 0.3, 0.4, 0.5 });
    }

    [Fact]
    public void Boundaries_OfALoopingChain_RepeatEveryCycle_AndOfANonLoopingOne_OnlyTheFirst()
    {
        // Spin (0.1 + 0.2 = 0.3 per cycle) asked for 2 cycles: cycle starts at 0 and 0.3, cuts at 0.1 and 0.4.
        Boundaries(Track(true, Ref("Spin", 0, loops: 2))).ShouldBe(new[] { 0, 0.1, 0.3, 0.4, 0.6 });
        // Run does not repeat: 3 cycles is one cycle of frames and then a held frame to 0.6.
        Boundaries(Track(true, Ref("Run", 0, loops: 3))).ShouldBe(new[] { 0, 0.1, 0.6 });
    }

    [Fact]
    public void Boundaries_StopWhereAnotherChainCutsTheChain()
    {
        // Walk would run to 0.5 but Run starts at 0.25 on the same track, so Walk is cut there.
        Boundaries(Track(true, Ref("Walk", 0), Ref("Run", 0.25)))
            .ShouldBe(new[] { 0, 0.1, 0.2, 0.25, 0.35, 0.45 });
    }

    [Fact]
    public void Boundaries_OfAnEmptySequence_AreJustZero()
    {
        Boundaries().ShouldBe(new[] { 0.0 });
    }

    [Fact]
    public void Next_GoesToTheFollowingBoundary_FromOnTheOneBefore_OrFromBetweenTwo_AndStaysAtTheEnd()
    {
        var steps = new[] { 0, 0.1, 0.2, 0.5 };
        PlayheadSteps.Next(steps, 0).ShouldBe(0.1);
        PlayheadSteps.Next(steps, 0.1).ShouldBe(0.2);
        PlayheadSteps.Next(steps, 0.15).ShouldBe(0.2);
        PlayheadSteps.Next(steps, 0.2).ShouldBe(0.5);
        PlayheadSteps.Next(steps, 0.5).ShouldBe(0.5);
    }

    [Fact]
    public void Previous_GoesToThePrecedingBoundary_FromOnTheOneAfter_OrFromBetweenTwo_AndStaysAtZero()
    {
        var steps = new[] { 0, 0.1, 0.2, 0.5 };
        PlayheadSteps.Previous(steps, 0.5).ShouldBe(0.2);
        PlayheadSteps.Previous(steps, 0.2).ShouldBe(0.1);
        PlayheadSteps.Previous(steps, 0.15).ShouldBe(0.1);
        PlayheadSteps.Previous(steps, 0.1).ShouldBe(0);
        PlayheadSteps.Previous(steps, 0).ShouldBe(0);
    }

    [Fact]
    public void Steps_IgnoreFloatNoise_SoAFrameBoundaryReachedBySumsIsNotSteppedOntoTwice()
    {
        // 0.1 + 0.2 is 0.30000000000000004: standing on it, Next must move on, not stay.
        var steps = new[] { 0, 0.1, 0.3, 0.6 };
        PlayheadSteps.Next(steps, 0.1 + 0.2).ShouldBe(0.6);
        PlayheadSteps.Previous(steps, 0.1 + 0.2).ShouldBe(0.1);
    }
}
