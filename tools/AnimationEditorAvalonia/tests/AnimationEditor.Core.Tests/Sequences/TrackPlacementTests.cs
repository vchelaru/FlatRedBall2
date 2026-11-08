using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>The pure math behind dragging and nudging a track in the sequence preview.</summary>
public class TrackPlacementTests
{
    [Theory]
    [InlineData(2, 2, false)]
    [InlineData(3, 0, true)]
    [InlineData(0, -3, true)]
    [InlineData(-2.9, 2.9, false)]
    public void PastDragThreshold_IsThreePixelsOnEitherAxis(double dx, double dy, bool expected) =>
        TrackPlacement.PastDragThreshold(dx, dy).ShouldBe(expected);

    [Fact]
    public void ScreenToWorld_DividesByZoomAndOffsetMultiplier_AndTurnsScreenYUp()
    {
        TrackPlacement.ScreenToWorld(10, 6, zoom: 2, offsetMultiplier: 1).ShouldBe((5f, -3f));
        TrackPlacement.ScreenToWorld(10, -6, zoom: 4, offsetMultiplier: 0.5).ShouldBe((5f, 3f));
    }

    [Theory]
    [InlineData(0.4f, 0f)]
    [InlineData(0.5f, 1f)]
    [InlineData(-0.5f, -1f)]
    [InlineData(-1.4f, -1f)]
    [InlineData(7f, 7f)]
    public void SnapToPixel_RoundsHalvesAwayFromZero_LikeTheAchxFrameDrag(float value, float expected) =>
        TrackPlacement.SnapToPixel(value).ShouldBe(expected);

    [Fact]
    public void Dragged_EveryTrackMovesByTheSameDelta_AndLandsOnWholePixels()
    {
        var moved = TrackPlacement.Dragged(new[] { (0.3f, 10f), (-20f, 4.5f) }, 2.4f, -1f, axisLock: false);

        moved.ShouldBe(new[] { (3f, 9f), (-18f, 4f) });
    }

    [Fact]
    public void Dragged_WithShift_KeepsOnlyTheLargerAxisOfTheDelta()
    {
        TrackPlacement.Dragged(new[] { (0f, 0f) }, 9f, 2f, axisLock: true).ShouldBe(new[] { (9f, 0f) });
        TrackPlacement.Dragged(new[] { (0f, 0f) }, 2f, -9f, axisLock: true).ShouldBe(new[] { (0f, -9f) });
    }

    [Theory]
    [InlineData(NudgeDirection.Left, false, -1f, 0f)]
    [InlineData(NudgeDirection.Right, false, 1f, 0f)]
    [InlineData(NudgeDirection.Up, false, 0f, 1f)]
    [InlineData(NudgeDirection.Down, false, 0f, -1f)]
    [InlineData(NudgeDirection.Right, true, 10f, 0f)]
    [InlineData(NudgeDirection.Down, true, 0f, -10f)]
    public void NudgeStep_IsOneUnit_TenWithShift_AndUpIsPositiveY(NudgeDirection direction, bool shift, float dx, float dy) =>
        TrackPlacement.NudgeStep(direction, shift).ShouldBe((dx, dy));

    [Fact]
    public void TopmostAt_OfOverlappingBoxes_IsTheOneDrawnLast()
    {
        var back = new SpriteBox(0, 1, 50, 50, 20, 20);
        var front = new SpriteBox(0, 0, 60, 60, 20, 20);

        TrackPlacement.TopmostAt(new[] { back, front }, 55, 55, sequence: 0).ShouldBe(front);
        TrackPlacement.TopmostAt(new[] { back, front }, 35, 35, sequence: 0).ShouldBe(back);
        TrackPlacement.TopmostAt(new[] { back, front }, 100, 100, sequence: 0).ShouldBeNull();
    }

    [Fact]
    public void TopmostAt_SpritesOfAnotherSequence_NeitherBlockNorTakeThePress()
    {
        var mine = new SpriteBox(0, 0, 50, 50, 20, 20);
        var other = new SpriteBox(1, 0, 50, 50, 30, 30);

        TrackPlacement.TopmostAt(new[] { mine, other }, 50, 50, sequence: 0).ShouldBe(mine);
        TrackPlacement.TopmostAt(new[] { other }, 50, 50, sequence: 0).ShouldBeNull();
    }

