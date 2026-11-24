using System.Text.Json;
using System.Text.Json.Nodes;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

/// <summary>A reference track holds several clips, one after another, with the chain track's cut rule.</summary>
public class SequenceMultiClipTests
{
    private static SequenceReferenceSave Clip(string target, double start, double? length = null) =>
        new() { Sequence = target, Start = start, Length = length };

    private static SequenceTrackSave Track(params SequenceReferenceSave[] clips) =>
        new() { Name = "E", References = [.. clips] };

    [Fact]
    public void RoundTrip_TwoClips_WritesAReferencesArrayAndReadsItBack()
    {
        var save = new SequenceListSave();
        save.Sequences.Add(new SequenceSave { Name = "A", Tracks = { Track(Clip("B", 0, 2), Clip("C", 2)) } });

        var json = save.ToJsonString();
        var track = JsonNode.Parse(json)!["sequences"]![0]!["tracks"]![0]!.AsObject();
        track.ContainsKey("reference").ShouldBeFalse();
        track["references"]!.AsArray().Count.ShouldBe(2);

        var clips = SequenceListSave.FromJsonString(json).Sequences[0].Tracks[0].References!;
        clips.Select(c => (c.Sequence, c.Start, c.Length)).ShouldBe([("B", 0.0, (double?)2.0), ("C", 2.0, null)]);
    }

    [Fact]
    public void FromJsonString_LegacySingleReference_LoadsAsOneClipAndSavesStably()
    {
        const string legacy = """{ "sequences": [ { "name": "A", "tracks": [ { "name": "E", "reference": { "sequence": "B", "start": 1, "length": 2, "tint": "red" } } ] }, { "name": "B" } ] }""";

        var loaded = SequenceListSave.FromJsonString(legacy);
        var clip = loaded.Sequences[0].Tracks[0].References!.ShouldHaveSingleItem();
        (clip.Sequence, clip.Start, clip.Length).ShouldBe(("B", 1.0, (double?)2.0));

        string first = loaded.ToJsonString();
        first.ShouldContain("\"references\"");
        first.ShouldNotContain("\"reference\"");
        first.ShouldContain("\"tint\"");
        SequenceListSave.FromJsonString(first).ToJsonString().ShouldBe(first);
    }

    [Fact]
    public void RoundTrip_EmptyReferenceTrack_StaysAReferenceTrack()
    {
        var save = new SequenceListSave();
        save.Sequences.Add(new SequenceSave { Name = "A", Tracks = { new SequenceTrackSave { Name = "E", References = [] } } });

        SequenceListSave.FromJsonString(save.ToJsonString()).Sequences[0].Tracks[0].References.ShouldNotBeNull().ShouldBeEmpty();
    }

    [Fact]
    public void FromJsonString_ReferenceAndReferences_Throws()
    {
        const string json = """{ "sequences": [ { "name": "A", "tracks": [ { "name": "E", "reference": { "sequence": "B" }, "references": [] } ] } ] }""";

        Should.Throw<JsonException>(() => SequenceListSave.FromJsonString(json)).Message.ShouldContain("both \"reference\" and \"references\"");
    }

    [Fact]
    public void ReferenceTime_NextClipStart_CutsThePreviousClip()
    {
        var track = Track(Clip("B", 0, 4), Clip("C", 2));
        var b = new SequenceSave { Name = "B", Loop = true };
        var first = track.References![0];

        SequenceTimeline.ReferenceTime(first, b, 1, 1.9, SequenceTimeline.NextStart(track, first)).ShouldNotBeNull();
        SequenceTimeline.ReferenceTime(first, b, 1, 2.0, SequenceTimeline.NextStart(track, first)).ShouldBeNull();
    }

    [Fact]
    public void ActiveClipAt_PicksTheLatestStartedClip_AndHidesAfterTheLastEnds()
    {
        var track = Track(Clip("C", 3, 1), Clip("B", 0, 10));
        double Len(SequenceReferenceSave _) => 1;

        SequenceTimeline.ActiveClipAt(track, 1, Len)!.Sequence.ShouldBe("B");
        SequenceTimeline.ActiveClipAt(track, 3.5, Len)!.Sequence.ShouldBe("C");
        SequenceTimeline.ActiveClipAt(track, 4, Len).ShouldBeNull();
    }

