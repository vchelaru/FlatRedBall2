using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>A track has no offset: a sprite sits at its frame's offset, whatever an old file holds.</summary>
public class SequenceTrackNoOffsetSceneTests
{
    private static SequenceDocument LoadWithOldOffsets(TestHelpers.TempDir dir)
    {
        var list = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Walk", Loop = true };
        chain.Frames.Add(new AnimationFrameSave { TextureName = "body.png", FrameLength = 0.25f, RelativeX = 5, RelativeY = 3, RightCoordinate = 1, BottomCoordinate = 1 });
        list.AnimationChains.Add(chain);
        list.SaveJson(Path.Combine(dir.Path, "body.achj"));
        string path = Path.Combine(dir.Path, "boss.seqj");
        File.WriteAllText(path,
            "{ \"version\": 1, \"sources\": [ \"body.achj\" ], \"sequences\": [ { \"name\": \"Attack\", \"tracks\": [ " +
            "{ \"name\": \"Body\", \"x\": 100, \"y\": -40, " +
            " \"chains\": [ { \"source\": \"body.achj\", \"chain\": \"Walk\", \"start\": 0, \"loops\": 1 } ] } ] } ] }");
        return SequenceDocument.Load(path);
    }

    [Fact]
    public void Sample_PlacesTheSpriteAtTheFrameOffset_IgnoringAnOldTrackOffsetInTheFile()
    {
        using var dir = new TestHelpers.TempDir();
        var document = LoadWithOldOffsets(dir);

        var sample = SequenceScene.At(document, 0, 0).Single().Sample();

        (sample.OffsetX, sample.OffsetY).ShouldBe((5f, 3f));
    }

}
