using System.Text.Json;
using System.Text.Json.Nodes;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

public class SequenceListSaveTests
{
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

    [Fact]
    public void Save_ReferenceToMissingSourceAndChain_KeptUnchanged()
    {
        // "Gone.achj" is not in sources and exists nowhere; the format never resolves references, so it keeps them.
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
                      "chains": [
                        {
                          "source": "Gone.achj",
                          "chain": "Deleted",
                          "start": 1.5,
                          "loops": 2
                        }
                      ]
                    }
                  ]
                }
              ]
            }
            """;
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.seqj");
        try
        {
            File.WriteAllText(path, json);

            SequenceListSave.FromFile(path).Save(path);

            File.ReadAllText(path).ReplaceLineEndings().ShouldBe(json.ReplaceLineEndings());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void FromJsonString_MissingOptionalFields_UsesDefaults()
    {
        var loaded = SequenceListSave.FromJsonString("""{ "sequences": [ { "tracks": [ { "chains": [ { "chain": "Walk" } ] } ] } ] }""");

        var reference = loaded.Sequences[0].Tracks[0].Chains.ShouldHaveSingleItem();
        reference.Loops.ShouldBe(1f);
        reference.Start.ShouldBe(0);
        loaded.Sources.ShouldBeEmpty();
    }

    private const string LegacyJson = """{ "sources": [ "A.achj" ], "sequences": [ { "name": "Idle", "tracks": [] } ] }""";

    [Fact]
    public void ToJsonString_NewFile_WritesVersionOneFirst()
    {
        var node = JsonNode.Parse(new SequenceListSave().ToJsonString())!.AsObject();

        node.First().Key.ShouldBe("version");
        node["version"]!.GetValue<int>().ShouldBe(1);
        new SequenceListSave().Version.ShouldBe(SequenceListSave.CurrentVersion);
        SequenceListSave.CurrentVersion.ShouldBe(1);
    }

    [Fact]
    public void FromJsonString_NoVersion_LoadsAsOneAndSavesWithVersionOne()
    {
        var loaded = SequenceListSave.FromJsonString(LegacyJson);

        loaded.Version.ShouldBe(1);
        loaded.IsFromNewerVersion.ShouldBeFalse();
        JsonNode.Parse(loaded.ToJsonString())!["version"]!.GetValue<int>().ShouldBe(1);
    }

    [Fact]
    public void FromJsonString_HigherVersion_LoadsKeepsNumberAndFlagsIt()
    {
        var loaded = SequenceListSave.FromJsonString("""{ "version": 7, "sources": [ "A.achj" ], "sequences": [] }""");

        loaded.Version.ShouldBe(7);
        loaded.IsFromNewerVersion.ShouldBeTrue();
        loaded.Sources.ShouldBe(new[] { "A.achj" });
        JsonNode.Parse(loaded.ToJsonString())!["version"]!.GetValue<int>().ShouldBe(7);
        SequenceListSave.FromJsonString(loaded.ToJsonString()).Version.ShouldBe(7);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-3")]
    [InlineData("\"2\"")]
    [InlineData("1.5")]
    [InlineData("null")]
    public void FromJsonString_InvalidVersion_Throws(string version)
    {
        Should.Throw<JsonException>(() => SequenceListSave.FromJsonString($$"""{ "version": {{version}} }"""));
    }

    // One unknown property of every JSON kind, with a different property order than the alphabetical one.
    private const string UnknownProps = """
        "zString": "hi", "aNumber": 1.25, "mBool": true, "kNull": null,
        "bObject": { "inner": [1, "two", { "deep": false }], "n": null }, "cArray": [3, "x", null, [true]]
        """;

    private static string WithUnknownEverywhere() => $$"""
        {
          "version": 1,
          {{UnknownProps}},
          "sources": [ "A.achj" ],
          "sequences": [
            {
              "name": "Idle", "loop": true,
              {{UnknownProps}},
              "tracks": [
                {
                  "name": "Body", "x": 1, "y": 2, "z": 3,
                  {{UnknownProps}},
                  "chains": [
                    { "source": "A.achj", "chain": "Walk", "start": 0.5, "loops": 2, {{UnknownProps}} }
                  ]
                }
              ]
            }
          ]
        }
        """;

    private static readonly string[] UnknownKeysInOrder = { "zString", "aNumber", "mBool", "kNull", "bObject", "cArray" };

    [Fact]
    public void RoundTrip_UnknownPropertiesAtEveryLevel_Survive()
    {
        var first = SequenceListSave.FromJsonString(WithUnknownEverywhere()).ToJsonString();
        var second = SequenceListSave.FromJsonString(first).ToJsonString();

        second.ShouldBe(first);
        var expected = JsonNode.Parse("{" + UnknownProps + "}")!.AsObject();
        var root = JsonNode.Parse(second)!.AsObject();
        var sequence = root["sequences"]![0]!.AsObject();
        var track = sequence["tracks"]![0]!.AsObject();
        var chain = track["chains"]![0]!.AsObject();
        foreach (var level in new[] { root, sequence, track, chain })
        {
            level.Select(p => p.Key).Where(k => UnknownKeysInOrder.Contains(k)).ShouldBe(UnknownKeysInOrder);
            foreach (var key in UnknownKeysInOrder)
                JsonNode.DeepEquals(level[key], expected[key]).ShouldBeTrue($"{key} changed");
        }
    }

    [Fact]
    public void ToJsonString_UnknownPropertiesWrittenAfterKnownOnes()
    {
        var root = JsonNode.Parse(SequenceListSave.FromJsonString(WithUnknownEverywhere()).ToJsonString())!.AsObject();
        var sequence = root["sequences"]![0]!.AsObject();
        var track = sequence["tracks"]![0]!.AsObject();
        var chain = track["chains"]![0]!.AsObject();

        root.Select(p => p.Key).Take(3).ShouldBe(new[] { "version", "sources", "sequences" });
        sequence.Select(p => p.Key).Take(3).ShouldBe(new[] { "name", "loop", "tracks" });
        track.Select(p => p.Key).Take(5).ShouldBe(new[] { "name", "x", "y", "z", "chains" });
        chain.Select(p => p.Key).Take(4).ShouldBe(new[] { "source", "chain", "start", "loops" });
    }

    [Fact]
    public void ToJsonString_CalledTwice_ByteIdentical()
    {
        var loaded = SequenceListSave.FromJsonString(WithUnknownEverywhere());

        loaded.ToJsonString().ShouldBe(loaded.ToJsonString());
    }

    [Fact]
    public void Save_UnknownProperties_SurviveFileRoundTrip()
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.seqj");
        try
        {
            SequenceListSave.FromJsonString(WithUnknownEverywhere()).Save(path);
            string first = File.ReadAllText(path);
            SequenceListSave.FromFile(path).Save(path);

            File.ReadAllText(path).ShouldBe(first);
            first.ShouldContain("\"deep\": false");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Edit_KnownFieldsAfterLoad_KeepsUnknownOnTheEditedObject()
    {
        var loaded = SequenceListSave.FromJsonString(WithUnknownEverywhere());
        var chain = loaded.Sequences[0].Tracks[0].Chains[0];
        chain.Start = 9;
        loaded.Sequences[0].Tracks[0].X = 4;

        var chainNode = JsonNode.Parse(loaded.ToJsonString())!["sequences"]![0]!["tracks"]![0]!["chains"]![0]!;
        chainNode["start"]!.GetValue<double>().ShouldBe(9);
        chainNode["bObject"]!["inner"]![2]!["deep"]!.GetValue<bool>().ShouldBeFalse();
    }

    [Fact]
    public void UnknownProperties_AreIndependentOfTheSourceDocument()
    {
        // Serializing must not move the stored nodes into the output tree (a JsonNode has one parent).
        var loaded = SequenceListSave.FromJsonString(WithUnknownEverywhere());

        var a = loaded.ToJsonString();
        loaded.Sequences[0].UnknownProperties["extra"] = 5;
        var b = loaded.ToJsonString();

        a.ShouldNotContain("\"extra\"");
        b.ShouldContain("\"extra\": 5");
    }
}