    [Fact]
    public void Length_SeveralClips_EndsAtTheLatestPlayedEnd()
    {
        var sequences = new List<SequenceSave>
        {
            new() { Name = "A", Tracks = { Track(Clip("B", 0, 10), Clip("C", 2, 1)) } },
            new() { Name = "B" }, new() { Name = "C" },
        };
        // B and C have no chains: length 0, so nothing plays. Give them chains of 1 s.
        foreach (var name in new[] { "B", "C" })
            sequences.Single(s => s.Name == name).Tracks.Add(new SequenceTrackSave
            {
                Name = "T",
                Chains = { new SequenceChainReferenceSave { Source = "a", Chain = name, Start = 0 } },
            });

        // Clip B is cut at 2 (C starts), C runs 2..3.
        SequenceTimeline.Length(sequences, sequences[0], _ => 1).ShouldBe(3, 1e-9);
    }

    [Fact]
    public void Length_ClipCutByLaterClip_DoesNotExtendTheSequence()
    {
        var sequences = new List<SequenceSave>
        {
            new() { Name = "A", Tracks = { Track(Clip("B", 0, 100), Clip("B", 1, 1)) } },
            new() { Name = "B", Tracks = { new SequenceTrackSave { Name = "T", Chains = { new SequenceChainReferenceSave { Source = "a", Chain = "x" } } } } },
        };

        SequenceTimeline.Length(sequences, sequences[0], _ => 1).ShouldBe(2, 1e-9);
    }

    [Fact]
    public void Cycle_OnASecondClip_IsDetected()
    {
        var sequences = new List<SequenceSave>
        {
            new() { Name = "A", Tracks = { Track(Clip("B", 0, 1), Clip("A", 1)) } },
            new() { Name = "B" },
        };

        SequenceReferences.FindCycle(sequences).ShouldNotBeNull();
        SequenceReferences.CyclicReferenceCount(sequences).ShouldBe(1);
    }

    [Fact]
    public void Inline_TwoClips_ProduceTheTargetTracksInStartOrderUnderOnePrefix()
    {
        var sequences = new List<SequenceSave>
        {
            new() { Name = "A", Tracks = { Track(Clip("C", 2), Clip("B", 0, 2)) } },
            new() { Name = "B", Tracks = { new SequenceTrackSave { Name = "Body" } } },
            new() { Name = "C", Tracks = { new SequenceTrackSave { Name = "Body" } } },
        };

        var inlined = SequenceReferences.Inline(sequences, sequences[0]);

        inlined.Select(t => (t.Name, t.Hops[0].Target.Name)).ShouldBe([("E/Body", "B"), ("E/Body", "C")]);
    }

    private static (SequenceTrackSave Track, Func<SequenceReferenceSave, double> Len) Carry(params SequenceReferenceSave[] clips) =>
        (new SequenceTrackSave { Name = "E", Continuous = true, References = [.. clips] }, _ => 1.0);

    [Fact]
    public void ReferenceCarryOver_FirstClipPlayingAtTheWrap_CarriesLikeBefore()
    {
        var (track, len) = Carry(Clip("T", 0, 8.6));

        SequenceTimeline.ReferenceCarryOver(track, 8.6, 2, len, _ => true).ShouldBe(17.2, 1e-6);
    }

    [Fact]
    public void ReferenceCarryOver_LaterClipOfTheSameTargetAtTheWrap_CarriesItsPlayedTime()
    {
        var (track, len) = Carry(Clip("T", 0, 4), Clip("T", 4, 4.6));

        // 8.6 - 4 = 4.6 s played by the second clip at the wrap; not the first clip, so no earlier carry.
        SequenceTimeline.ReferenceCarryOver(track, 8.6, 1, len, _ => true).ShouldBe(4.6, 1e-6);
    }

    [Fact]
    public void ReferenceCarryOver_DifferentTargetAtTheWrap_IsZero()
    {
        var (track, len) = Carry(Clip("T", 0, 4), Clip("U", 4, 4.6));

        SequenceTimeline.ReferenceCarryOver(track, 8.6, 1, len, _ => true).ShouldBe(0);
    }

    [Fact]
    public void ReferenceTimeAtPass_SecondClip_NeverCarries()
    {
        var (track, len) = Carry(Clip("T", 0, 8.6), Clip("T", 8.6));
        var target = new SequenceSave { Name = "T", Loop = true };

        SequenceTimeline.ReferenceTimeAtPass(track, track.References![1], target, 1, 0.3, 1, 8.6, len, out int pass).ShouldBeNull();
        pass.ShouldBe(0);
    }
}
