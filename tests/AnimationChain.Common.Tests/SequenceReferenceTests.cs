using System.Text.Json;
using System.Text.Json.Nodes;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

/// <summary>A track that plays another sequence of the same file: format, cycles, inlining and timing.</summary>
public class SequenceReferenceTests
{
    private static SequenceSave Sequence(string name, bool loop = true, params SequenceTrackSave[] tracks)
    {
        var sequence = new SequenceSave { Name = name, Loop = loop };
        sequence.Tracks.AddRange(tracks);
        return sequence;
    }

    private static SequenceTrackSave ChainTrack(string name, double start = 0, float loops = 1, bool visible = true) =>
        new() { Name = name, Visible = visible, Chains = { new SequenceChainReferenceSave { Source = "a.achx", Chain = name, Start = start, Loops = loops } } };

    private static SequenceTrackSave RefTrack(string name, string target, double start = 0, float loops = 1, bool visible = true) =>
        new() { Name = name, Visible = visible, Reference = new SequenceReferenceSave { Sequence = target, Start = start, Loops = loops } };

    [Fact]
    public void RoundTrip_ReferenceTrack_KeepsSequenceStartAndLoops()
    {
        var save = new SequenceListSave { Sequences = { Sequence("Idle", true, RefTrack("Earth", "Earth", start: 0.5, loops: 2)), Sequence("Earth") } };

        var loaded = SequenceListSave.FromJsonString(save.ToJsonString());

        var reference = loaded.Sequences[0].Tracks[0].Reference!;
        reference.Sequence.ShouldBe("Earth");
        reference.Start.ShouldBe(0.5);
        reference.Loops.ShouldBe(2f);
    }

    [Fact]
    public void ToJsonString_ReferenceTrack_WritesReferenceObjectAndNoChains()
    {
        var save = new SequenceListSave { Sequences = { Sequence("Idle", true, RefTrack("Earth", "Earth", 1, 3)), Sequence("Earth") } };

        var track = JsonNode.Parse(save.ToJsonString())!["sequences"]![0]!["tracks"]![0]!.AsObject();

        track.Select(p => p.Key).ShouldBe(new[] { "name", "reference" });
        track["reference"]!.AsObject().Select(p => p.Key).ShouldBe(new[] { "sequence", "start", "loops" });
    }

    [Fact]
    public void ToJsonString_ChainTrack_StaysUnchanged()
    {
        var save = new SequenceListSave { Sequences = { Sequence("Idle", true, ChainTrack("Body")) } };

        var track = JsonNode.Parse(save.ToJsonString())!["sequences"]![0]!["tracks"]![0]!.AsObject();

        track.Select(p => p.Key).ShouldBe(new[] { "name", "chains" });
    }

    [Fact]
    public void FromJsonString_TrackWithReferenceAndChains_IsRefusedNamingTheSequenceAndTrack()
    {
        const string json = """
            { "sequences": [
              { "name": "Idle", "tracks": [ { "name": "Earth", "reference": { "sequence": "Spin" },
                "chains": [ { "source": "a.achx", "chain": "Walk" } ] } ] },
              { "name": "Spin", "tracks": [] } ] }
            """;

        var ex = Should.Throw<JsonException>(() => SequenceListSave.FromJsonString(json));

        ex.Message.ShouldBe("Sequence \"Idle\", track \"Earth\" has both a reference and chains; a track plays one or the other. Remove one.");
    }

    [Fact]
    public void FromJsonString_TrackWithReferenceAndEmptyChains_LoadsAsAReference()
    {
        var loaded = SequenceListSave.FromJsonString("""
            { "sequences": [
              { "name": "Idle", "tracks": [ { "name": "Earth", "reference": { "sequence": "Spin" }, "chains": [] } ] },
              { "name": "Spin", "tracks": [] } ] }
            """);

        var track = loaded.Sequences[0].Tracks[0];
        track.Reference!.Sequence.ShouldBe("Spin");
        track.Chains.ShouldBeEmpty();
    }

    [Fact]
    public void FromJsonString_FileWithoutReferences_LoadsWithNoReference()
    {
        var loaded = SequenceListSave.FromJsonString("""{ "sequences": [ { "name": "A", "tracks": [ { "name": "T", "chains": [] } ] } ] }""");

        loaded.Sequences[0].Tracks[0].Reference.ShouldBeNull();
    }

