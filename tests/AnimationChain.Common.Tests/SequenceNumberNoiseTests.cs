using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

/// <summary>Written numbers are rounded so double and float arithmetic noise never reaches the file.</summary>
public class SequenceNumberNoiseTests
{
    [Fact]
    public void ToJsonString_NoisyStartAndLength_WritesCleanNumbers()
    {
        var save = new SequenceListSave();
        var track = new SequenceTrackSave { Name = "Moon" };
        track.Chains.Add(new SequenceChainReferenceSave { Start = 3.8000000000000003, Length = 1.2000000000000002 });
        track.Chains.Add(new SequenceChainReferenceSave { Start = 3.784615285579969, Length = 3.7846153846153 });
        save.Sequences.Add(new SequenceSave { Name = "Action", Tracks = { track } });

        string json = save.ToJsonString();

        json.ShouldContain("\"start\": 3.8,");
        json.ShouldContain("\"start\": 3.784615,");
        json.ShouldContain("\"length\": 1.2");
        json.ShouldContain("\"length\": 3.784615");
        json.ShouldNotContain("0000000");
    }
}
