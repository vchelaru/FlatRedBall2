using System.Text.Json;
using System.Text.Json.Nodes;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

/// <summary>A track's optional <c>"continuous"</c> flag: false by default, written only when true.</summary>
public class SequenceTrackContinuousTests
{
    private static SequenceListSave SaveWith(bool continuous)
    {
        var save = new SequenceListSave();
        save.Sequences.Add(new SequenceSave { Name = "Action", Tracks = { new SequenceTrackSave { Name = "Moon", Continuous = continuous } } });
        return save;
    }

    [Fact]
    public void Continuous_DefaultsToFalse()
    {
        new SequenceTrackSave().Continuous.ShouldBeFalse();
    }

    [Fact]
    public void FromJsonString_NoContinuousField_ReadsFalse()
    {
        var loaded = SequenceListSave.FromJsonString("""{ "sequences": [ { "tracks": [ { "name": "Moon" } ] } ] }""");

        loaded.Sequences[0].Tracks[0].Continuous.ShouldBeFalse();
    }

    [Fact]
    public void ToJsonString_ContinuousTrack_WritesContinuousTrue_AfterNameAndVisibleBeforeTheChains()
    {
        var track = JsonNode.Parse(SaveWith(continuous: true).ToJsonString())!["sequences"]![0]!["tracks"]![0]!.AsObject();

        track["continuous"]!.GetValue<bool>().ShouldBeTrue();
        track.Select(p => p.Key).ShouldBe(new[] { "name", "continuous", "chains" });
    }

    [Fact]
    public void ToJsonString_OrdinaryTrack_WritesNoContinuousField()
    {
        SaveWith(continuous: false).ToJsonString().ShouldNotContain("continuous");
    }

    [Fact]
    public void RoundTrip_ContinuousStaysTrue_AndFalseStaysFalse()
    {
        SequenceListSave.FromJsonString(SaveWith(continuous: true).ToJsonString()).Sequences[0].Tracks[0].Continuous.ShouldBeTrue();
        SequenceListSave.FromJsonString(SaveWith(continuous: false).ToJsonString()).Sequences[0].Tracks[0].Continuous.ShouldBeFalse();
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
                      "visible": false,
                      "chains": []
                    }
                  ]
                }
              ]
            }
            """;

        SequenceListSave.FromJsonString(json).ToJsonString().ReplaceLineEndings().ShouldBe(json.ReplaceLineEndings());
    }

    [Fact]
    public void ToJsonString_FileWithTheField_IsByteIdenticalAfterALoadAndSave()
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
                  "loop": true,
                  "tracks": [
                    {
                      "name": "Body",
                      "visible": false,
                      "continuous": true,
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
    [InlineData("\"true\"")]
    [InlineData("1")]
    [InlineData("null")]
    public void FromJsonString_ContinuousOfTheWrongType_ThrowsNamingThePath(string value)
    {
        var error = Should.Throw<JsonException>(() =>
            SequenceListSave.FromJsonString($$"""{ "sequences": [ { "tracks": [ { "name": "Moon", "continuous": {{value}} } ] } ] }"""));

        error.Message.ShouldContain("sequences[0].tracks[0].continuous");
    }

    [Fact]
    public void RoundTrip_UnknownTrackPropertiesAlongsideContinuous_StillSurvive_AndContinuousIsNotTreatedAsUnknown()
    {
        var loaded = SequenceListSave.FromJsonString(
            """{ "sequences": [ { "tracks": [ { "name": "Moon", "continuous": true, "zTag": { "keep": 1 } } ] } ] }""");

        var track = loaded.Sequences[0].Tracks[0];
        track.UnknownProperties.Select(p => p.Key).ShouldBe(new[] { "zTag" });
        var written = JsonNode.Parse(loaded.ToJsonString())!["sequences"]![0]!["tracks"]![0]!.AsObject();
        written["continuous"]!.GetValue<bool>().ShouldBeTrue();
        written["zTag"]!["keep"]!.GetValue<int>().ShouldBe(1);
    }
}
