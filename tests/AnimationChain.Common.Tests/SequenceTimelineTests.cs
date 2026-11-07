using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

public class SequenceTimelineTests
{
    private static SequenceChainReferenceSave Ref(string chain, double start, float loops = 1) =>
        new() { Source = "a.achj", Chain = chain, Start = start, Loops = loops };

    [Fact]
    public void ActiveAt_LaterChainStartsInsideEarlier_CutsTheEarlierAndNeverResumesIt()
    {
        var charge = Ref("Charge", start: 0, loops: 1); // 3s cycle
        var hurt = Ref("Hurt", start: 1, loops: 1);    // 0.5s cycle
        var track = new SequenceTrackSave { Chains = { charge, hurt } };
        double Cycle(SequenceChainReferenceSave r) => r == charge ? 3 : 0.5;

        SequenceTimeline.ActiveAt(track, 0.5, Cycle, out double local0).ShouldBe(charge);
        local0.ShouldBe(0.5, 1e-9);
        SequenceTimeline.ActiveAt(track, 1.2, Cycle, out _).ShouldBe(hurt);
        // Hurt ended at 1.5; Charge's tail (to 3.0) is cut, so the sprite is hidden.
        SequenceTimeline.ActiveAt(track, 2.0, Cycle, out _).ShouldBeNull();
        SequenceTimeline.PlayedLength(track, charge, 3).ShouldBe(1, 1e-9);
    }

    [Fact]
    public void PlayedLength_InterrupterMovesAway_EarlierChainPlaysInFullAgain()
    {
        var charge = Ref("Charge", start: 0, loops: 2);
        var hurt = Ref("Hurt", start: 1);
        var track = new SequenceTrackSave { Chains = { charge, hurt } };

        hurt.Start = 10;

        SequenceTimeline.PlayedLength(track, charge, cycleLength: 3).ShouldBe(6, 1e-9);
    }

    [Fact]
    public void Length_FloatLoops_EndsPartWayThroughTheLastCycle()
    {
        var sequence = new SequenceSave { Tracks = { new SequenceTrackSave { Chains = { Ref("Walk", start: 1, loops: 2.5f) } } } };

        // A ping-pong chain of 0.4s forward has a 0.8s cycle.
        SequenceTimeline.Length(sequence, _ => 0.8).ShouldBe(3, 1e-6);
    }

    [Theory]
    // edge time, expected loops (cycle 1s, start 0, magnet 0.05s)
    [InlineData(1.7, 1.7)]
    [InlineData(2.04, 2.0)]   // within the magnet: pulled to a whole cycle
    [InlineData(3.6, 3.0)]    // stopped at the next chain, which starts at 3
    public void LoopsForEdgeDrag_FreeWithMagnetAndStopsAtNextChain(double edgeTime, double expected)
    {
        SequenceTimeline.LoopsForEdgeDrag(start: 0, edgeTime, cycleLength: 1, nextStart: 3, magnetSeconds: 0.05)
            .ShouldBe((float)expected, 1e-5f);
    }

    [Fact]
    public void ToJsonString_RoundTrip_KeepsEveryField()
    {
        var save = new SequenceListSave { Sources = { "Boss.achj", "fx/Effects.achx" } };
        save.Sequences.Add(new SequenceSave
        {
            Name = "Attack",
            Loop = true,
            Tracks =
            {
                new SequenceTrackSave
                {
                    Name = "Wing R", X = 75, Y = 95.5f, Z = 3, FlipHorizontal = true,
                    Chains = { new SequenceChainReferenceSave { Source = "Boss.achj", Chain = "WingFlap", Start = 0.25, Loops = 11.5f } },
                },
            },
        });

        var loaded = SequenceListSave.FromJsonString(save.ToJsonString());

        loaded.Sources.ShouldBe(new[] { "Boss.achj", "fx/Effects.achx" });
        var sequence = loaded.Sequences.ShouldHaveSingleItem();
        sequence.Name.ShouldBe("Attack");
        sequence.Loop.ShouldBeTrue();
        var track = sequence.Tracks.ShouldHaveSingleItem();
        (track.Name, track.X, track.Y, track.Z, track.FlipHorizontal).ShouldBe(("Wing R", 75f, 95.5f, 3, true));
        var reference = track.Chains.ShouldHaveSingleItem();
        (reference.Source, reference.Chain, reference.Start, reference.Loops).ShouldBe(("Boss.achj", "WingFlap", 0.25, 11.5f));
    }
}