    [Fact]
    public void RoundTrip_UnknownPropertyOnReference_IsKept()
    {
        const string json = """
            { "sequences": [ { "name": "A", "tracks": [ { "name": "E", "reference": { "sequence": "B", "start": 0, "loops": 1, "tint": "red" } } ] }, { "name": "B" } ] }
            """;

        var again = JsonNode.Parse(SequenceListSave.FromJsonString(json).ToJsonString())!["sequences"]![0]!["tracks"]![0]!["reference"]!;

        again["tint"]!.GetValue<string>().ShouldBe("red");
    }

    [Fact]
    public void FromJsonString_ReferenceWithWrongKind_NamesTheProperty()
    {
        var ex = Should.Throw<JsonException>(() => SequenceListSave.FromJsonString(
            """{ "sequences": [ { "name": "A", "tracks": [ { "name": "E", "reference": { "sequence": 5 } } ] } ] }"""));

        ex.Message.ShouldContain("sequences[0].tracks[0].reference.sequence");
    }

    [Fact]
    public void FromJsonString_SelfReference_LoadsAndSavesUnchanged_AndPlaysNothing()
    {
        var save = new SequenceListSave { Sequences = { Sequence("A", true, ChainTrack("Body"), RefTrack("E", "A")) } };
        string json = save.ToJsonString();

        var loaded = SequenceListSave.FromJsonString(json);

        loaded.ToJsonString().ShouldBe(json);
        SequenceReferences.Inline(loaded.Sequences, loaded.Sequences[0]).Select(t => t.Name).ShouldBe(new[] { "Body" });
        SequenceReferences.CycleThrough(loaded.Sequences, "A", "A").ShouldBe(new[] { "A", "A" });
    }

    [Fact]
    public void FromJsonString_TwoStepCycle_Loads_TheClosingReferencesPlayNothing_AndOtherSequencesStillPlay()
    {
        var save = new SequenceListSave
        {
            Sequences =
            {
                Sequence("A", true, ChainTrack("Body"), RefTrack("x", "B")),
                Sequence("B", true, ChainTrack("Leg"), RefTrack("y", "A")),
                Sequence("C", true, RefTrack("a", "A")),
            },
        };

        var loaded = SequenceListSave.FromJsonString(save.ToJsonString());
        var sequences = loaded.Sequences;

        SequenceReferences.Inline(sequences, sequences[0]).Select(t => t.Name).ShouldBe(new[] { "Body" });
        SequenceReferences.Inline(sequences, sequences[1]).Select(t => t.Name).ShouldBe(new[] { "Leg" });
        SequenceReferences.Inline(sequences, sequences[2]).Select(t => t.Name).ShouldBe(new[] { "a/Body" });
        SequenceReferences.CycleThrough(sequences, "A", "B").ShouldBe(new[] { "A", "B", "A" });
        SequenceReferences.CycleThrough(sequences, "C", "A").ShouldBeNull();
    }

    [Fact]
    public void Inline_DeepChainWithoutCycle_ReachesTheLeaf_WithNoFalseCycle()
    {
        const int depth = 500;
        var sequences = new List<SequenceSave>();
        for (int i = 0; i < depth; i++) sequences.Add(Sequence($"S{i}", true, RefTrack("r", $"S{i + 1}")));
        sequences.Add(Sequence($"S{depth}", true, ChainTrack("Leaf")));

        var tracks = SequenceReferences.Inline(sequences, sequences[0]);

        tracks.Single().Track.Name.ShouldBe("Leaf");
        tracks.Single().Hops.Count.ShouldBe(depth);
        SequenceReferences.CycleThrough(sequences, "S0", "S1").ShouldBeNull();
        SequenceReferences.FindCycle(sequences).ShouldBeNull();
    }

    [Fact]
    public void FromJsonString_ReferenceToMissingSequence_LoadsAndKeepsTheName()
    {
        var loaded = SequenceListSave.FromJsonString(
            """{ "sequences": [ { "name": "A", "tracks": [ { "name": "E", "reference": { "sequence": "Gone" } } ] } ] }""");

        loaded.Sequences[0].Tracks[0].Reference!.Sequence.ShouldBe("Gone");
    }

