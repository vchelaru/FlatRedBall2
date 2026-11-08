using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>
/// Draw order is lane order and nothing else: a track has no Z, the top lane is in front and the bottom lane is at the back.
/// The fixtures are raw JSON so they can carry the <c>"z"</c> old files hold, which must change nothing.
/// </summary>
public class SequenceLaneOrderTests
{
    // Three tracks, each playing hero's Walk from 0. The z values are in the opposite order to the lanes for A and B, so the old
    // draw-by-Z rule would put B behind A, and C between them.
    private const string ThreeTracks = """
        { "version": 1, "sources": [ "hero.achj" ], "sequences": [ { "name": "Idle", "tracks": [
          { "name": "A", "z": 9, "chains": [ { "source": "hero.achj", "chain": "Walk", "start": 0, "loops": 1 } ] },
          { "name": "B", "z": 0, "chains": [ { "source": "hero.achj", "chain": "Walk", "start": 0, "loops": 1 } ] },
          { "name": "C", "z": 4, "chains": [ { "source": "hero.achj", "chain": "Walk", "start": 0, "loops": 1 } ] } ] } ] }
        """;

    private static SequenceDocument Open(TestHelpers.TempDir dir, string seqj)
    {
        var chains = new AnimationChainListSave();
        var walk = new AnimationChainSave { Name = "Walk", Loop = true };
        walk.Frames.Add(new AnimationFrameSave { TextureName = "hero.png", FrameLength = 0.5f, RightCoordinate = 1, BottomCoordinate = 1 });
        chains.AnimationChains.Add(walk);
        chains.SaveJson(Path.Combine(dir.Path, "hero.achj"));
        string path = Path.Combine(dir.Path, "boss.seqj");
        File.WriteAllText(path, seqj);
        return SequenceDocument.Load(path);
    }

    /// <summary>The names of the tracks drawn at time 0, back to front (the bottom lane first).</summary>
    private static string[] DrawOrder(SequenceDocument document)
    {
        var tracks = document.Save.Sequences[0].Tracks;
        return SequenceScene.At(document, 0, 0).Select(sprite => tracks[sprite.Track].Name).ToArray();
    }

    private static string[] Lanes(SequenceDocument document) => document.Save.Sequences[0].Tracks.Select(t => t.Name).ToArray();

    [Fact]
    public void At_DrawsTracksInLaneOrder_TopLaneInFront_WhateverZTheFileHolds()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, ThreeTracks);

        DrawOrder(document).ShouldBe(new[] { "C", "B", "A" });
    }

    [Fact]
    public void At_TheTopLaneCoversTheLanesBelowIt_AndReversingTheFileTracksFlipsThat()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, ThreeTracks);
        var sprites = SequenceScene.At(document, 0, 0);

        sprites.Last().Track.ShouldBe(0, "the last sprite drawn is on top: the first track of the file");
        sprites.First().Track.ShouldBe(2);

        document.Save.Sequences[0].Tracks.Reverse();

        var flipped = SequenceScene.At(document, 0, 0);
        flipped.Last().Track.ShouldBe(0);
        document.Save.Sequences[0].Tracks[flipped.Last().Track].Name.ShouldBe("C", "C is now the first track, so it covers the rest");
        document.Save.Sequences[0].Tracks[flipped.First().Track].Name.ShouldBe("A");
    }

    [Fact]
    public void ReorderingLanes_IsAPureReorder_OneUndoEntry_NoOtherPropertyChanges_AndTheSavedFileHasNoZ()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, ThreeTracks);
        var tracks = document.Save.Sequences[0].Tracks;
        tracks[0].FlipHorizontal = true; tracks[0].Visible = false;
        var undo = new UndoManager();

        // Drag A from the top lane to below the last one.
        document.MoveTrack(undo, 0, from: 0, slot: 3).ShouldBe(2);

        Lanes(document).ShouldBe(new[] { "B", "C", "A" });
        DrawOrder(document).ShouldBe(new[] { "C", "B" }, "A is hidden, so it is not drawn");
        var moved = document.Save.Sequences[0].Tracks[2];
        (moved.FlipHorizontal, moved.Visible).ShouldBe((true, false));
        undo.UndoHistory.Select(c => c.Description).ShouldBe(new[] { "Move Track 'A'" });
        File.ReadAllText(Path.Combine(dir.Path, "boss.seqj")).ShouldNotContain("\"z\"");

        undo.Undo();
        Lanes(document).ShouldBe(new[] { "A", "B", "C" });
    }

    [Fact]
    public void PasteTracks_LandAtThePastePosition_AndDrawByLane()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, ThreeTracks);
        TimelineClipboard.TryRead(document.CopyTracks(0, new[] { 2 }), out var content, out _).ShouldBeTrue();

        document.PasteTracks(new UndoManager(), 0, content!, afterTrack: 0);

        Lanes(document).ShouldBe(new[] { "A", "C 2", "B", "C" });
        DrawOrder(document).ShouldBe(new[] { "C", "B", "C 2", "A" });
    }

    [Fact]
    public void DuplicateTracks_LandRightBelowTheOriginal_AndDrawByLane()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, ThreeTracks);

        document.DuplicateTracks(new UndoManager(), 0, new[] { 0 });

        Lanes(document).ShouldBe(new[] { "A", "A 2", "B", "C" });
        DrawOrder(document).ShouldBe(new[] { "C", "B", "A 2", "A" });
    }

    [Fact]
    public void PasteChains_ThatMakeANewTrack_PutItInTheFirstLane_InFront()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir, ThreeTracks);
        TimelineClipboard.TryRead(document.CopyChains(0, new[] { new TimelineChainId(1, 0) }), out var content, out _).ShouldBeTrue();
        document.Save.Sequences[0].Tracks.RemoveAt(1);

        // B no longer exists, so the paste makes it on top of the lanes.
        document.PasteChains(new UndoManager(), 0, content!, selectedTrack: null, time: 0);

        Lanes(document).ShouldBe(new[] { "B", "A", "C" });
        DrawOrder(document).ShouldBe(new[] { "C", "A", "B" });
    }
}
