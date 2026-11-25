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

    private static SequenceChainReferenceSave Ref(string chain, double start, double? length = null) =>
        new() { Source = "hero.achx", Chain = chain, Start = start, Length = length };

    private static AnimationChainSave? Find(SequenceChainReferenceSave reference) =>
        reference.Chain switch { "Walk" => Walk, "Run" => Run, "Spin" => Spin, _ => null };

    private static IReadOnlyList<double> Boundaries(params SequenceTrackSave[] tracks)
    {
        var sequence = new SequenceSave();
        sequence.Tracks.AddRange(tracks);
        return FileBoundaries(sequence);
    }

    // Of the first sequence of the file.
    private static IReadOnlyList<double> FileBoundaries(params SequenceSave[] file) =>
        PlayheadSteps.Boundaries(file, file[0], Find, _ => AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds).Select(time => Math.Round(time, 4)).ToList();

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
        Boundaries(Track(true, Ref("Spin", 0, length: 0.6))).ShouldBe(new[] { 0, 0.1, 0.3, 0.4, 0.6 });
        // Run does not repeat: 3 cycles is one cycle of frames and then a held frame to 0.6.
        Boundaries(Track(true, Ref("Run", 0, length: 0.6))).ShouldBe(new[] { 0, 0.1, 0.6 });
    }

    [Fact]
    public void Boundaries_StopWhereAnotherChainCutsTheChain()
    {
        // Walk would run to 0.5 but Run starts at 0.25 on the same track, so Walk is cut there.
        Boundaries(Track(true, Ref("Walk", 0), Ref("Run", 0.25)))
            .ShouldBe(new[] { 0, 0.1, 0.2, 0.25, 0.35, 0.45 });
    }

    private static SequenceSave Sequence(string name, bool loop, params SequenceTrackSave[] tracks)
    {
        var sequence = new SequenceSave { Name = name, Loop = loop };
        sequence.Tracks.AddRange(tracks);
        return sequence;
    }

    private static SequenceTrackSave Clips(bool visible, params SequenceReferenceSave[] clips) => new() { Visible = visible, References = [.. clips] };

    private static SequenceReferenceSave Clip(string target, double start, double? length = null) => new() { Sequence = target, Start = start, Length = length };

    [Fact]
    public void Boundaries_OfAClip_AreItsEdgesAndTheFramesOfTheSequenceItPlays_CutByTheNextClip()
    {
        // Run is 0.1 + 0.1. The first clip would last 0.2 but the next starts 0.15 after it.
        FileBoundaries(
            Sequence("Host", true, Clips(true, Clip("A", 1), Clip("A", 1.15))),
            Sequence("A", true, Track(true, Ref("Run", 0)), Track(false, Ref("Walk", 0, length: 0.2))))
            .ShouldBe(new[] { 0, 1, 1.1, 1.15, 1.25, 1.35 });
    }

    [Theory]
    [InlineData(true, new[] { 0, 0.1, 0.2, 0.3, 0.4, 0.5 })]
    [InlineData(false, new[] { 0, 0.1, 0.5 })] // holds its last frame
    public void Boundaries_OfALongClip_RepeatWhenItsTargetLoops(bool loop, double[] expected)
    {
        FileBoundaries(
            Sequence("Host", true, Clips(true, Clip("A", 0, length: 0.5))),
            Sequence("A", loop, Track(true, Ref("Run", 0))))
            .ShouldBe(expected);
    }

    [Fact]
    public void Boundaries_GoThroughASequencePlayedByAPlayedSequence()
    {
        FileBoundaries(
            Sequence("Host", true, Clips(true, Clip("Outer", 1))),
            Sequence("Outer", true, Clips(true, Clip("Inner", 0.5))),
            Sequence("Inner", true, Track(true, Ref("Run", 0.25))))
            .ShouldBe(new[] { 0, 1, 1.5, 1.75, 1.85, 1.95 });
    }

    [Fact]
    public void Boundaries_LeaveOutAHiddenClipTrack_AMissingTarget_AndAClipThatPlaysItsHolderBack()
    {
        FileBoundaries(
            Sequence("Host", true, Track(true, Ref("Run", 0)), Clips(false, Clip("A", 0.05)), Clips(true, Clip("Gone", 0.05), Clip("Host", 0.15))),
            Sequence("A", true, Track(true, Ref("Walk", 0, length: 0.03))))
            .ShouldBe(new[] { 0, 0.1, 0.2 });
    }

    [Fact]
    public void Boundaries_ThatOnlyDifferByFloatErrorLateInTheSequence_AreOne()
    {
        // Spin's frames are floats: its boundary at 400 s adds up to a few microseconds past the start of Run.
        Boundaries(Track(true, Ref("Spin", 0, length: 500)), Track(true, Ref("Run", 400)))
            .Count(time => Math.Abs(time - 400) < 0.01).ShouldBe(1);
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
        // Late in a sequence the sums are further off.
        var late = new[] { 400, 400.1, 400.2 };
        PlayheadSteps.Next(late, 400.100006 - 0.00001).ShouldBe(400.2);
        PlayheadSteps.Previous(late, 400.100006).ShouldBe(400);
    }
}