    [Fact]
    public void WouldCycle_TargetReachesHost_IsTrue()
    {
        var sequences = new List<SequenceSave> { Sequence("A"), Sequence("B", true, RefTrack("r", "A")) };

        SequenceReferences.WouldCycle(sequences, host: "A", target: "B").ShouldBeTrue();
        SequenceReferences.WouldCycle(sequences, host: "A", target: "A").ShouldBeTrue();
        SequenceReferences.WouldCycle(sequences, host: "B", target: "A").ShouldBeFalse();
    }

    [Fact]
    public void Inline_ReferenceTrack_PrefixesInheritedTrackNamesWithTheReferenceTrack()
    {
        var sequences = new List<SequenceSave>
        {
            Sequence("Idle", true, ChainTrack("Body"), RefTrack("Earth", "Earth")),
            Sequence("Earth", true, ChainTrack("Body")),
        };

        var names = SequenceReferences.Inline(sequences, sequences[0]).Select(t => t.Name).ToList();

        names.ShouldBe(new[] { "Body", "Earth/Body" });
    }

    [Fact]
    public void Inline_NestedReference_PrefixesEachLevel()
    {
        var sequences = new List<SequenceSave>
        {
            Sequence("A", true, RefTrack("b", "B")),
            Sequence("B", true, RefTrack("c", "C")),
            Sequence("C", true, ChainTrack("Leaf")),
        };

        SequenceReferences.Inline(sequences, sequences[0]).Single().Name.ShouldBe("b/c/Leaf");
    }

    [Fact]
    public void Inline_HiddenReferenceTrackOrInheritedTrack_IsNotVisible()
    {
        var sequences = new List<SequenceSave>
        {
            Sequence("A", true, RefTrack("hiddenRef", "B", visible: false), RefTrack("ok", "B")),
            Sequence("B", true, ChainTrack("Shown"), ChainTrack("Hidden", visible: false)),
        };

        var tracks = SequenceReferences.Inline(sequences, sequences[0]);

        tracks.Select(t => (t.Name, t.Visible)).ShouldBe(new[]
        {
            ("hiddenRef/Shown", false), ("hiddenRef/Hidden", false), ("ok/Shown", true), ("ok/Hidden", false),
        });
    }

    [Fact]
    public void Inline_ReferenceToMissingSequence_YieldsNothing()
    {
        var sequences = new List<SequenceSave> { Sequence("A", true, RefTrack("r", "Gone")) };

        SequenceReferences.Inline(sequences, sequences[0]).ShouldBeEmpty();
    }

    [Fact]
    public void Inline_CycleBuiltInCode_DoesNotRecurseForever()
    {
        var sequences = new List<SequenceSave> { Sequence("A", true, RefTrack("r", "A")) };

        SequenceReferences.Inline(sequences, sequences[0]).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(-0.1, null)]      // before the clip starts
    [InlineData(1.0, 0.0)]        // the target starts with the parent's clock at the clip start
    [InlineData(1.5, 0.5)]
    [InlineData(3.0, 0.0)]        // a looping target wraps at its length (2 s)
    [InlineData(4.9, 1.9)]
    [InlineData(5.0, null)]       // 2 loops of 2 s end at 1 + 4
    public void ReferenceTime_LoopingTarget_WrapsAndEndsAfterLoopsCycles(double parentTime, double? expected)
    {
        var reference = new SequenceReferenceSave { Sequence = "T", Start = 1, Loops = 2 };

        var time = SequenceTimeline.ReferenceTime(reference, new SequenceSave { Name = "T", Loop = true }, targetLength: 2, parentTime);

        if (expected is null) time.ShouldBeNull();
        else time!.Value.ShouldBe(expected.Value, 1e-9);
    }

    [Fact]
    public void ReferenceTime_NonLoopingTarget_HoldsItsFinalPoseUntilTheClipEnds()
    {
        var reference = new SequenceReferenceSave { Sequence = "T", Start = 0, Loops = 2 };
        var target = new SequenceSave { Name = "T", Loop = false };

        var time = SequenceTimeline.ReferenceTime(reference, target, targetLength: 2, parentTime: 3);

        time.ShouldBe(SequenceTimeline.PoseTime(2, 2, false));
        SequenceTimeline.ReferenceTime(reference, target, 2, 4.0).ShouldBeNull();
    }

    [Fact]
    public void ReferenceTime_EmptyTarget_IsNeverActive()
    {
        var reference = new SequenceReferenceSave { Sequence = "T" };

        SequenceTimeline.ReferenceTime(reference, new SequenceSave { Name = "T" }, targetLength: 0, parentTime: 0).ShouldBeNull();
    }