    // ── Where the preview draws a sprite, flipped or not ─────────────────────

    private static SequenceDocument Load(TestHelpers.TempDir dir, bool flip)
    {
        string source = Path.Combine(dir.Path, "body.achj");
        var list = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Walk" };
        // 16 by 16 frame (UV 0..1 of a 16 px texture is resolved by the caller; only the offset matters here), offset 8 to the right.
        chain.Frames.Add(new AnimationFrameSave { TextureName = "body.png", FrameLength = 0.25f, RightCoordinate = 1, BottomCoordinate = 1, RelativeX = 8, RelativeY = 3 });
        list.AnimationChains.Add(chain);
        list.SaveJson(source);
        var save = new SequenceListSave { Sources = { "body.achj" } };
        var sequence = new SequenceSave { Name = "Attack" };
        var track = new SequenceTrackSave { Name = "Body", X = 10, Y = 20, FlipHorizontal = flip };
        track.Chains.Add(new SequenceChainReferenceSave { Source = "body.achj", Chain = "Walk", Start = 0, Loops = 1 });
        sequence.Tracks.Add(track);
        save.Sequences.Add(sequence);
        string path = Path.Combine(dir.Path, "boss.seqj");
        save.Save(path);
        return SequenceDocument.Load(path);
    }

    [Theory]
    [InlineData(false, 10 + 8)]
    [InlineData(true, 10 - 8)]
    public void SpriteBox_IsWhereThePreviewDrawsIt_TheFrameOffsetMirroredOnAFlippedTrack(bool flip, float expectedWorldX)
    {
        using var dir = new TestHelpers.TempDir();
        var sprite = SequenceScene.At(Load(dir, flip), 0, 0.1).Single();

        var box = SpriteBox.Of(sprite, 16, 16, zoom: 2, offsetMultiplier: 1, originX: 100, originY: 200);

        box.CenterX.ShouldBe(100 + expectedWorldX * 2);
        box.CenterY.ShouldBe(200 - (20 + 3) * 2);
        (box.HalfWidth, box.HalfHeight).ShouldBe((16.0, 16.0));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DraggingByAScreenDelta_MovesTheBoxByExactlyThatDelta_FlippedOrNot(bool flip)
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, flip);
        var before = SpriteBox.Of(SequenceScene.At(document, 0, 0.1).Single(), 16, 16, zoom: 2, offsetMultiplier: 1, originX: 0, originY: 0);

        // The pointer travels 10 right and 6 down on screen at 2x zoom: 5 right, 3 down in the world.
        var (dx, dy) = TrackPlacement.ScreenToWorld(10, 6, zoom: 2, offsetMultiplier: 1);
        var dragged = TrackPlacement.Dragged(new[] { (10f, 20f) }, dx, dy, axisLock: false).Single();
        document.Save.Sequences[0].Tracks[0].X = dragged.X;
        document.Save.Sequences[0].Tracks[0].Y = dragged.Y;
        var after = SpriteBox.Of(SequenceScene.At(document, 0, 0.1).Single(), 16, 16, zoom: 2, offsetMultiplier: 1, originX: 0, originY: 0);

        (after.CenterX - before.CenterX, after.CenterY - before.CenterY).ShouldBe((10.0, 6.0));
    }

    [Fact]
    public void TopmostAt_TwoTracksOnTopOfEachOther_PicksTheTopLane()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, flip: false);
        var second = new SequenceTrackSave { Name = "Fx", X = 10, Y = 20 };
        second.Chains.Add(new SequenceChainReferenceSave { Source = "body.achj", Chain = "Walk", Start = 0, Loops = 1 });
        document.Save.Sequences[0].Tracks.Add(second);

        var boxes = SequenceScene.At(document, 0, 0.1)
            .Select(sprite => SpriteBox.Of(sprite, 16, 16, zoom: 1, offsetMultiplier: 1, originX: 0, originY: 0)).ToList();
        var hit = TrackPlacement.TopmostAt(boxes, 18, -23, sequence: 0);

        // Track 0 is the top lane, so it is drawn last and wins over the track below it.
        hit!.Value.Track.ShouldBe(0);
    }
}
