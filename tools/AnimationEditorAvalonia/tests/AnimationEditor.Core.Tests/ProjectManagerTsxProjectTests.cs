using AnimationEditor.Core;
using DotTiled;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.IO;
using System.Linq;
using FilePath = AnimationEditor.Core.Paths.FilePath;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class ProjectManagerTsxProjectTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    // 4 columns, 16x16 tiles, one animated tile with no Name property.
    private const string PlainFixtureXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="16" columns="4">
         <image source="Heroes.png" width="64" height="64"/>
         <tile id="0">
          <animation>
           <frame tileid="0" duration="200"/>
           <frame tileid="1" duration="200"/>
          </animation>
         </tile>
        </tileset>
        """;

    // Columns=0 makes every "% columns"/"/ columns" tile-position computation in
    // TiledAnimationToAchjMapper.Map fail loudly (InvalidOperationException) rather than
    // divide-by-zero or wrap into nonsense -- unlike the wangset case below, this throw happens
    // from inside Map, called *after* LoadTsxProject has already assigned _tsxTileset.
    private const string ColumnsZeroFixtureXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Corrupt" tilewidth="16" tileheight="16" tilecount="16" columns="0">
         <image source="Corrupt.png" width="64" height="64"/>
         <tile id="0">
          <animation>
           <frame tileid="0" duration="200"/>
          </animation>
         </tile>
        </tileset>
        """;

    // Tile 5 owns an animation whose frames are [6, 7] -- 5 itself never appears as a frame, an
    // ordinary hand-authored-in-Tiled pattern (same shape as NativeTsxProjectRoundTripTests'
    // OwnerNotFirstFrameFixtureXml). Used to prove a delete-then-undo round trip restores the
    // chain to tile 5, not tile 6.
    private const string OwnerNotFirstFrameFixtureXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="16" columns="4">
         <image source="Heroes.png" width="64" height="64"/>
         <tile id="5">
          <properties>
           <property name="Name" value="RiseUp"/>
          </properties>
          <animation>
           <frame tileid="6" duration="300"/>
           <frame tileid="7" duration="300"/>
          </animation>
         </tile>
        </tileset>
        """;

    // Tile 5's own animation is [9, 13] -- one row below its own static position (row 1 -> rows
    // 2/3), the same "owner isn't its own first frame" pattern as OwnerNotFirstFrameFixtureXml
    // above. Tile 6, one column right of tile 5's STATIC position (not its frame-0 position),
    // carries ParentId=5 as the satellite, with its own on-disk content [10, 14] -- also not its
    // own frame-0 position (10), matching NativeTsxProjectRoundTripTests'
    // OwnerNotFirstFrameWithSatelliteFixtureXml.
    private const string OwnerNotFirstFrameWithSatelliteFixtureXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="64" columns="4">
         <image source="Heroes.png" width="64" height="256"/>
         <tile id="5">
          <animation>
           <frame tileid="9" duration="150"/>
           <frame tileid="13" duration="150"/>
          </animation>
         </tile>
         <tile id="6">
          <properties>
           <property name="ParentId" type="int" value="5"/>
          </properties>
          <animation>
           <frame tileid="10" duration="150"/>
           <frame tileid="14" duration="150"/>
          </animation>
         </tile>
        </tileset>
        """;

    private const string WangsetFixtureXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Terrain" tilewidth="16" tileheight="16" tilecount="16" columns="4">
         <image source="Terrain.png" width="64" height="64"/>
         <wangsets>
          <wangset name="Ground" type="corner" tile="-1">
           <wangcolor name="Grass" color="#00ff00" tile="0" probability="1"/>
           <wangtile tileid="0" wangid="0,1,0,1,0,1,0,1"/>
          </wangset>
         </wangsets>
        </tileset>
        """;

    private string WriteFixture(string xml, string fileName)
    {
        var path = Path.Combine(_dir.Path, fileName);
        File.WriteAllText(path, xml);
        return path;
    }

    [Fact]
    public void LoadTsxProject_PlainTileset_PopulatesAnimationChainListSaveAndTileSize()
    {
        var pm = new ProjectManager();
        var path = WriteFixture(PlainFixtureXml, "Heroes.tsx");

        pm.LoadTsxProject(new FilePath(path));

        Assert.True(pm.IsNativeTsxProject);
        Assert.Equal("ID:0", pm.AnimationChainListSave!.AnimationChains.Single().Name);
        Assert.Equal((16, 16), pm.TsxTileSize);
    }

    // Duplication sweep: LoadAnimationChain resets ReferencedPngs/OnDiskCoordinateType because
    // one ProjectManager instance is reused across File > Open calls (see LoadAnimationChain's own
    // "must not leak into a now-plain achx project" comment for the tsx-side fields it resets).
    // LoadTsxProject never got the symmetric reset, so opening an achx (populating these two
    // fields) and then a tsx in the same tab leaked the achx's stale PNGs into the tsx project --
    // concretely, MainWindow's texture combo unions in ReferencedPngs, so it offered textures from
    // a project that was no longer open.
    [Fact]
    public void LoadTsxProject_PriorAchxLeftReferencedPngsAndCoordinateType_ClearsThemForTheTsxProject()
    {
        var pm = new ProjectManager();
        pm.ReferencedPngs = new[] { new FilePath(Path.Combine(_dir.Path, "StaleFromAchx.png")) };
        pm.OnDiskCoordinateType = TextureCoordinateType.UV;
        var path = WriteFixture(PlainFixtureXml, "Heroes.tsx");

        pm.LoadTsxProject(new FilePath(path));

        Assert.Empty(pm.ReferencedPngs);
        Assert.Equal(TextureCoordinateType.Pixel, pm.OnDiskCoordinateType);
    }

    [Fact]
    public void LoadTsxProject_UnsupportedConstruct_ThrowsAndLeavesProjectUnchanged()
    {
        var pm = new ProjectManager();
        var path = WriteFixture(WangsetFixtureXml, "Terrain.tsx");

        var ex = Assert.Throws<NotSupportedException>(() => pm.LoadTsxProject(new FilePath(path)));

        Assert.Contains("wangsets", ex.Message);
        Assert.False(pm.IsNativeTsxProject);
        Assert.Null(pm.AnimationChainListSave);
    }

    [Fact]
    public void LoadTsxProject_MapThrows_ThrowsAndLeavesProjectUnchanged()
    {
        // Unlike the wangset case above (rejected before _tsxTileset is ever assigned, by the
        // TsxCompatibilityChecker dry-run), a Columns=0 tsx passes that check fine -- TsxWriter
        // never divides by Columns -- so the throw comes from TiledAnimationToAchjMapper.Map,
        // called *after* LoadTsxProject already set _tsxTileset. The load must still be all-or-
        // nothing: IsNativeTsxProject/AnimationChainListSave must reflect "nothing loaded", not a
        // half-applied tileset with no matching chain data.
        var pm = new ProjectManager();
        var path = WriteFixture(ColumnsZeroFixtureXml, "Corrupt.tsx");

        Assert.Throws<InvalidOperationException>(() => pm.LoadTsxProject(new FilePath(path)));

        Assert.False(pm.IsNativeTsxProject);
        Assert.Null(pm.AnimationChainListSave);
    }

    // ── Native-tsx / achx-push coexistence (issue #1147) ────────────────────────────
    // A .tsx cannot be both an achx-push sync target (a .tiledsync association pointing at it)
    // and a native-tsx project at the same time -- SaveTsxProject's load-time snapshot and
    // achx-push's stateless-per-save sync would silently fight over the same file (see the
    // "lost-update" TODO in plan/1147-tsx-sync-hardening/phase-01-bug-sweep.md). Refusing the
    // combination outright, rather than trying to merge them, is the chosen fix.

    [Fact]
    public void LoadTsxProject_TsxAlreadyAssociatedViaTiledSync_ThrowsInsteadOfSilentlyCoexisting()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var tsxPath = WriteFixture(PlainFixtureXml, "Heroes.tsx");
        var achxPath = Path.Combine(_dir.Path, "Hero.achx");
        File.WriteAllText(achxPath, "<AnimationChainListSave/>");
        ctx.IoManager.AddAssociatedTiledTilesetPath(achxPath, tsxPath);

        var ex = Assert.Throws<InvalidOperationException>(
            () => ctx.ProjectManager.LoadTsxProject(new FilePath(tsxPath)));

        Assert.Contains("Hero.achx", ex.Message);
        Assert.False(ctx.ProjectManager.IsNativeTsxProject);
        // The load never got far enough to touch it -- still the fresh, empty ACLS TestServices set up.
        Assert.Same(ctx.Acls, ctx.ProjectManager.AnimationChainListSave);
    }

    [Fact]
    public void LoadTsxProject_TsxAssociatedFromAchjInstead_ThrowsInsteadOfSilentlyCoexisting()
    {
        // Same association mechanism regardless of whether the owning project is .achx or .achj --
        // AddAssociatedTiledTilesetPath doesn't care, and neither should this guard.
        var ctx = TestHelpers.SetupFreshAcls();
        var tsxPath = WriteFixture(PlainFixtureXml, "Heroes.tsx");
        var achjPath = Path.Combine(_dir.Path, "Hero.achj");
        File.WriteAllText(achjPath, "{}");
        ctx.IoManager.AddAssociatedTiledTilesetPath(achjPath, tsxPath);

        var ex = Assert.Throws<InvalidOperationException>(
            () => ctx.ProjectManager.LoadTsxProject(new FilePath(tsxPath)));

        Assert.Contains("Hero.achj", ex.Message);
    }

    [Fact]
    public void LoadTsxProject_NoAssociationAnywhere_OpensNormallyAsBefore()
    {
        // Happy path guard: an unassociated .tsx must keep opening exactly as before -- the new
        // scan must not false-positive on a plain tsx with no .tiledsync anywhere near it.
        var ctx = TestHelpers.SetupFreshAcls();
        var tsxPath = WriteFixture(PlainFixtureXml, "Heroes.tsx");

        ctx.ProjectManager.LoadTsxProject(new FilePath(tsxPath));

        Assert.True(ctx.ProjectManager.IsNativeTsxProject);
    }

    [Fact]
    public void LoadTsxProject_TiledSyncAssociatesADifferentTsx_OpensNormally()
    {
        // A .tiledsync file existing nearby isn't itself a conflict -- only one whose paths
        // actually resolve to THIS tsx should block the load.
        var ctx = TestHelpers.SetupFreshAcls();
        var tsxPath = WriteFixture(PlainFixtureXml, "Heroes.tsx");
        var otherTsxPath = WriteFixture(PlainFixtureXml, "Villains.tsx");
        var achxPath = Path.Combine(_dir.Path, "Hero.achx");
        ctx.IoManager.AddAssociatedTiledTilesetPath(achxPath, otherTsxPath);

        ctx.ProjectManager.LoadTsxProject(new FilePath(tsxPath));

        Assert.True(ctx.ProjectManager.IsNativeTsxProject);
    }

    [Fact]
    public void SaveTsxProject_AfterLoad_WritesAnimationBackToTsxFile()
    {
        var pm = new ProjectManager();
        var path = WriteFixture(PlainFixtureXml, "Heroes.tsx");
        pm.LoadTsxProject(new FilePath(path));

        pm.SaveTsxProject();

        var reloaded = DotTiled.Serialization.Loader.Default().LoadTileset(path);
        var tile = reloaded.Tiles.Single(t => t.ID == 0);
        Assert.Equal([((uint)0, 200), ((uint)1, 200)], tile.Animation.Select(f => (f.TileID, f.Duration)));
    }

    // A chain the user creates in-session (never loaded from disk) has no prior
    // _tsxEntryTileIdsByChain entry, so its first save must compute an entry tile id from its
    // first frame -- but every save after that must reuse the id it committed on that first save,
    // not recompute, even if the chain's own frame order later changes what frame[0] would map to.
    [Fact]
    public void SaveTsxProject_BrandNewChain_EntryTileIdStaysStableAcrossRepeatedSaves()
    {
        var pm = new ProjectManager();
        var path = WriteFixture(PlainFixtureXml, "Heroes.tsx");
        pm.LoadTsxProject(new FilePath(path));

        // Tiles 4 and 5 (row 1) carry no animation in the fixture, so this is a genuinely new chain.
        var newChain = new AnimationChainSave { Name = "NewChain" };
        newChain.Frames.Add(new AnimationFrameSave
        {
            TextureName = "Heroes.png",
            LeftCoordinate = 0f, RightCoordinate = 0.25f,
            TopCoordinate = 0.25f, BottomCoordinate = 0.5f,
            FrameLength = 0.1f,
        });
        newChain.Frames.Add(new AnimationFrameSave
        {
            TextureName = "Heroes.png",
            LeftCoordinate = 0.25f, RightCoordinate = 0.5f,
            TopCoordinate = 0.25f, BottomCoordinate = 0.5f,
            FrameLength = 0.1f,
        });
        pm.AnimationChainListSave!.AnimationChains.Add(newChain);

        pm.SaveTsxProject();

        var afterFirstSave = DotTiled.Serialization.Loader.Default().LoadTileset(path);
        Assert.Equal((uint)4, EntryTileIdNamed(afterFirstSave, "NewChain"));

        // Reverse frame order: frame[0] now maps to tile 5, not tile 4. Without identity tracking
        // this save would relocate the animation from tile 4 to tile 5.
        newChain.Frames.Reverse();
        pm.SaveTsxProject();

        var afterSecondSave = DotTiled.Serialization.Loader.Default().LoadTileset(path);
        Assert.Equal((uint)4, EntryTileIdNamed(afterSecondSave, "NewChain"));
    }

    // A user can delete every frame from a chain via the UI without deleting the chain object
    // itself, leaving an empty AnimationChainSave still present in AnimationChainListSave. That
    // must clear the tile the chain used to own (same as deleting the chain outright would), and
    // must NOT leave a stale _tsxEntryTileIdsByChain entry that a later re-populated save could
    // wrongly reuse.
    [Fact]
    public void SaveTsxProject_AllFramesDeletedFromChain_ClearsPreviouslyOwnedTileAndDoesNotStickOnResave()
    {
        var pm = new ProjectManager();
        var path = WriteFixture(PlainFixtureXml, "Heroes.tsx");
        pm.LoadTsxProject(new FilePath(path));

        var chain = pm.AnimationChainListSave!.AnimationChains.Single();
        Assert.Equal("ID:0", chain.Name);
        chain.Frames.Clear();

        pm.SaveTsxProject();

        var afterClear = DotTiled.Serialization.Loader.Default().LoadTileset(path);
        // Tile 0 only ever existed to carry this chain's animation -- with nothing left after
        // clearing, it must be removed entirely rather than left as a bare <tile id="0"/> stub.
        Assert.DoesNotContain(afterClear.Tiles, t => t.ID == 0);

        // Re-populate the same chain object with frames that map to a different tile (row 1,
        // column 0 -> tile id 4). If the stale tile-0 identity hint lingered, this would either
        // misapply to tile 0 or throw; it must instead be computed fresh from the new geometry.
        chain.Frames.Add(new AnimationFrameSave
        {
            TextureName = "Heroes.png",
            LeftCoordinate = 0f, RightCoordinate = 0.25f,
            TopCoordinate = 0.25f, BottomCoordinate = 0.5f,
            FrameLength = 0.1f,
        });

        pm.SaveTsxProject();

        var afterResave = DotTiled.Serialization.Loader.Default().LoadTileset(path);
        Assert.Equal((uint)4, EntryTileIdNamed(afterResave, "ID:0"));
        // Tile 0 stays gone -- the re-populated chain now maps to tile 4, and nothing recreates
        // a tile-0 stub along the way.
        Assert.DoesNotContain(afterResave.Tiles, t => t.ID == 0);
    }

    // DeleteChainsCommand.Do() removes the chain from AnimationChainListSave.AnimationChains and
    // immediately autosaves (AppCommands.SaveCurrentAnimationChainList runs after every mutating
    // command); DeleteChainsCommand.Undo() re-inserts the exact same AnimationChainSave object
    // and autosaves again. Between those two saves, the chain is entirely absent from `mapped`,
    // and SaveTsxProject's post-save bookkeeping loop rebuilds _tsxEntryTileIdsByChain from
    // `mapped` alone -- so the chain's original tile-identity hint is dropped, not just for the
    // save where it's absent (correct) but permanently, even though the exact same object comes
    // right back afterward. A subsequent save with that same object must restore it to its
    // original tile (5), not relocate it to frame[0]'s tile (6).
    [Fact]
    public void SaveTsxProject_DeleteChainThenReinsertSameObjectAndSave_RestoresOriginalTileInsteadOfRelocating()
    {
        var pm = new ProjectManager();
        var path = WriteFixture(OwnerNotFirstFrameFixtureXml, "Heroes.tsx");
        pm.LoadTsxProject(new FilePath(path));

        var chain = pm.AnimationChainListSave!.AnimationChains.Single();
        Assert.Equal("RiseUp", chain.Name);

        // DeleteChainsCommand.Do().
        pm.AnimationChainListSave.AnimationChains.Remove(chain);
        pm.SaveTsxProject();

        var afterDelete = DotTiled.Serialization.Loader.Default().LoadTileset(path);
        var deletedTile = afterDelete.Tiles.SingleOrDefault(t => t.ID == 5);
        Assert.True(deletedTile is null || deletedTile.Animation.Count == 0);

        // DeleteChainsCommand.Undo(): same object, original index.
        pm.AnimationChainListSave.AnimationChains.Insert(0, chain);
        pm.SaveTsxProject();

        var afterUndo = DotTiled.Serialization.Loader.Default().LoadTileset(path);
        var riseUp = afterUndo.Tiles.SingleOrDefault(t => t.ID == 5);
        Assert.NotNull(riseUp);
        Assert.Equal([((uint)6, 300), ((uint)7, 300)], riseUp!.Animation.Select(f => (f.TileID, f.Duration)));

        var tileSix = afterUndo.Tiles.SingleOrDefault(t => t.ID == 6);
        Assert.True(tileSix is null || tileSix.Animation.Count == 0);
    }

    // The frame-level sibling of the DeleteChainsCommand case above. DeleteFramesCommand.Do()
    // clears every frame from a chain but leaves the AnimationChainSave object itself in
    // AnimationChainListSave -- unlike DeleteChainsCommand, the chain is never absent from the
    // ACLS, so the "carry the hint forward for an absent chain" fix above doesn't apply here.
    // DeleteFramesCommand.Undo() re-inserts the exact same AnimationFrameSave objects at their
    // original indices. A subsequent save with those same objects must restore the chain to its
    // original tile (5), not recompute from frame[0] (6).
    [Fact]
    public void SaveTsxProject_DeleteFramesThenUndoWithSameFrameObjectsAndSave_RestoresOriginalTileInsteadOfRelocating()
    {
        var pm = new ProjectManager();
        var path = WriteFixture(OwnerNotFirstFrameFixtureXml, "Heroes.tsx");
        pm.LoadTsxProject(new FilePath(path));

        var chain = pm.AnimationChainListSave!.AnimationChains.Single();
        Assert.Equal("RiseUp", chain.Name);
        var originalFrames = chain.Frames.ToArray();

        // DeleteFramesCommand.Do(): clear every frame; the chain object itself stays put.
        chain.Frames.Clear();
        pm.SaveTsxProject();

        var afterDelete = DotTiled.Serialization.Loader.Default().LoadTileset(path);
        var clearedTile = afterDelete.Tiles.SingleOrDefault(t => t.ID == 5);
        Assert.True(clearedTile is null || clearedTile.Animation.Count == 0);

        // DeleteFramesCommand.Undo(): re-insert the EXACT SAME frame objects at their original indices.
        foreach (var frame in originalFrames)
            chain.Frames.Add(frame);
        pm.SaveTsxProject();

        var afterUndo = DotTiled.Serialization.Loader.Default().LoadTileset(path);
        var riseUp = afterUndo.Tiles.SingleOrDefault(t => t.ID == 5);
        Assert.NotNull(riseUp);
        Assert.Equal([((uint)6, 300), ((uint)7, 300)], riseUp!.Animation.Select(f => (f.TileID, f.Duration)));

        var tileSix = afterUndo.Tiles.SingleOrDefault(t => t.ID == 6);
        Assert.True(tileSix is null || tileSix.Animation.Count == 0);
    }

    // DeleteFramesCommand.Redo() re-removes the exact same frame objects a subsequent Undo just
    // restored -- same object-reference stability as Undo, so the whole Do/Undo/Redo/Undo cycle
    // must keep landing back on tile 5, not just a single Do/Undo round trip.
    [Fact]
    public void SaveTsxProject_DeleteFramesUndoRedoUndoCycleWithSameFrameObjects_RestoresOriginalTileEveryTime()
    {
        var pm = new ProjectManager();
        var path = WriteFixture(OwnerNotFirstFrameFixtureXml, "Heroes.tsx");
        pm.LoadTsxProject(new FilePath(path));

        var chain = pm.AnimationChainListSave!.AnimationChains.Single();
        var originalFrames = chain.Frames.ToArray();

        // Do().
        chain.Frames.Clear();
        pm.SaveTsxProject();

        // Undo().
        foreach (var frame in originalFrames)
            chain.Frames.Add(frame);
        pm.SaveTsxProject();

        // Redo(): same object references removed again.
        chain.Frames.Clear();
        pm.SaveTsxProject();

        var afterRedo = DotTiled.Serialization.Loader.Default().LoadTileset(path);
        var clearedAfterRedo = afterRedo.Tiles.SingleOrDefault(t => t.ID == 5);
        Assert.True(clearedAfterRedo is null || clearedAfterRedo.Animation.Count == 0);

        // Undo() again: same object references restored again.
        foreach (var frame in originalFrames)
            chain.Frames.Add(frame);
        pm.SaveTsxProject();

        var afterSecondUndo = DotTiled.Serialization.Loader.Default().LoadTileset(path);
        var riseUp = afterSecondUndo.Tiles.SingleOrDefault(t => t.ID == 5);
        Assert.NotNull(riseUp);
        Assert.Equal([((uint)6, 300), ((uint)7, 300)], riseUp!.Animation.Select(f => (f.TileID, f.Duration)));
    }

    // The mirror-image guarantee: if the user clears every frame and then genuinely re-authors
    // the chain with brand-new frame content (different AnimationFrameSave objects, different
    // geometry) instead of undoing, the dormant tile-5 hint from the delete above must NOT be
    // reused -- this must compute a fresh entry tile id from the new geometry, same as
    // SaveTsxProject_AllFramesDeletedFromChain_ClearsPreviouslyOwnedTileAndDoesNotStickOnResave
    // but against an owner-not-first-frame fixture, so a frame-sequence-based fix can't
    // accidentally satisfy this case by coincidence (e.g. an owner tile id that happens to equal
    // frame[0]'s id).
    [Fact]
    public void SaveTsxProject_DeleteFramesThenReauthorWithDifferentFrameObjectsAndSave_ComputesFreshTileNotStaleDormantHint()
    {
        var pm = new ProjectManager();
        var path = WriteFixture(OwnerNotFirstFrameFixtureXml, "Heroes.tsx");
        pm.LoadTsxProject(new FilePath(path));

        var chain = pm.AnimationChainListSave!.AnimationChains.Single();

        // DeleteFramesCommand.Do().
        chain.Frames.Clear();
        pm.SaveTsxProject();

        // Re-author with brand-new frame content (row 1, column 0 -> tile id 4) instead of undoing.
        chain.Frames.Add(new AnimationFrameSave
        {
            TextureName = "Heroes.png",
            LeftCoordinate = 0f, RightCoordinate = 0.25f,
            TopCoordinate = 0.25f, BottomCoordinate = 0.5f,
            FrameLength = 0.1f,
        });

        pm.SaveTsxProject();

        var afterResave = DotTiled.Serialization.Loader.Default().LoadTileset(path);
        Assert.Equal((uint)4, EntryTileIdNamed(afterResave, "RiseUp"));
        var tileFive = afterResave.Tiles.SingleOrDefault(t => t.ID == 5);
        Assert.True(tileFive is null || tileFive.Animation.Count == 0);
    }

    // The satellite-level sibling of the two frame-count-based dormant-hint fixes above, but for a
    // command that shrinks then restores a chain's FOOTPRINT rather than its frame count --
    // e.g. BulkFrameRegionChangedCommand.Do()/Undo() dragging a resize handle across every frame
    // of a 2-wide chain down to 1-wide and back. Unlike DeleteFramesCommand, the frame OBJECTS
    // here never change (Do()/Undo() only mutate LeftCoordinate/RightCoordinate/etc. in place on
    // the same AnimationFrameSave instances) and chain.Frames.Count never changes either -- only
    // the computed footprint width does. A save while shrunk correctly clears the now-unused
    // satellite (tile 6) per the already-established "footprint shrinks" behavior, but the
    // now-satellite-less save also unconditionally replaced (not merged into) the whole
    // per-chain satellite hint dictionary, discarding the (Dx,Dy)=(1,0) -> tile 6 hint outright.
    // A subsequent save after Undo (footprint restored to its exact original 2-wide rect) must
    // land the satellite back on tile 6, not recompute it fresh from geometry (which would land
    // on tile 10 -- the satellite's own frame-0 content tile -- exactly the "owner isn't its own
    // first frame" relocation bug this whole file is themed around, just for a satellite).
    [Fact]
    public void SaveTsxProject_ShrinkFootprintThenUndoWithSameFrameObjectsAndSave_RestoresSatelliteToOriginalTileInsteadOfRelocating()
    {
        var pm = new ProjectManager();
        var path = WriteFixture(OwnerNotFirstFrameWithSatelliteFixtureXml, "Heroes.tsx");
        pm.LoadTsxProject(new FilePath(path));

        var chain = pm.AnimationChainListSave!.AnimationChains.Single();
        var originalRects = chain.Frames
            .Select(f => (f.LeftCoordinate, f.TopCoordinate, f.RightCoordinate, f.BottomCoordinate))
            .ToArray();

        // BulkFrameRegionChangedCommand.Do(): shrink every frame from 2 tiles wide to 1 tile wide
        // in place -- same AnimationFrameSave objects, only their coordinates change.
        foreach (var frame in chain.Frames)
            frame.RightCoordinate = frame.LeftCoordinate + (frame.RightCoordinate - frame.LeftCoordinate) / 2f;
        pm.SaveTsxProject();

        var afterShrink = DotTiled.Serialization.Loader.Default().LoadTileset(path);
        var clearedSatellite = afterShrink.Tiles.SingleOrDefault(t => t.ID == 6);
        Assert.True(clearedSatellite is null || clearedSatellite.Animation.Count == 0);

        // BulkFrameRegionChangedCommand.Undo(): restore every frame's exact original rect.
        for (var i = 0; i < chain.Frames.Count; i++)
        {
            var (l, t, r, b) = originalRects[i];
            chain.Frames[i].LeftCoordinate = l;
            chain.Frames[i].TopCoordinate = t;
            chain.Frames[i].RightCoordinate = r;
            chain.Frames[i].BottomCoordinate = b;
        }
        pm.SaveTsxProject();

        var afterUndo = DotTiled.Serialization.Loader.Default().LoadTileset(path);
        var satellite = afterUndo.Tiles.SingleOrDefault(t => t.ID == 6);
        Assert.NotNull(satellite);
        Assert.Equal(5, satellite!.GetProperty<IntProperty>("ParentId").Value);
        Assert.Equal([((uint)10, 150), ((uint)14, 150)], satellite.Animation.Select(f => (f.TileID, f.Duration)));

        // Tile 10 -- the satellite's own frame-0 content tile, where the bug would relocate it to
        // -- must not have been newly claimed as its own animated tile.
        Assert.DoesNotContain(afterUndo.Tiles, t => t.ID == 10 && t.Animation.Count > 0);
    }

    // Broader gap than any single command: a save whose mapping ABORTS with a warning (mismatched
    // texture, misaligned rect, footprint overflow, non-zero margin/spacing -- any of MapChain's
    // Empty() call sites) while the chain still has frames is neither the "live hint" case nor the
    // "genuinely cleared to zero" case, so nothing preserved its entry/satellite hints -- the next
    // successful save recomputed fresh from frame[0], relocating an "owner isn't its own first
    // frame" chain. Reachable via ANY command whose Do()/Undo() can drive a chain in and out of an
    // abort condition without touching Frames.Count or frame identity --
    // SetFrameTextureNameCommand.Do()/Undo() (pointing a frame at the wrong texture, then back) is
    // the simplest one.
    [Fact]
    public void SaveTsxProject_MappingAbortsThenRecoversViaTextureNameFix_RestoresOriginalTileInsteadOfRelocating()
    {
        var pm = new ProjectManager();
        var path = WriteFixture(OwnerNotFirstFrameFixtureXml, "Heroes.tsx");
        pm.LoadTsxProject(new FilePath(path));

        var chain = pm.AnimationChainListSave!.AnimationChains.Single();
        var frame0 = chain.Frames[0];
        var originalTextureName = frame0.TextureName;

        // SetFrameTextureNameCommand.Do(): point frame 0 at a texture that doesn't match the open
        // tileset's own image -- MapChain aborts the whole chain with a warning.
        frame0.TextureName = "Wrong.png";
        pm.SaveTsxProject();

        // SetFrameTextureNameCommand.Undo(): restore the original, matching texture name.
        frame0.TextureName = originalTextureName;
        pm.SaveTsxProject();

        var reloaded = DotTiled.Serialization.Loader.Default().LoadTileset(path);
        var riseUp = reloaded.Tiles.SingleOrDefault(t => t.ID == 5);
        Assert.NotNull(riseUp);
        Assert.Equal([((uint)6, 300), ((uint)7, 300)], riseUp!.Animation.Select(f => (f.TileID, f.Duration)));
    }

    // The abort test above only checks the FINAL state after the chain is fixed and re-saved -- it
    // never confirmed what the file looks like right after the aborting save itself. In practice
    // (issue found live: resizing a chain's frame to a size Tiled can't represent as a tile
    // animation) a user can abort a save and not immediately un-abort it -- the previously-working
    // animation must still be sitting on disk in the meantime, not wiped the moment the edit that
    // broke it gets saved.
    [Fact]
    public void SaveTsxProject_MappingAborts_LeavesPreviouslyOwnedTileAnimationUntouchedRatherThanWipingIt()
    {
        var pm = new ProjectManager();
        var path = WriteFixture(OwnerNotFirstFrameFixtureXml, "Heroes.tsx");
        pm.LoadTsxProject(new FilePath(path));

        var chain = pm.AnimationChainListSave!.AnimationChains.Single();
        chain.Frames[0].TextureName = "Wrong.png";

        pm.SaveTsxProject();

        var reloaded = DotTiled.Serialization.Loader.Default().LoadTileset(path);
        var riseUp = reloaded.Tiles.SingleOrDefault(t => t.ID == 5);
        Assert.NotNull(riseUp);
        Assert.Equal([((uint)6, 300), ((uint)7, 300)], riseUp!.Animation.Select(f => (f.TileID, f.Duration)));
    }

    // The exact live-bug shape: growing one frame of a single-cell chain to try to turn it into a
    // multi-tile group, without growing every other frame to match, doesn't create satellites --
    // it fails MultiTileToTiledAnimationMapper's "every frame must share the chain's footprint"
    // check, and (before this fix) silently deleted the chain's original, working single-cell
    // animation as a side effect of the failed resize.
    [Fact]
    public void SaveTsxProject_GrowOnlyOneFrameToSpanMultipleCells_FailsValidationButPreservesOriginalAnimation()
    {
        var pm = new ProjectManager();
        var path = WriteFixture(PlainFixtureXml, "Heroes.tsx");
        pm.LoadTsxProject(new FilePath(path));

        var chain = pm.AnimationChainListSave!.AnimationChains.Single();
        // Grow frame 0 from one 16px tile (UV 0.25) to two tiles (UV 0.5) wide; frame 1 stays a
        // single 16px cell -- the two frames now disagree on footprint size.
        chain.Frames[0].RightCoordinate = 0.5f;

        pm.SaveTsxProject();

        var reloaded = DotTiled.Serialization.Loader.Default().LoadTileset(path);
        var tile0 = reloaded.Tiles.SingleOrDefault(t => t.ID == 0);
        Assert.NotNull(tile0);
        Assert.Equal([((uint)0, 200), ((uint)1, 200)], tile0!.Animation.Select(f => (f.TileID, f.Duration)));
    }

    // A dormant chain (frames cleared to zero, hint parked in _tsxDormantHintsByChain) whose
    // refill uses genuinely NEW frame content (not the original objects -- so the pre-map
    // revival check correctly does not fire) that ALSO happens to trigger a mapping abort (wrong
    // texture name) must not lose its dormant hint outright. A LATER save that puts the ORIGINAL
    // frame objects back must still revive the dormant hint and land on tile 5, not recompute
    // fresh from frame[0] (tile 6) as if the chain had never had a hint at all.
    [Fact]
    public void SaveTsxProject_DormantChainRefillAlsoAbortsMapping_DormantHintSurvivesForLaterRevival()
    {
        var pm = new ProjectManager();
        var path = WriteFixture(OwnerNotFirstFrameFixtureXml, "Heroes.tsx");
        pm.LoadTsxProject(new FilePath(path));

        var chain = pm.AnimationChainListSave!.AnimationChains.Single();
        Assert.Equal("RiseUp", chain.Name);
        var originalFrames = chain.Frames.ToArray();

        // DeleteFramesCommand.Do(): clear every frame -- creates the dormant hint for tile 5.
        chain.Frames.Clear();
        pm.SaveTsxProject();

        // Refill with brand-new, unrelated frame content that ALSO points at a texture that
        // doesn't match the open tileset's own image -- MapChain aborts the whole chain with a
        // warning. This is NOT the same frame objects as the dormant snapshot, so the pre-map
        // revival check correctly does not fire for this save.
        chain.Frames.Add(new AnimationFrameSave
        {
            TextureName = "Wrong.png",
            LeftCoordinate = 0f, RightCoordinate = 0.25f,
            TopCoordinate = 0.25f, BottomCoordinate = 0.5f,
            FrameLength = 0.1f,
        });
        pm.SaveTsxProject();

        // Fix it: clear the aborted refill and put the ORIGINAL frame objects back. If the
        // dormant hint survived the abort save above, this must revive it and land on tile 5.
        chain.Frames.Clear();
        foreach (var frame in originalFrames)
            chain.Frames.Add(frame);
        pm.SaveTsxProject();

        var reloaded = DotTiled.Serialization.Loader.Default().LoadTileset(path);
        var riseUp = reloaded.Tiles.SingleOrDefault(t => t.ID == 5);
        Assert.NotNull(riseUp);
        Assert.Equal([((uint)6, 300), ((uint)7, 300)], riseUp!.Animation.Select(f => (f.TileID, f.Duration)));

        var tileSix = reloaded.Tiles.SingleOrDefault(t => t.ID == 6);
        Assert.True(tileSix is null || tileSix.Animation.Count == 0);
    }

    // The dormant sibling of SaveTsxProject_DeleteChainThenReinsertSameObjectAndSave_...: a chain
    // that is already DORMANT (frames cleared) when DeleteChainsCommand removes it from the ACLS
    // entirely, then Undo() re-inserts the exact same (still-empty) object. The dormant hint must
    // survive the round trip through absence, and a later save with the ORIGINAL frame objects
    // put back must still revive it onto tile 5.
    [Fact]
    public void SaveTsxProject_DormantChainDeletedThenReinsertedStillEmptyThenRevived_DormantHintSurvivesAbsence()
    {
        var pm = new ProjectManager();
        var path = WriteFixture(OwnerNotFirstFrameFixtureXml, "Heroes.tsx");
        pm.LoadTsxProject(new FilePath(path));

        var chain = pm.AnimationChainListSave!.AnimationChains.Single();
        var originalFrames = chain.Frames.ToArray();

        // DeleteFramesCommand.Do(): clear every frame -- creates the dormant hint for tile 5.
        chain.Frames.Clear();
        pm.SaveTsxProject();

        // DeleteChainsCommand.Do(): remove the (still-empty) chain from the ACLS entirely.
        pm.AnimationChainListSave.AnimationChains.Remove(chain);
        pm.SaveTsxProject();

        // DeleteChainsCommand.Undo(): re-insert the exact same object, still empty.
        pm.AnimationChainListSave.AnimationChains.Insert(0, chain);
        pm.SaveTsxProject();

        // Now restore the ORIGINAL frame objects -- must revive the dormant hint onto tile 5, not
        // recompute fresh from frame[0] (tile 6).
        foreach (var frame in originalFrames)
            chain.Frames.Add(frame);
        pm.SaveTsxProject();

        var reloaded = DotTiled.Serialization.Loader.Default().LoadTileset(path);
        var riseUp = reloaded.Tiles.SingleOrDefault(t => t.ID == 5);
        Assert.NotNull(riseUp);
        Assert.Equal([((uint)6, 300), ((uint)7, 300)], riseUp!.Animation.Select(f => (f.TileID, f.Duration)));

        var tileSix = reloaded.Tiles.SingleOrDefault(t => t.ID == 6);
        Assert.True(tileSix is null || tileSix.Animation.Count == 0);
    }

    // Reordering a chain's frames (e.g. ReorderCommand<AnimationFrameSave> backing "Reverse Chain"
    // or drag-to-reorder) never adds/removes any AnimationFrameSave object and never empties the
    // chain, but it DOES change which frame is at index 0 -- the exact value
    // MultiTileToTiledAnimationMapper.MapChain falls back to when no entry/satellite hint exists.
    // Both hints are looked up purely by chain (and, for satellites, offset) reference -- never by
    // frame order -- so a reorder must leave the entry and satellite tiles exactly where they were
    // before, only changing the per-frame Animation sequence each one plays.
    [Fact]
    public void SaveTsxProject_ReorderFramesWithinChainAndSave_EntryAndSatelliteTilesStayPutOnlyAnimationOrderChanges()
    {
        var pm = new ProjectManager();
        var path = WriteFixture(OwnerNotFirstFrameWithSatelliteFixtureXml, "Heroes.tsx");
        pm.LoadTsxProject(new FilePath(path));

        var chain = pm.AnimationChainListSave!.AnimationChains.Single();
        Assert.Equal(2, chain.Frames.Count);

        // ReorderCommand<AnimationFrameSave>'s "Reverse Chain" action: chain.Frames.Reverse().
        // Frame[0] used to be tile 9/10 (anchor/satellite); after reversing, frame[0] is what used
        // to be tile 13/14 -- if the hints weren't consulted, the fallback would relocate the
        // anchor to 13 and the satellite to 14.
        chain.Frames.Reverse();
        pm.SaveTsxProject();

        var reloaded = DotTiled.Serialization.Loader.Default().LoadTileset(path);

        var anchor = reloaded.Tiles.SingleOrDefault(t => t.ID == 5);
        Assert.NotNull(anchor);
        Assert.Equal([((uint)13, 150), ((uint)9, 150)], anchor!.Animation.Select(f => (f.TileID, f.Duration)));

        var satellite = reloaded.Tiles.SingleOrDefault(t => t.ID == 6);
        Assert.NotNull(satellite);
        Assert.Equal(5, satellite!.GetProperty<IntProperty>("ParentId").Value);
        Assert.Equal([((uint)14, 150), ((uint)10, 150)], satellite.Animation.Select(f => (f.TileID, f.Duration)));

        // Neither tile 13 nor tile 9 (the reversed frame-0 position) should have been newly
        // claimed as an independent animated tile.
        Assert.DoesNotContain(reloaded.Tiles, t => t.ID == 13 && t.Animation.Count > 0);
    }

    // "Save As" (SaveTsxProject(targetPath: <new path>)) must produce a complete, correct tsx at
    // the new path -- via TsxWriter's full-rewrite branch, since a brand-new path never satisfies
    // TsxWriter.Write's `File.Exists(path)` patch-mode gate -- and must leave the file the project
    // was loaded from completely untouched.
    [Fact]
    public void SaveTsxProject_TargetPath_WritesCompleteFileAtNewPathWithoutModifyingOriginal()
    {
        var pm = new ProjectManager();
        var originalPath = WriteFixture(PlainFixtureXml, "Heroes.tsx");
        pm.LoadTsxProject(new FilePath(originalPath));
        var originalContentBeforeSaveAs = File.ReadAllText(originalPath);

        var newPath = Path.Combine(_dir.Path, "HeroesSaveAs.tsx");
        Assert.False(File.Exists(newPath));

        pm.SaveTsxProject(targetPath: newPath);

        Assert.True(File.Exists(newPath));
        var reloaded = DotTiled.Serialization.Loader.Default().LoadTileset(newPath);
        var tile = reloaded.Tiles.Single(t => t.ID == 0);
        Assert.Equal([((uint)0, 200), ((uint)1, 200)], tile.Animation.Select(f => (f.TileID, f.Duration)));
        Assert.Equal(4, reloaded.Columns);
        Assert.Equal(16, reloaded.TileWidth);

        Assert.Equal(originalContentBeforeSaveAs, File.ReadAllText(originalPath));
    }

    // SaveTsxProject never updates FileName itself -- SaveAnimationChainList(string) (the
    // achx/achj equivalent) doesn't either; AppCommands.SaveCurrentAnimationChainListAsync is the
    // layer that repoints _pm.FileName = path after a successful Save-As dialog. So a subsequent
    // no-args SaveTsxProject() call must still target the file the project was originally loaded
    // from, not the Save-As path -- pin both halves of that contract.
    [Fact]
    public void SaveTsxProject_TargetPath_DoesNotUpdateFileNameAndSubsequentNoArgSaveStaysOnOriginalFile()
    {
        var pm = new ProjectManager();
        var originalPath = WriteFixture(PlainFixtureXml, "Heroes.tsx");
        pm.LoadTsxProject(new FilePath(originalPath));

        var newPath = Path.Combine(_dir.Path, "HeroesSaveAs.tsx");
        pm.SaveTsxProject(targetPath: newPath);

        // FullPath normalizes slashes/casing, so compare via FilePath rather than raw strings.
        Assert.Equal(new FilePath(originalPath).FullPath, new FilePath(pm.FileName!).FullPath);

        var chain = pm.AnimationChainListSave!.AnimationChains.Single();
        chain.Name = "Renamed";

        pm.SaveTsxProject(); // no targetPath -- must go to originalPath, not newPath

        var reloadedOriginal = DotTiled.Serialization.Loader.Default().LoadTileset(originalPath);
        Assert.Equal((uint)0, EntryTileIdNamed(reloadedOriginal, "Renamed"));

        // The Save-As copy must not have received the post-Save-As edit -- it was never touched
        // again after the initial Save-As write. ("ID:0" is the synthetic default name, which is
        // never written as a real "Name" property -- see NativeTsxAnimationSync's explicitName
        // logic -- so check the untouched animation data and the absence of the rename instead.)
        var reloadedSaveAsCopy = DotTiled.Serialization.Loader.Default().LoadTileset(newPath);
        var saveAsTileZero = reloadedSaveAsCopy.Tiles.Single(t => t.ID == 0);
        Assert.Equal([((uint)0, 200), ((uint)1, 200)], saveAsTileZero.Animation.Select(f => (f.TileID, f.Duration)));
        Assert.DoesNotContain(reloadedSaveAsCopy.Tiles,
            t => t.Properties.OfType<DotTiled.StringProperty>().Any(p => p.Name == "Name" && p.Value == "Renamed"));
    }

    // A ProjectManager instance is reused across File > Open calls (it's a single long-lived
    // instance per app window/tab-set, not recreated per file -- see TabSwitchCacheTests). Loading
    // a plain .achx after a native tsx project was open must clear every tsx-specific field, or
    // IsNativeTsxProject/TsxTileSize keep reporting the *previous* tsx's state even though the
    // currently-loaded project is no longer a tsx at all -- and worse, AppCommands.
    // SaveCurrentAnimationChainList branches on IsNativeTsxProject to decide whether to call
    // SaveTsxProject (writing Tiled tileset XML, sourced from the stale _tsxTileset) instead of
    // SaveAnimationChainList for what the user believes is a plain achx save.
    [Fact]
    public void LoadAnimationChain_AfterLoadTsxProject_ClearsNativeTsxState()
    {
        var pm = new ProjectManager();
        var tsxPath = WriteFixture(PlainFixtureXml, "Heroes.tsx");
        pm.LoadTsxProject(new FilePath(tsxPath));
        Assert.True(pm.IsNativeTsxProject);

        var achxPath = Path.Combine(_dir.Path, "Plain.achx");
        var acls = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        acls.AnimationChains.Add(new AnimationChainSave { Name = "Idle" });
        acls.Save(achxPath);

        pm.LoadAnimationChain(new FilePath(achxPath));

        Assert.False(pm.IsNativeTsxProject);
        Assert.Null(pm.TsxTileSize);
    }

    // SaveAnimationChainList(string)/(Stream)/SaveAnimationChainListAsync(Stream) write
    // achx/achj-format content -- calling any of them on a native tsx project (whose
    // AnimationChainListSave is a *view* over Tiled tileset data, not a real achx) would
    // silently write malformed/misleading content instead of the real .tsx file.
    // AppCommands.SaveCurrentAnimationChainList already branches on IsNativeTsxProject to route
    // to SaveTsxProject instead, but that's the only gate today -- ProjectManager itself should
    // refuse directly too, so a caller that bypasses AppCommands (several UI-layer call sites do,
    // e.g. the browser build's stream-based save) can't slip through unguarded.
    [Fact]
    public void SaveAnimationChainList_NativeTsxProjectLoaded_ThrowsInsteadOfWritingAchxFormatContent()
    {
        var pm = new ProjectManager();
        var tsxPath = WriteFixture(PlainFixtureXml, "Heroes.tsx");
        pm.LoadTsxProject(new FilePath(tsxPath));
        // UV needs no texture-size resolution, so the only way this can throw is the guard under
        // test -- not an incidental "missing PNG" failure from the Pixel-conversion path.
        pm.OnDiskCoordinateType = TextureCoordinateType.UV;

        var achxPath = Path.Combine(_dir.Path, "WrongFormat.achx");
        var ex = Assert.Throws<InvalidOperationException>(() => pm.SaveAnimationChainList(achxPath));
        Assert.Contains("SaveTsxProject", ex.Message);
        Assert.False(File.Exists(achxPath));
    }

    [Fact]
    public void SaveAnimationChainList_StreamOverload_NativeTsxProjectLoaded_ThrowsInsteadOfWritingAchxFormatContent()
    {
        var pm = new ProjectManager();
        var tsxPath = WriteFixture(PlainFixtureXml, "Heroes.tsx");
        pm.LoadTsxProject(new FilePath(tsxPath));
        pm.OnDiskCoordinateType = TextureCoordinateType.UV;

        using var stream = new MemoryStream();
        var ex = Assert.Throws<InvalidOperationException>(() => pm.SaveAnimationChainList(stream));
        Assert.Contains("SaveTsxProject", ex.Message);
    }

    [Fact]
    public async System.Threading.Tasks.Task SaveAnimationChainListAsync_NativeTsxProjectLoaded_ThrowsInsteadOfWritingAchxFormatContent()
    {
        var pm = new ProjectManager();
        var tsxPath = WriteFixture(PlainFixtureXml, "Heroes.tsx");
        pm.LoadTsxProject(new FilePath(tsxPath));
        pm.OnDiskCoordinateType = TextureCoordinateType.UV;

        using var stream = new MemoryStream();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => pm.SaveAnimationChainListAsync(stream));
        Assert.Contains("SaveTsxProject", ex.Message);
    }

    // A collision shape (or a flip, offset, color, non-looping chain) has no home in a .tsx. The
    // save still writes everything the format can hold, but must say what it dropped -- see
    // TsxLossyDataCheckTests for the full list.
    [Fact]
    public void SaveTsxProject_FrameCarriesCollisionShape_WritesAnimationAndWarnsAboutTheShape()
    {
        var pm = new ProjectManager();
        var path = WriteFixture(PlainFixtureXml, "Heroes.tsx");
        pm.LoadTsxProject(new FilePath(path));
        var chain = pm.AnimationChainListSave!.AnimationChains.Single();
        chain.Frames[0].ShapesSave = new ShapesSave { Shapes = { new AARectSave { Name = "Hit" } } };
        chain.Frames[1].FrameLength = 0.3f;

        var warnings = pm.SaveTsxProject();

        var warning = Assert.Single(warnings);
        Assert.Contains("\"ID:0\"", warning);
        Assert.Contains("shape", warning);
        var reloaded = DotTiled.Serialization.Loader.Default().LoadTileset(path);
        Assert.Equal([((uint)0, 200), ((uint)1, 300)], reloaded.Tiles.Single(t => t.ID == 0).Animation.Select(f => (f.TileID, f.Duration)));
    }

    private static uint EntryTileIdNamed(DotTiled.Tileset tileset, string chainName) =>
        tileset.Tiles
            .Single(t => t.Properties.OfType<DotTiled.StringProperty>().Any(p => p.Name == "Name" && p.Value == chainName))
            .ID;
}
