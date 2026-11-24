using System.Text.Json.Nodes;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

/// <summary>
/// A continuous reference track keeps the target's playback phase across the holder's loop wrap, when the clip starts at 0,
/// the target loops and the clip is still playing at the wrap. Otherwise the wrap restarts the target.
/// </summary>
public class SequenceReferenceContinuousTests
{
    private const double ParentLength = 8.6;
    private const double TargetLength = 1.0;

    private static SequenceTrackSave Track(bool continuous, double start = 0, double? length = ParentLength) =>
        new() { Continuous = continuous, References = [new SequenceReferenceSave { Sequence = "T", Start = start, Length = length }] };

    private static double? Time(SequenceTrackSave track, bool targetLoops, double parentTime, int pass) =>
        SequenceTimeline.ReferenceTimeAtPass(track, track.References![0], new SequenceSave { Name = "T", Loop = targetLoops }, TargetLength, parentTime, pass, ParentLength, _ => TargetLength, out _);

    [Theory]
    [InlineData(0, 0.3)]
    [InlineData(1, 0.9)] // 8.6 + 0.3 = 8.9, wrapped by the 1 s target
    [InlineData(2, 0.5)] // 17.2 + 0.3 = 17.5
    public void ReferenceTimeAtPass_ContinuousLoopingTarget_CarriesPhaseAcrossWraps(int pass, double expected)
    {
        Time(Track(true), targetLoops: true, parentTime: 0.3, pass)!.Value.ShouldBe(expected, 1e-6);
    }

    [Theory]
    [InlineData(true, 1, 8)] // 8.6 + 0.3 = 8.9: the 1 s target has wrapped 8 times
    [InlineData(true, 0, 0)]
    [InlineData(false, 1, 0)] // not carried: the target runs at pass 0
    public void ReferenceTimeAtPass_CountsTheTargetsOwnWraps_OnlyWhenCarried(bool continuous, int pass, int expectedTargetPass)
    {
        var track = Track(continuous);
        SequenceTimeline.ReferenceTimeAtPass(track, track.References![0], new SequenceSave { Name = "T", Loop = true }, TargetLength, 0.3, pass, ParentLength, _ => TargetLength, out int targetPass);

        targetPass.ShouldBe(expectedTargetPass);
    }

    [Fact]
    public void ReferenceTimeAtPass_NotContinuous_RestartsAtEveryWrap()
    {
        Time(Track(false), targetLoops: true, parentTime: 0.3, pass: 3)!.Value.ShouldBe(0.3, 1e-6);
    }

    [Fact]
    public void ReferenceTimeAtPass_TargetDoesNotLoop_FlagIgnored()
    {
        Time(Track(true), targetLoops: false, parentTime: 0.3, pass: 1)!.Value.ShouldBe(0.3, 1e-6);
    }

    [Fact]
    public void ReferenceTimeAtPass_ClipStartsAfterZero_FlagIgnored()
    {
        Time(Track(true, start: 0.5, length: ParentLength - 0.5), targetLoops: true, parentTime: 1.0, pass: 1)!.Value.ShouldBe(0.5, 1e-6);
    }

    [Fact]
    public void ReferenceTimeAtPass_ClipEndsBeforeTheWrap_FlagIgnored()
    {
        Time(Track(true, length: 2), targetLoops: true, parentTime: 0.3, pass: 1)!.Value.ShouldBe(0.3, 1e-6);
    }

    [Fact]
    public void ReferenceTimeAtPass_ClipWindowStillAppliesWithinEachPass()
    {
        // The clip is 8.6 s long: it is over at the end of each pass, never earlier because of the carry.
        Time(Track(true), targetLoops: true, parentTime: 8.5, pass: 2).ShouldNotBeNull();
        Time(Track(true), targetLoops: true, parentTime: ParentLength, pass: 2).ShouldBeNull();
    }

    [Fact]
    public void ToJsonString_ContinuousReferenceTrack_WritesTheFlagAndReadsItBack()
    {
        var save = new SequenceListSave();
        save.Sequences.Add(new SequenceSave { Name = "P", Tracks = { Track(true) } });

        var json = save.ToJsonString();

        JsonNode.Parse(json)!["sequences"]![0]!["tracks"]![0]!["continuous"]!.GetValue<bool>().ShouldBeTrue();
        SequenceListSave.FromJsonString(json).Sequences[0].Tracks[0].Continuous.ShouldBeTrue();
    }

    [Fact]
    public void ToJsonString_NonContinuousReferenceTrack_RoundTripsByteIdenticalWithoutTheField()
    {
        var save = new SequenceListSave();
        save.Sequences.Add(new SequenceSave { Name = "P", Tracks = { Track(false) } });
        var json = save.ToJsonString();

        json.ShouldNotContain("continuous");
        SequenceListSave.FromJsonString(json).ToJsonString().ShouldBe(json);
    }
}
