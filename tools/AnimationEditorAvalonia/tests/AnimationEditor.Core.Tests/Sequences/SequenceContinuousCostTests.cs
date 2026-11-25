using AnimationEditor.Core.Rendering;
using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>The cost of a continuous track in a loop that has run for hours, and that its carry is still the pass-by-pass one.</summary>
public class SequenceContinuousCostTests
{
    private const double Tenth = 0.1f; // 0.10000000149: five of them are a hair over 0.5

    private static SequenceChainReferenceSave Block(string chain, double start, double? length = null) =>
        new() { Source = "a", Chain = chain, Start = start, Length = length };

    private static SequenceReferenceSave Clip(string target, double start, double? length = null) =>
        new() { Sequence = target, Start = start, Length = length };

    private static double Cycle(string name) => name == "Walk" ? 5 * Tenth : 3 * Tenth;

    [Fact]
    public void CarryOver_ManyPasses_MeasuresTheBlocksNoMoreThanOnePassDoes()
    {
        var track = new SequenceTrackSave { Continuous = true, Chains = { Block("Walk", 0, 0.5) } };

        // One night of a half-second loop.
        Calls(60_000).ShouldBe(Calls(1));

        int Calls(int pass)
        {
            int calls = 0;
            SequenceTimeline.CarryOver(track, 0.5, pass, _ => { calls++; return 0.5; });
            return calls;
        }
    }

    [Fact]
    public void ReferenceCarryOver_ManyPasses_MeasuresTheClipsNoMoreThanOnePassDoes()
    {
        var track = new SequenceTrackSave { Continuous = true, References = [Clip("Walk", 0, 0.5)] };

        Calls(60_000).ShouldBe(Calls(1));

        int Calls(int pass)
        {
            int calls = 0;
            SequenceTimeline.ReferenceCarryOver(track, 0.5, pass, _ => { calls++; return 0.5; }, _ => true);
            return calls;
        }
    }

    // The block at the wrap: the first one, a later one of the same chain, another chain, nothing; then a first block that starts late.
    public static TheoryData<double, SequenceChainReferenceSave[]> ChainCases => new()
    {
        { 5 * Tenth, [Block("Walk", 0)] },
        { 1.2, [Block("Walk", 0, 1.2)] },
        { 2.5 + Tenth, [Block("Walk", 0), Block("Run", 0.5), Block("Walk", 1.5, 1 + Tenth)] },
        { 1.5, [Block("Walk", 0), Block("Run", 0.5, 1)] },
        { 1.5, [Block("Walk", 0)] },
        { 1.2, [Block("Walk", 0.2, 1)] },
    };

    [Theory]
    [MemberData(nameof(ChainCases))]
    public void CarryOver_AfterManyPasses_IsWhatCarryingPassByPassGives(double length, SequenceChainReferenceSave[] blocks)
    {
        var track = new SequenceTrackSave { Continuous = true };
        track.Chains.AddRange(blocks);
        bool starts = blocks[0].Start == 0;

        AssertSameAsPassByPass(length, 5000, pass => SequenceTimeline.CarryOver(track, length, pass, block => Cycle(block.Chain)), carry =>
        {
            var atWrap = SequenceTimeline.ActiveAt(track, SequenceTimeline.LastInstant(length), block => Cycle(block.Chain), out _);
            if (!starts || atWrap is null || atWrap.Chain != blocks[0].Chain) return 0;
            return (ReferenceEquals(atWrap, blocks[0]) ? carry : 0) + (length - atWrap.Start);
        });
    }

    public static TheoryData<double, SequenceReferenceSave[]> ClipCases => new()
    {
        { 5 * Tenth, [Clip("Walk", 0)] },
        { 1.2, [Clip("Walk", 0, 1.2)] },
        { 2.5 + Tenth, [Clip("Walk", 0), Clip("Run", 0.5), Clip("Walk", 1.5, 1 + Tenth)] },
        { 1.5, [Clip("Walk", 0), Clip("Run", 0.5, 1)] },
        { 1.5, [Clip("Walk", 0)] },
        { 1.2, [Clip("Walk", 0.2, 1)] },
    };

    [Theory]
    [MemberData(nameof(ClipCases))]
    public void ReferenceCarryOver_AfterManyPasses_IsWhatCarryingPassByPassGives(double length, SequenceReferenceSave[] clips)
    {
        var track = new SequenceTrackSave { Continuous = true, References = [.. clips] };
        bool starts = clips[0].Start == 0;

        AssertSameAsPassByPass(length, 5000, pass => SequenceTimeline.ReferenceCarryOver(track, length, pass, clip => Cycle(clip.Sequence), _ => true), carry =>
        {
            var atWrap = SequenceTimeline.ActiveClipAt(track, SequenceTimeline.LastInstant(length), clip => Cycle(clip.Sequence));
            if (!starts || atWrap is null || atWrap.Sequence != clips[0].Sequence) return 0;
            return (ReferenceEquals(atWrap, clips[0]) ? carry : 0) + (length - atWrap.Start);
        });
    }

    // Adds up pass by pass, as a player counting wraps would. The sum gathers rounding error the product does not, so the two
    // are compared to a nanosecond and by the frame they show.
    private static void AssertSameAsPassByPass(double length, int passes, Func<int, double> carryOver, Func<double, double> nextPass)
    {
        var walk = new AnimationChainSave();
        for (int i = 0; i < 5; i++) walk.Frames.Add(new AnimationFrameSave { FrameLength = 0.1f });
        double carry = 0;
        for (int pass = 0; pass <= passes; pass++)
        {
            double actual = carryOver(pass);
            actual.ShouldBe(carry, 1e-9, $"pass {pass}");
            ChainClock.Locate(walk, actual, FrameLengthUnit.Seconds).FrameIndex
                .ShouldBe(ChainClock.Locate(walk, carry, FrameLengthUnit.Seconds).FrameIndex, $"pass {pass}");
            carry = nextPass(carry);
        }
    }
}
