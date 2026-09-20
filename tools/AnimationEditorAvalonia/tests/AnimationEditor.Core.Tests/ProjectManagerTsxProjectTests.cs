using AnimationEditor.Core;
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
        var clearedTile = afterClear.Tiles.Single(t => t.ID == 0);
        Assert.Empty(clearedTile.Animation);
        Assert.DoesNotContain(clearedTile.Properties, p => p.Name is "Name" or "ParentId");

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
        var tileZeroAfterResave = afterResave.Tiles.Single(t => t.ID == 0);
        Assert.Empty(tileZeroAfterResave.Animation);
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

    private static uint EntryTileIdNamed(DotTiled.Tileset tileset, string chainName) =>
        tileset.Tiles
            .Single(t => t.Properties.OfType<DotTiled.StringProperty>().Any(p => p.Name == "Name" && p.Value == chainName))
            .ID;
}