    [Fact]
    public void Length_ReferenceTrack_ExtendsTheParentToTheClipEnd()
    {
        var sequences = new List<SequenceSave>
        {
            Sequence("Idle", true, ChainTrack("Body"), RefTrack("Earth", "Earth", start: 0.5, loops: 10)),
            Sequence("Earth", true, ChainTrack("Body")),
        };

        double length = SequenceTimeline.Length(sequences, sequences[0], _ => 1.0);

        length.ShouldBe(10.5, 1e-9);
    }

    [Fact]
    public void Length_BrokenReference_AddsNothing()
    {
        var sequences = new List<SequenceSave>
        {
            Sequence("Idle", true, ChainTrack("Body"), RefTrack("Gone", "Missing", loops: 5), RefTrack("Loop", "Back", loops: 5)),
            Sequence("Back", true, ChainTrack("Body", loops: 3), RefTrack("Idle", "Idle")),
        };

        SequenceTimeline.Length(sequences, sequences[0], _ => 1.0).ShouldBe(1.0, 1e-9);
        // Back's reference to Idle closes the cycle too, so Back is its own chain only.
        SequenceTimeline.Length(sequences, sequences[1], _ => 1.0).ShouldBe(3.0, 1e-9);
    }

    [Fact]
    public void Length_SequenceOfOnlyReferences_PlaysThroughEveryLevel()
    {
        var sequences = new List<SequenceSave>
        {
            Sequence("A", true, RefTrack("b", "B", start: 1)),
            Sequence("B", true, RefTrack("c", "C", start: 0.5, loops: 2)),
            Sequence("C", true, ChainTrack("Leaf", loops: 2)),
        };
        double Length(SequenceSave s) => SequenceTimeline.Length(sequences, s, _ => 1.0);
        var leaf = SequenceReferences.Inline(sequences, sequences[0]).Single(t => t.Name == "b/c/Leaf");

        // C is 2 s; B's clip of C runs 0.5 + 2 x 2; A's clip of B starts at 1.
        Length(sequences[1]).ShouldBe(4.5, 1e-9);
        Length(sequences[0]).ShouldBe(5.5, 1e-9);
        SequenceReferences.InlinedTime(leaf, 4.0, Length)!.Value.ShouldBe(0.5, 1e-9);
    }

    [Fact]
    public void InlinedTime_NestedHops_MapsTimeThroughEachClip()
    {
        var sequences = new List<SequenceSave>
        {
            Sequence("A", true, RefTrack("b", "B", start: 1)),
            Sequence("B", true, RefTrack("c", "C", start: 0.5), ChainTrack("pad")),
            Sequence("C", true, ChainTrack("Leaf")),
        };
        var leaf = SequenceReferences.Inline(sequences, sequences[0]).Single(t => t.Name == "b/c/Leaf");

        // B is 4 s and C 1 s long; C's clip starts 0.5 s into B, and B's clip starts 1 s into A.
        double Length(SequenceSave target) => target.Name == "B" ? 4 : 1;
        SequenceReferences.InlinedTime(leaf, 2.0, Length)!.Value.ShouldBe(0.5, 1e-9);
        SequenceReferences.InlinedTime(leaf, 1.2, Length).ShouldBeNull();
    }

