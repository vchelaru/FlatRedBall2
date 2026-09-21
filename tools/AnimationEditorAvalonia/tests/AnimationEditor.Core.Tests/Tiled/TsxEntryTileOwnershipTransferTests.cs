using AnimationEditor.Core;
using AnimationEditor.Core.Tests;
using DotTiled;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.IO;
using System.Linq;
using FilePath = AnimationEditor.Core.Paths.FilePath;
using Xunit;

namespace AnimationEditor.Core.Tests.Tiled;

/// <summary>
/// A chain's "entry tile" (<c>ProjectManager._tsxEntryTileIdsByChain</c>) pins which physical
/// on-disk tile carries a chain's <c>&lt;animation&gt;</c> block across saves, so a hand-authored
/// file where the owner isn't its own frame-0 tile doesn't get silently relocated (see
/// <see cref="MultiTileToTiledAnimationMapper"/>'s <c>knownEntryTileIds</c> doc comment).
/// <para>
/// Resizing frame 0 so its own top-left cell moves (growing or shrinking its left or top edge)
/// invalidates that pin: the physical tile the hint points to is no longer frame 0's own
/// top-left cell. Growing produces an unconditional, provable collision (the stale hint's tile id
/// becomes identical to a freshly-computed satellite's own tile id -- two different
/// <c>&lt;animation&gt;</c> sequences would be written onto the same tile). Shrinking just
/// orphans the hint (its tile id no longer appears anywhere in the chain's own footprint at all),
/// which is indistinguishable from a legitimate hand-authored owner tile without knowing whether
/// the hint was ever *auto-derived* by a prior save (safe to relocate) as opposed to loaded
/// as-is from the file (must never be silently discarded).
/// </para>
/// </summary>
public class TsxEntryTileOwnershipTransferTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    // 4 columns, 16x16 tiles, 8 rows (tilecount=32, image 64x128).
    private const string SingleTileFixtureXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="32" columns="4">
         <image source="Heroes.png" width="64" height="128"/>
         <tile id="1">
          <animation>
           <frame tileid="1" duration="100"/>
           <frame tileid="5" duration="100"/>
          </animation>
         </tile>
        </tileset>
        """;

    private string WriteFixture(string xml, string fileName)
    {
        var path = Path.Combine(_dir.Path, fileName);
        File.WriteAllText(path, xml);
        return path;
    }

    /// <summary>Sets a frame's rect from tile-grid coordinates (columns/rows, 0-based, end-exclusive)
    /// against a 4-column x 8-row, 16px-tile tileset (image 64x128 -- see the fixtures below).</summary>
    private static void SetGridRect(AnimationFrameSave frame, int colStart, int colEnd, int rowStart, int rowEnd)
    {
        const float tileX = 16f / 64f;  // 0.25 UV per tile horizontally (64px-wide image)
        const float tileY = 16f / 128f; // 0.125 UV per tile vertically (128px-tall image)
        frame.LeftCoordinate = colStart * tileX;
        frame.RightCoordinate = colEnd * tileX;
        frame.TopCoordinate = rowStart * tileY;
        frame.BottomCoordinate = rowEnd * tileY;
    }

    [Fact]
    public void GrowingFrameZeroLeftward_CausesEntrySatelliteCollision_TransfersEntryToNewOrigin()
    {
        var pm = new ProjectManager();
        var path = WriteFixture(SingleTileFixtureXml, "Heroes.tsx");
        pm.LoadTsxProject(new FilePath(path));

        var chain = pm.AnimationChainListSave!.AnimationChains.Single();

        // Grow frame 0's left edge out one tile (was tile 1 alone, cols 1..2 -- now cols 0..2,
        // two tiles wide). Frame 1 must match the new footprint too (chain[1] was tile 5, cols
        // 1..2, row 1 -- now cols 0..2, row 1). The OLD anchor (tile 1) is now frame 0's own
        // (1,0)-offset cell -- exactly where a freshly-computed satellite would also land.
        SetGridRect(chain.Frames[0], colStart: 0, colEnd: 2, rowStart: 0, rowEnd: 1);
        SetGridRect(chain.Frames[1], colStart: 0, colEnd: 2, rowStart: 1, rowEnd: 2);

        pm.SaveTsxProject();

        var reloaded = DotTiled.Serialization.Loader.Default().LoadTileset(path);

        // The new origin (tile 0) must carry the anchor sequence -- in the un-fixed collision
        // bug, tile 0 gets no <animation> at all (the stale hint keeps entryTileId pinned to
        // tile 1, and the satellite also resolves to tile 1, so nothing ever targets tile 0).
        var anchorTile = reloaded.Tiles.SingleOrDefault(t => t.ID == 0);
        Assert.NotNull(anchorTile);
        Assert.Equal([((uint)0, 100), ((uint)4, 100)], anchorTile!.Animation.Select(f => (f.TileID, f.Duration)));

        // The old anchor tile (1) now correctly holds the satellite's own sequence, not a
        // second (colliding) copy of the anchor's.
        var satelliteTile = reloaded.Tiles.Single(t => t.ID == 1);
        Assert.Equal([((uint)1, 100), ((uint)5, 100)], satelliteTile.Animation.Select(f => (f.TileID, f.Duration)));
        Assert.Equal(0, satelliteTile.GetProperty<IntProperty>("ParentId").Value);
    }

    [Fact]
    public void ShrinkingFrameZeroLeftward_OnAutoDerivedChain_TransfersEntryToNewOrigin()
    {
        var pm = new ProjectManager();
        // An unrelated existing chain just so the file is a valid tsx project to load.
        var path = WriteFixture(SingleTileFixtureXml, "Heroes.tsx");
        pm.LoadTsxProject(new FilePath(path));

        // A chain created in-session (no hint from the file at all) -- two tiles wide, at rows 2
        // and 3, columns 0..2.
        var chain = new AnimationChainSave { Name = "NewWide" };
        var frame0 = new AnimationFrameSave { TextureName = "Heroes.png", FrameLength = 0.1f };
        var frame1 = new AnimationFrameSave { TextureName = "Heroes.png", FrameLength = 0.1f };
        SetGridRect(frame0, colStart: 0, colEnd: 2, rowStart: 2, rowEnd: 3);
        SetGridRect(frame1, colStart: 0, colEnd: 2, rowStart: 3, rowEnd: 4);
        chain.Frames.Add(frame0);
        chain.Frames.Add(frame1);
        pm.AnimationChainListSave!.AnimationChains.Add(chain);

        // First save: no prior hint exists, so entryTileId is freshly computed from frame 0's own
        // top-left cell (tile 8) -- this is what makes the chain "auto-derived" going forward.
        pm.SaveTsxProject();

        // Shrink frame 0's (and frame 1's) left edge inward by one tile -- both frames drop to a
        // single tile (9 and 13 respectively). The old entry hint (8) is now outside the
        // footprint entirely, not even a satellite.
        SetGridRect(chain.Frames[0], colStart: 1, colEnd: 2, rowStart: 2, rowEnd: 3);
        SetGridRect(chain.Frames[1], colStart: 1, colEnd: 2, rowStart: 3, rowEnd: 4);

        pm.SaveTsxProject();

        var reloaded = DotTiled.Serialization.Loader.Default().LoadTileset(path);

        // The chain's current tile (9) must carry the sequence -- in the un-fixed bug, the stale
        // hint (8) keeps being reused, so tile 9 (what's actually there now) never gets it.
        var currentTile = reloaded.Tiles.SingleOrDefault(t => t.ID == 9);
        Assert.NotNull(currentTile);
        Assert.Equal([((uint)9, 100), ((uint)13, 100)], currentTile!.Animation.Select(f => (f.TileID, f.Duration)));

        // The old (now-orphaned) tile must be cleared, not left holding stale/wrong content.
        var oldTile = reloaded.Tiles.SingleOrDefault(t => t.ID == 8);
        Assert.True(oldTile is null || oldTile.Animation.Count == 0);
    }

    // Deleting the very frame the hint was derived from (and adding a new one elsewhere) changes
    // what frame 0 maps to without any frame's own origin cell moving. That's an add/remove, not
    // a resize -- the pinned tile stays put, exactly like a frame reorder.
    [Fact]
    public void RemovingEntryOriginFrame_OnAutoDerivedChain_PreservesEntryTile()
    {
        var pm = new ProjectManager();
        var path = WriteFixture(SingleTileFixtureXml, "Heroes.tsx");
        pm.LoadTsxProject(new FilePath(path));

        var chain = new AnimationChainSave { Name = "NewChain" };
        var frame0 = new AnimationFrameSave { TextureName = "Heroes.png", FrameLength = 0.1f };
        var frame1 = new AnimationFrameSave { TextureName = "Heroes.png", FrameLength = 0.1f };
        SetGridRect(frame0, colStart: 0, colEnd: 1, rowStart: 1, rowEnd: 2); // tile 4
        SetGridRect(frame1, colStart: 1, colEnd: 2, rowStart: 1, rowEnd: 2); // tile 5
        chain.Frames.Add(frame0);
        chain.Frames.Add(frame1);
        pm.AnimationChainListSave!.AnimationChains.Add(chain);
        pm.SaveTsxProject(); // entry tile 4, derived from frame0

        var frame2 = new AnimationFrameSave { TextureName = "Heroes.png", FrameLength = 0.1f };
        SetGridRect(frame2, colStart: 2, colEnd: 3, rowStart: 1, rowEnd: 2); // tile 6
        chain.Frames.Remove(frame0);
        chain.Frames.Add(frame2);

        pm.SaveTsxProject();

        var reloaded = DotTiled.Serialization.Loader.Default().LoadTileset(path);
        var ownerTile = reloaded.Tiles.Single(t => t.ID == 4);
        Assert.Equal([((uint)5, 100), ((uint)6, 100)], ownerTile.Animation.Select(f => (f.TileID, f.Duration)));
        var relocatedTile = reloaded.Tiles.SingleOrDefault(t => t.ID == 5);
        Assert.True(relocatedTile is null || relocatedTile.Animation.Count == 0);
    }

    // A reorder alone must not transfer (see RemovingEntryOriginFrame... above and
    // ProjectManagerTsxProjectTests.SaveTsxProject_BrandNewChain_EntryTileIdStaysStable...), but
    // it must not disarm a later transfer either: the hint still tracks the frame it was derived
    // from, wherever that frame now sits in the chain, so shrinking that frame's left edge
    // afterward is still an origin move.
    [Fact]
    public void ReorderingThenShrinkingLeftward_OnAutoDerivedChain_TransfersEntryToNewOrigin()
    {
        var pm = new ProjectManager();
        var path = WriteFixture(SingleTileFixtureXml, "Heroes.tsx");
        pm.LoadTsxProject(new FilePath(path));

        var chain = new AnimationChainSave { Name = "NewWide" };
        var frame0 = new AnimationFrameSave { TextureName = "Heroes.png", FrameLength = 0.1f };
        var frame1 = new AnimationFrameSave { TextureName = "Heroes.png", FrameLength = 0.1f };
        SetGridRect(frame0, colStart: 0, colEnd: 2, rowStart: 2, rowEnd: 3); // origin tile 8
        SetGridRect(frame1, colStart: 0, colEnd: 2, rowStart: 3, rowEnd: 4); // origin tile 12
        chain.Frames.Add(frame0);
        chain.Frames.Add(frame1);
        pm.AnimationChainListSave!.AnimationChains.Add(chain);
        pm.SaveTsxProject(); // entry tile 8, derived from frame0

        chain.Frames.Reverse();
        pm.SaveTsxProject(); // still tile 8

        SetGridRect(frame0, colStart: 1, colEnd: 2, rowStart: 2, rowEnd: 3); // tile 9
        SetGridRect(frame1, colStart: 1, colEnd: 2, rowStart: 3, rowEnd: 4); // tile 13
        pm.SaveTsxProject();

        var reloaded = DotTiled.Serialization.Loader.Default().LoadTileset(path);
        var currentTile = reloaded.Tiles.SingleOrDefault(t => t.ID == 13);
        Assert.NotNull(currentTile);
        Assert.Equal([((uint)13, 100), ((uint)9, 100)], currentTile!.Animation.Select(f => (f.TileID, f.Duration)));
        var oldTile = reloaded.Tiles.SingleOrDefault(t => t.ID == 8);
        Assert.True(oldTile is null || oldTile.Animation.Count == 0);
    }

    // Owner tile 20 is purely administrative: its own animation cycles tiles 0/1 and 4/5, none of
    // which is tile 20 itself -- an ordinary hand-authored Tiled pattern (same shape as
    // NativeTsxProjectRoundTripTests' OwnerNotFirstFrameFixtureXml), just with a 2-tile-wide
    // footprint so it can be shrunk. The satellite's ParentId points at the anchor's own static
    // grid position (tile 20 = row 5, col 0), and the satellite itself must sit one column right
    // of *that* (row 5, col 1 = tile 21) -- TiledAnimationToAchjMapper validates the satellite's
    // position against the anchor's own static position, not against its animation content.
    private const string HandAuthoredWideOwnerFixtureXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="32" columns="4">
         <image source="Heroes.png" width="64" height="128"/>
         <tile id="20">
          <animation>
           <frame tileid="0" duration="150"/>
           <frame tileid="4" duration="150"/>
          </animation>
         </tile>
         <tile id="21">
          <properties>
           <property name="ParentId" type="int" value="20"/>
          </properties>
          <animation>
           <frame tileid="1" duration="150"/>
           <frame tileid="5" duration="150"/>
          </animation>
         </tile>
        </tileset>
        """;

    [Fact]
    public void ShrinkingFrameZeroLeftward_OnHandAuthoredChain_PreservesOwnerTile()
    {
        var pm = new ProjectManager();
        var path = WriteFixture(HandAuthoredWideOwnerFixtureXml, "Heroes.tsx");
        pm.LoadTsxProject(new FilePath(path));

        var chain = pm.AnimationChainListSave!.AnimationChains.Single();

        // Shrink frame 0's (and frame 1's) left edge inward by one tile, same transform as the
        // auto-derived test above -- but this chain's entry hint (20) came straight from the
        // file, never from one of our own saves, so it must NOT be transferred.
        SetGridRect(chain.Frames[0], colStart: 1, colEnd: 2, rowStart: 0, rowEnd: 1);
        SetGridRect(chain.Frames[1], colStart: 1, colEnd: 2, rowStart: 1, rowEnd: 2);

        pm.SaveTsxProject();

        var reloaded = DotTiled.Serialization.Loader.Default().LoadTileset(path);

        // The hand-authored owner tile keeps carrying the chain's (now-shrunk) sequence.
        var ownerTile = reloaded.Tiles.SingleOrDefault(t => t.ID == 20);
        Assert.NotNull(ownerTile);
        Assert.Equal([((uint)1, 150), ((uint)5, 150)], ownerTile!.Animation.Select(f => (f.TileID, f.Duration)));

        // No new tile was invented at the chain's current geometric position (tile 1) for the
        // same content -- it stayed on tile 20, exactly as an intentionally-authored file expects.
        var relocatedTile = reloaded.Tiles.SingleOrDefault(t => t.ID == 1);
        Assert.True(relocatedTile is null || relocatedTile.Animation.Count == 0);
    }
}
