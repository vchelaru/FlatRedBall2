using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>Which track's sprite a click in the sequence preview lands on.</summary>
public class TrackHitTestTests
{
    [Fact]
    public void TopmostAt_OfOverlappingBoxes_IsTheOneDrawnLast()
    {
        var back = new SpriteBox(0, 1, 50, 50, 20, 20);
        var front = new SpriteBox(0, 0, 60, 60, 20, 20);

        TrackHitTest.TopmostAt(new[] { back, front }, 55, 55, sequence: 0).ShouldBe(front);
        TrackHitTest.TopmostAt(new[] { back, front }, 35, 35, sequence: 0).ShouldBe(back);
        TrackHitTest.TopmostAt(new[] { back, front }, 100, 100, sequence: 0).ShouldBeNull();
    }

    [Fact]
    public void TopmostAt_SpritesOfAnotherSequence_NeitherBlockNorTakeTheClick()
    {
        var mine = new SpriteBox(0, 0, 50, 50, 20, 20);
        var other = new SpriteBox(1, 0, 50, 50, 30, 30);

        TrackHitTest.TopmostAt(new[] { mine, other }, 50, 50, sequence: 0).ShouldBe(mine);
        TrackHitTest.TopmostAt(new[] { other }, 50, 50, sequence: 0).ShouldBeNull();
    }

    // ── Where the preview draws a sprite, flipped or not ─────────────────────

    private static SequenceDocument Load(TestHelpers.TempDir dir, bool flip, int tracks = 1)
    {
        string source = Path.Combine(dir.Path, "body.achj");
        var list = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Walk" };
        // Only the offset matters here: 8 right, 3 up.
        chain.Frames.Add(new AnimationFrameSave { TextureName = "body.png", FrameLength = 0.25f, RightCoordinate = 1, BottomCoordinate = 1, RelativeX = 8, RelativeY = 3 });
        list.AnimationChains.Add(chain);
        list.SaveJson(source);
        var save = new SequenceListSave { Sources = { "body.achj" } };
        var sequence = new SequenceSave { Name = "Attack" };
        for (int i = 0; i < tracks; i++)
        {
            var track = new SequenceTrackSave { Name = i == 0 ? "Body" : "Fx" + i, FlipHorizontal = flip };
            track.Chains.Add(new SequenceChainReferenceSave { Source = "body.achj", Chain = "Walk", Start = 0, Loops = 1 });
            sequence.Tracks.Add(track);
        }
        save.Sequences.Add(sequence);
        string path = Path.Combine(dir.Path, "boss.seqj");
        save.Save(path);
        return SequenceDocument.Load(path);
    }

    [Theory]
    [InlineData(false, 8)]
    [InlineData(true, -8)]
    public void SpriteBox_IsWhereThePreviewDrawsIt_TheFrameOffsetMirroredOnAFlippedTrack(bool flip, float expectedWorldX)
    {
        using var dir = new TestHelpers.TempDir();
        var sprite = SequenceScene.At(Load(dir, flip), 0, 0.1).Single();

        var box = SpriteBox.Of(sprite, 16, 16, zoom: 2, offsetMultiplier: 1, originX: 100, originY: 200);

        box.CenterX.ShouldBe(100 + expectedWorldX * 2);
        box.CenterY.ShouldBe(200 - 3 * 2);
        (box.HalfWidth, box.HalfHeight).ShouldBe((16.0, 16.0));
    }

    [Fact]
    public void TopmostAt_TwoTracksOnTopOfEachOther_PicksTheTopLane()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, flip: false, tracks: 2);

        var boxes = SequenceScene.At(document, 0, 0.1)
            .Select(sprite => SpriteBox.Of(sprite, 16, 16, zoom: 1, offsetMultiplier: 1, originX: 0, originY: 0)).ToList();
        var hit = TrackHitTest.TopmostAt(boxes, 8, -3, sequence: 0);

        // Track 0 is the top lane, so it is drawn last and wins over the track below it.
        hit!.Value.Track.ShouldBe(0);
    }
}
