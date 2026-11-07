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
}
