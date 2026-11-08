using System.Linq;
using System.Text.Json.Nodes;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

/// <summary>A track has only a Name and a Visible flag, no flip. Files written before that still hold <c>"flipHorizontal"</c>, which is dropped.</summary>
public class SequenceTrackNoFlipTests
{
    private const string FileWithFlips = """
        { "version": 1, "sources": [ "Boss.achj" ], "sequences": [ { "name": "Idle", "tracks": [
          { "name": "Body", "flipHorizontal": true, "chains": [ { "source": "Boss.achj", "chain": "Walk", "start": 0, "loops": 1 } ] },
          { "name": "Wing", "flipHorizontal": true, "tag": "keep", "visible": false, "chains": [] } ] } ] }
        """;

    private static JsonArray WrittenTracks(SequenceListSave save) =>
        JsonNode.Parse(save.ToJsonString())!["sequences"]![0]!["tracks"]!.AsArray();

    [Fact]
    public void ATrack_HasNoFlipMember()
    {
        typeof(SequenceTrackSave).GetProperty("FlipHorizontal").ShouldBeNull();
    }

    [Fact]
    public void ToJsonString_OfANewTrack_HasNoFlipKey()
    {
        var save = new SequenceListSave();
        save.Sequences.Add(new SequenceSave { Name = "S", Tracks = { new SequenceTrackSave { Name = "T" } } });

        WrittenTracks(save)[0]!.AsObject().Select(p => p.Key).ShouldBe(new[] { "name", "chains" });
    }

    [Fact]
    public void FromJsonString_FileWithTrackFlips_LoadsWithoutFailing_AndKeepsEverythingElse()
    {
        var loaded = SequenceListSave.FromJsonString(FileWithFlips);

        var tracks = loaded.Sequences.ShouldHaveSingleItem().Tracks;
        tracks.Select(t => (t.Name, t.Visible, t.Chains.Count)).ShouldBe(new[] { ("Body", true, 1), ("Wing", false, 0) });
        tracks[1].UnknownProperties["tag"]!.GetValue<string>().ShouldBe("keep");
    }

    [Fact]
    public void ToJsonString_AfterLoadingAFileWithTrackFlips_WritesNoFlip_NotEvenAsAnUnknownProperty()
    {
        var loaded = SequenceListSave.FromJsonString(FileWithFlips);

        loaded.Sequences[0].Tracks.ShouldAllBe(t => !t.UnknownProperties.ContainsKey("flipHorizontal"));
        var written = WrittenTracks(loaded);
        foreach (var track in written)
            track!.AsObject().ContainsKey("flipHorizontal").ShouldBeFalse();
        written[1]!["tag"]!.GetValue<string>().ShouldBe("keep");
        written[1]!["visible"]!.GetValue<bool>().ShouldBeFalse();
    }

    [Fact]
    public void FromJsonString_AWrongTypedFlip_IsIgnoredToo()
    {
        var loaded = SequenceListSave.FromJsonString("""{ "sequences": [ { "name": "S", "tracks": [ { "name": "T", "flipHorizontal": "yes" } ] } ] }""");

        loaded.Sequences[0].Tracks.ShouldHaveSingleItem().Name.ShouldBe("T");
        WrittenTracks(loaded)[0]!.AsObject().ContainsKey("flipHorizontal").ShouldBeFalse();
    }
}
