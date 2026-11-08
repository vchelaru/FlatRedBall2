using System.Linq;
using System.Text.Json.Nodes;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

/// <summary>A track has no Z: draw order is lane order. Files written before that still hold a <c>"z"</c>, which is dropped.</summary>
public class SequenceTrackNoZTests
{
    private const string FileWithZ = """
        { "version": 1, "sources": [ "Boss.achj" ], "sequences": [ { "name": "Idle", "tracks": [
          { "name": "Body", "x": 1, "y": 2, "z": 3, "chains": [ { "source": "Boss.achj", "chain": "Walk", "start": 0, "loops": 1 } ] },
          { "name": "Wing", "x": 0, "y": 0, "z": -7, "tag": "keep", "chains": [] } ] } ] }
        """;

    [Fact]
    public void FromJsonString_FileWithAZPerTrack_LoadsWithoutFailing_AndKeepsEverythingElse()
    {
        var loaded = SequenceListSave.FromJsonString(FileWithZ);

        var tracks = loaded.Sequences.ShouldHaveSingleItem().Tracks;
        tracks.Select(t => (t.Name, t.Chains.Count)).ShouldBe(new[] { ("Body", 1), ("Wing", 0) });
        tracks[1].UnknownProperties["tag"]!.GetValue<string>().ShouldBe("keep");
    }

    [Fact]
    public void FromJsonString_AWrongTypedZ_IsIgnoredToo()
    {
        var loaded = SequenceListSave.FromJsonString("""{ "sequences": [ { "name": "S", "tracks": [ { "name": "T", "z": "high" } ] } ] }""");

        loaded.Sequences[0].Tracks.ShouldHaveSingleItem().Name.ShouldBe("T");
    }

    [Fact]
    public void ToJsonString_AfterLoadingAFileWithZ_WritesNoZ_NotEvenAsAnUnknownProperty()
    {
        var loaded = SequenceListSave.FromJsonString(FileWithZ);

        var tracks = loaded.Sequences[0].Tracks;
        tracks.ShouldAllBe(t => !t.UnknownProperties.ContainsKey("z"));
        var written = JsonNode.Parse(loaded.ToJsonString())!["sequences"]![0]!["tracks"]!.AsArray();
        foreach (var track in written)
            track!.AsObject().ContainsKey("z").ShouldBeFalse();
        written[1]!["tag"]!.GetValue<string>().ShouldBe("keep");
    }

    [Fact]
    public void ToJsonString_OfANewTrack_HasNoZ()
    {
        var save = new SequenceListSave();
        save.Sequences.Add(new SequenceSave { Name = "S", Tracks = { new SequenceTrackSave { Name = "T" } } });

        save.ToJsonString().ShouldNotContain("\"z\"");
        save.ToJsonString().ShouldNotContain("\"Z\"");
    }

    [Fact]
    public void ToJsonString_KeepsTheKeyOrderWithoutZ()
    {
        var save = SequenceListSave.FromJsonString(FileWithZ);

        var keys = JsonNode.Parse(save.ToJsonString())!["sequences"]![0]!["tracks"]![0]!.AsObject().Select(p => p.Key);

        keys.ShouldBe(new[] { "name", "chains" });
    }
}
