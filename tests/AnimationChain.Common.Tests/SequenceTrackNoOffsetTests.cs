using System.Linq;
using System.Text.Json.Nodes;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

/// <summary>A track has no X/Y offset: sprites sit at their frames' offsets. Files written before that still hold <c>"x"</c>/<c>"y"</c>, which are dropped.</summary>
public class SequenceTrackNoOffsetTests
{
    private const string FileWithOffsets = """
        { "version": 1, "sources": [ "Boss.achj" ], "sequences": [ { "name": "Idle", "tracks": [
          { "name": "Body", "x": 12, "y": -3, "flipHorizontal": true, "chains": [ { "source": "Boss.achj", "chain": "Walk", "start": 0, "loops": 1 } ] },
          { "name": "Wing", "x": 0, "y": 0, "tag": "keep", "visible": false, "chains": [] } ] } ] }
        """;

    private static JsonArray WrittenTracks(SequenceListSave save) =>
        JsonNode.Parse(save.ToJsonString())!["sequences"]![0]!["tracks"]!.AsArray();

    [Fact]
    public void ToJsonString_OfANewTrack_HasNoXOrY()
    {
        var save = new SequenceListSave();
        save.Sequences.Add(new SequenceSave { Name = "S", Tracks = { new SequenceTrackSave { Name = "T" } } });

        WrittenTracks(save)[0]!.AsObject().Select(p => p.Key).ShouldBe(new[] { "name", "chains" });
    }

    [Fact]
    public void FromJsonString_FileWithOffsetsPerTrack_LoadsWithoutFailing_AndKeepsEverythingElse()
    {
        var loaded = SequenceListSave.FromJsonString(FileWithOffsets);

        var tracks = loaded.Sequences.ShouldHaveSingleItem().Tracks;
        tracks.Select(t => (t.Name, t.FlipHorizontal, t.Visible, t.Chains.Count)).ShouldBe(new[] { ("Body", true, true, 1), ("Wing", false, false, 0) });
        tracks[1].UnknownProperties["tag"]!.GetValue<string>().ShouldBe("keep");
    }

    [Fact]
    public void ToJsonString_AfterLoadingAFileWithOffsets_WritesNoXOrY_NotEvenAsUnknownProperties()
    {
        var loaded = SequenceListSave.FromJsonString(FileWithOffsets);

        loaded.Sequences[0].Tracks.ShouldAllBe(t => !t.UnknownProperties.ContainsKey("x") && !t.UnknownProperties.ContainsKey("y"));
        var written = WrittenTracks(loaded);
        foreach (var track in written)
        {
            track!.AsObject().ContainsKey("x").ShouldBeFalse();
            track.AsObject().ContainsKey("y").ShouldBeFalse();
        }
        written[0]!["flipHorizontal"]!.GetValue<bool>().ShouldBeTrue();
        written[1]!["tag"]!.GetValue<string>().ShouldBe("keep");
        written[1]!["visible"]!.GetValue<bool>().ShouldBeFalse();
    }

    [Fact]
    public void FromJsonString_AWrongTypedX_IsIgnoredToo()
    {
        var loaded = SequenceListSave.FromJsonString("""{ "sequences": [ { "name": "S", "tracks": [ { "name": "T", "x": "left", "y": [1] } ] } ] }""");

        loaded.Sequences[0].Tracks.ShouldHaveSingleItem().Name.ShouldBe("T");
        WrittenTracks(loaded)[0]!.AsObject().ContainsKey("x").ShouldBeFalse();
    }
}