    [Theory]
    [InlineData(1.9, 0.9)]
    [InlineData(2.0, null)]       // half a cycle of a 2 s target ends 1 s after the start
    public void ReferenceTime_FractionalLoops_EndsPartWayThroughTheCycle(double parentTime, double? expected)
    {
        var reference = new SequenceReferenceSave { Sequence = "T", Start = 1, Loops = 0.5f };

        var time = SequenceTimeline.ReferenceTime(reference, new SequenceSave { Name = "T", Loop = true }, targetLength: 2, parentTime);

        if (expected is null) time.ShouldBeNull();
        else time!.Value.ShouldBe(expected.Value, 1e-9);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    public void ReferenceTime_ZeroOrNegativeLoops_IsNeverActive(float loops)
    {
        var reference = new SequenceReferenceSave { Sequence = "T", Start = 0, Loops = loops };
        var target = new SequenceSave { Name = "T", Loop = true };

        SequenceTimeline.ReferenceTime(reference, target, 2, 0).ShouldBeNull();
        SequenceTimeline.ReferenceTime(reference, target, 2, 0.5).ShouldBeNull();
    }

    [Theory]
    [InlineData(0.0, 1.0)]        // a clip starting at -1 is already 1 s in when the holder starts
    [InlineData(0.5, 1.5)]
    [InlineData(1.0, null)]
    public void ReferenceTime_NegativeStartFromDisk_PlaysFromPartWayIn(double parentTime, double? expected)
    {
        var reference = new SequenceReferenceSave { Sequence = "T", Start = -1, Loops = 1 };

        var time = SequenceTimeline.ReferenceTime(reference, new SequenceSave { Name = "T", Loop = true }, targetLength: 2, parentTime);

        if (expected is null) time.ShouldBeNull();
        else time!.Value.ShouldBe(expected.Value, 1e-9);
    }

    [Fact]
    public void Length_NonLoopingTarget_ClipStillLastsAllItsLoops()
    {
        var sequences = new List<SequenceSave>
        {
            Sequence("A", true, RefTrack("b", "B", loops: 3)),
            Sequence("B", false, ChainTrack("Body")),
        };

        SequenceTimeline.Length(sequences, sequences[0], _ => 1.0).ShouldBe(3.0, 1e-9);
    }

    [Fact]
    public void Length_HiddenReferenceTrack_StillCounts()
    {
        var sequences = new List<SequenceSave>
        {
            Sequence("A", true, ChainTrack("Body"), RefTrack("b", "B", loops: 4, visible: false)),
            Sequence("B", true, ChainTrack("Body")),
        };

        SequenceTimeline.Length(sequences, sequences[0], _ => 1.0).ShouldBe(4.0, 1e-9);
    }

    [Fact]
    public void Length_FourLevelsOfReferences_AddsEveryStart_AndTimeMapsToTheLeaf()
    {
        var sequences = new List<SequenceSave>
        {
            Sequence("A", true, RefTrack("b", "B", start: 1)),
            Sequence("B", true, RefTrack("c", "C", start: 0.5)),
            Sequence("C", true, RefTrack("d", "D", start: 0.25, loops: 2)),
            Sequence("D", true, ChainTrack("Leaf")),
        };
        double Length(SequenceSave s) => SequenceTimeline.Length(sequences, s, _ => 1.0);

        Length(sequences[2]).ShouldBe(2.25, 1e-9);
        Length(sequences[1]).ShouldBe(2.75, 1e-9);
        Length(sequences[0]).ShouldBe(3.75, 1e-9);
        var leaf = SequenceReferences.Inline(sequences, sequences[0]).Single();
        leaf.Name.ShouldBe("b/c/d/Leaf");
        leaf.Hops.Select(h => h.Target.Name).ShouldBe(new[] { "B", "C", "D" });
        // 3.25 in A is 2.25 in B, 1.75 in C, 1.5 into D's clip: the second cycle of a 1 s D.
        SequenceReferences.InlinedTime(leaf, 3.25, Length)!.Value.ShouldBe(0.5, 1e-9);
    }

    [Fact]
    public void Inline_TargetHoldingACycle_PlaysTheTargetsOwnTracksOnly()
    {
        var sequences = new List<SequenceSave>
        {
            Sequence("A", true, RefTrack("b", "B", loops: 1)),
            Sequence("B", true, ChainTrack("Legs"), RefTrack("c", "C")),
            Sequence("C", true, ChainTrack("Arm"), RefTrack("b", "B")),
        };

        SequenceReferences.Plays(sequences, "A", "B").ShouldBeTrue("A is not on the cycle");
        SequenceReferences.Inline(sequences, sequences[0]).Select(t => t.Name).ShouldBe(new[] { "b/Legs" });
        SequenceTimeline.Length(sequences, sequences[0], _ => 1.0).ShouldBe(1.0, 1e-9);
        SequenceReferences.FindCycle(sequences).ShouldBe(new[] { "B", "C", "B" });
    }

    [Fact]
    public void Diamond_TwoPathsToOneSequence_IsNoCycle_AndInlinesBothPaths()
    {
        var sequences = new List<SequenceSave>
        {
            Sequence("A", true, RefTrack("b", "B"), RefTrack("c", "C")),
            Sequence("B", true, RefTrack("d", "D")),
            Sequence("C", true, RefTrack("d", "D")),
            Sequence("D", true, ChainTrack("Leaf")),
        };

        SequenceReferences.FindCycle(sequences).ShouldBeNull();
        SequenceReferences.CyclicReferenceCount(sequences).ShouldBe(0);
        SequenceReferences.WouldCycle(sequences, "A", "D").ShouldBeFalse();
        SequenceReferences.Inline(sequences, sequences[0]).Select(t => t.Name).ShouldBe(new[] { "b/d/Leaf", "c/d/Leaf" });
    }

    [Fact]
    public void CyclicReferenceCount_CountsEveryReferenceOnTheCycle()
    {
        var self = new List<SequenceSave> { Sequence("A", true, RefTrack("a", "A")) };
        var twoStep = new List<SequenceSave>
        {
            Sequence("A", true, RefTrack("b", "B"), RefTrack("gone", "Missing")),
            Sequence("B", true, RefTrack("a", "A")),
        };

        SequenceReferences.CyclicReferenceCount(self).ShouldBe(1);
        SequenceReferences.CyclicReferenceCount(twoStep).ShouldBe(2);
    }

    [Fact]
    public void Inline_DuplicateSequenceNames_TheFirstWins()
    {
        var sequences = new List<SequenceSave>
        {
            Sequence("A", true, RefTrack("b", "B")),
            Sequence("B", true, ChainTrack("First")),
            Sequence("B", true, ChainTrack("Second")),
        };

        SequenceReferences.Inline(sequences, sequences[0]).Select(t => t.Name).ShouldBe(new[] { "b/First" });
    }

    [Fact]
    public void FromJsonString_ReferenceWithoutStartOrLoops_StartsAtZeroForOneCycle()
    {
        var loaded = SequenceListSave.FromJsonString(
            """{ "sequences": [ { "name": "A", "tracks": [ { "name": "E", "reference": { "sequence": "B" } } ] }, { "name": "B" } ] }""");

        var reference = loaded.Sequences[0].Tracks[0].Reference!;
        reference.Start.ShouldBe(0);
        reference.Loops.ShouldBe(1f);
    }

    [Fact]
    public void RoundTrip_HiddenReferenceTrack_FractionalLoopsAndNegativeStart_AreKept()
    {
        var save = new SequenceListSave { Sequences = { Sequence("A", true, RefTrack("E", "B", start: -0.5, loops: 0.25f, visible: false)), Sequence("B") } };
        string json = save.ToJsonString();

        var loaded = SequenceListSave.FromJsonString(json);

        var track = loaded.Sequences[0].Tracks[0];
        track.Visible.ShouldBeFalse();
        track.Reference!.Start.ShouldBe(-0.5);
        track.Reference.Loops.ShouldBe(0.25f);
        loaded.ToJsonString().ShouldBe(json);
    }

    [Fact]
    public void InlinedTime_OuterClipEnded_IsNullEvenWhenTheInnerWouldStillPlay()
    {
        var sequences = new List<SequenceSave>
        {
            Sequence("A", true, RefTrack("b", "B", loops: 0.5f)),
            Sequence("B", true, ChainTrack("Pad", loops: 2), RefTrack("c", "C", loops: 5)),
            Sequence("C", true, ChainTrack("Leaf")),
        };
        double Length(SequenceSave s) => SequenceTimeline.Length(sequences, s, _ => 1.0);
        var leaf = SequenceReferences.Inline(sequences, sequences[0]).Single(t => t.Name == "b/c/Leaf");

        // B is 5 s (its clip of C), so A's half cycle of B ends at 2.5 while C would play on in B until 5.
        SequenceReferences.InlinedTime(leaf, 2.25, Length)!.Value.ShouldBe(0.25, 1e-9);
        SequenceReferences.InlinedTime(leaf, 3.0, Length).ShouldBeNull();
    }

    [Fact]
    public void InlinedTime_NonLoopingMiddleSequence_HoldsTheInnerFinalPose()
    {
        var sequences = new List<SequenceSave>
        {
            Sequence("A", true, RefTrack("b", "B", loops: 2)),
            Sequence("B", false, RefTrack("c", "C", loops: 2)),
            Sequence("C", true, ChainTrack("Leaf")),
        };
        double Length(SequenceSave s) => SequenceTimeline.Length(sequences, s, _ => 1.0);
        var leaf = SequenceReferences.Inline(sequences, sequences[0]).Single();

        // B (2 s, Loop off) holds its last instant during A's second cycle of it; C is then near the end of its second cycle.
        SequenceReferences.InlinedTime(leaf, 3.0, Length)!.Value.ShouldBe(1.0, 1e-3);
    }
}
