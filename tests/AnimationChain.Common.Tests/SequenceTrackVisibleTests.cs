using System.Text.Json;
using System.Text.Json.Nodes;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

/// <summary>A track's optional <c>"visible"</c> flag: true by default, written only when false.</summary>
public class SequenceTrackVisibleTests
{
    private static SequenceListSave SaveWith(bool visible)
    {
        var save = new SequenceListSave();
        save.Sequences.Add(new SequenceSave { Name = "Action", Tracks = { new SequenceTrackSave { Name = "Moon", Visible = visible } } });
        return save;
    }

    [Fact]
    public void Visible_DefaultsToTrue()
    {
        new SequenceTrackSave().Visible.ShouldBeTrue();
    }

    [Fact]
    public void FromJsonString_NoVisibleField_ReadsTrue()
    {
        var loaded = SequenceListSave.FromJsonString("""{ "sequences": [ { "tracks": [ { "name": "Moon" } ] } ] }""");

        loaded.Sequences[0].Tracks[0].Visible.ShouldBeTrue();
    }

    [Fact]
    public void ToJsonString_HiddenTrack_WritesVisibleFalse_AfterTheKnownFieldsBeforeTheChains()
    {
        var track = JsonNode.Parse(SaveWith(visible: false).ToJsonString())!["sequences"]![0]!["tracks"]![0]!.AsObject();

        track["visible"]!.GetValue<bool>().ShouldBeFalse();
        track.Select(p => p.Key).ShouldBe(new[] { "name", "x", "y", "z", "visible", "chains" });
    }

    [Fact]
    public void ToJsonString_VisibleTrack_WritesNoVisibleField()
    {
        SaveWith(visible: true).ToJsonString().ShouldNotContain("visible");
    }

    [Fact]
    public void RoundTrip_HiddenTrack_StaysHidden_AndVisibleTrackStaysVisible()
    {
        SequenceListSave.FromJsonString(SaveWith(visible: false).ToJsonString()).Sequences[0].Tracks[0].Visible.ShouldBeFalse();
        SequenceListSave.FromJsonString(SaveWith(visible: true).ToJsonString()).Sequences[0].Tracks[0].Visible.ShouldBeTrue();
    }

    [Fact]
    public void ToJsonString_FileWithoutTheField_IsByteIdenticalAfterALoadAndSave()
    {
        const string json = """
            {
              "version": 1,
              "sources": [
                "Boss.achj"
              ],
              "sequences": [
                {
                  "name": "Idle",
                  "loop": false,
                  "tracks": [
                    {
                      "name": "Body",
                      "x": 0,
                      "y": 0,
                      "z": 0,
                      "chains": []
                    }
                  ]
                }
              ]
            }
            """;

        SequenceListSave.FromJsonString(json).ToJsonString().ReplaceLineEndings().ShouldBe(json.ReplaceLineEndings());
    }

    [Theory]
    [InlineData("\"false\"")]
    [InlineData("0")]
    [InlineData("null")]
    public void FromJsonString_VisibleOfTheWrongType_ThrowsNamingThePath(string value)
    {
        var error = Should.Throw<JsonException>(() =>
            SequenceListSave.FromJsonString($$"""{ "sequences": [ { "tracks": [ { "name": "Moon", "visible": {{value}} } ] } ] }"""));

        error.Message.ShouldContain("sequences[0].tracks[0].visible");
    }

    [Fact]
    public void RoundTrip_UnknownTrackPropertiesAlongsideVisible_StillSurvive_AndVisibleIsNotTreatedAsUnknown()
    {
        var loaded = SequenceListSave.FromJsonString(
            """{ "sequences": [ { "tracks": [ { "name": "Moon", "visible": false, "zTag": { "keep": 1 } } ] } ] }""");

        var track = loaded.Sequences[0].Tracks[0];
        track.UnknownProperties.Select(p => p.Key).ShouldBe(new[] { "zTag" });
        var written = JsonNode.Parse(loaded.ToJsonString())!["sequences"]![0]!["tracks"]![0]!.AsObject();
        written["visible"]!.GetValue<bool>().ShouldBeFalse();
        written["zTag"]!["keep"]!.GetValue<int>().ShouldBe(1);
    }
}
