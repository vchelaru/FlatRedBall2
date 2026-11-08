using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>A track has no flip: a sprite sits at its frame's offset and draws with the frame's own flip, whatever an old file holds.</summary>
public class SequenceTrackNoFlipSceneTests
{
    private static SequenceDocument LoadWithOldTrackFlip(TestHelpers.TempDir dir)
    {
        var list = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Walk", Loop = true };
        chain.Frames.Add(new AnimationFrameSave
        {
            TextureName = "body.png", FrameLength = 0.25f, RelativeX = 5, RelativeY = 3, RightCoordinate = 1, BottomCoordinate = 1,
            FlipVertical = true,
        });
        list.AnimationChains.Add(chain);
        list.SaveJson(Path.Combine(dir.Path, "body.achj"));
        string path = Path.Combine(dir.Path, "boss.seqj");
        File.WriteAllText(path,
            "{ \"version\": 1, \"sources\": [ \"body.achj\" ], \"sequences\": [ { \"name\": \"Attack\", \"tracks\": [ " +
            "{ \"name\": \"Body\", \"flipHorizontal\": true, " +
            " \"chains\": [ { \"source\": \"body.achj\", \"chain\": \"Walk\", \"start\": 0, \"loops\": 1 } ] } ] } ] }");
        return SequenceDocument.Load(path);
    }

    [Fact]
    public void Sample_PlacesTheSpriteAtTheFramesOwnOffset_IgnoringAnOldTrackFlipInTheFile()
    {
        using var dir = new TestHelpers.TempDir();
        var sprite = SequenceScene.At(LoadWithOldTrackFlip(dir), 0, 0).Single();

        var sample = sprite.Sample();

        (sample.OffsetX, sample.OffsetY).ShouldBe((5f, 3f));
    }

    [Fact]
    public void ASprite_HasNoTrackFlip_ItsFrameKeepsItsOwnFlip()
    {
        using var dir = new TestHelpers.TempDir();
        var sprite = SequenceScene.At(LoadWithOldTrackFlip(dir), 0, 0).Single();

        typeof(SequenceSprite).GetProperty("FlipMatrix").ShouldBeNull("the frame's own flags draw the sprite, no track matrix replaces them");
        typeof(SequenceSprite).GetProperty("FlipHorizontal").ShouldBeNull();
        (sprite.Frame.FlipHorizontal, sprite.Frame.FlipVertical, sprite.Frame.FlipDiagonal).ShouldBe((false, true, false));
    }
}
