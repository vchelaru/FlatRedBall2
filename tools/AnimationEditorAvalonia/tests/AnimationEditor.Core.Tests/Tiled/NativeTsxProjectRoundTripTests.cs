using AnimationEditor.Core.Tiled;
using DotTiled;
using DotTiled.Serialization;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Tiled;

/// <summary>
/// End-to-end: load a .tsx, fold it into an achx-shaped <c>AnimationChainListSave</c> (the native
/// project's in-memory model per issue #1140), save it straight back out, and confirm nothing was
/// lost -- exercising <see cref="TiledAnimationToAchjMapper"/>, <see
/// cref="MultiTileToTiledAnimationMapper"/>, <see cref="NativeTsxAnimationSync"/> and <see
/// cref="TsxWriter"/> together the way the native tsx save path will use them.
/// </summary>
public class NativeTsxProjectRoundTripTests
{
    // 4 columns, 16x16 tiles. Tile 0 is a plain single-tile animation (no Name). Tile 8 is the
    // anchor of a 2-tile-wide group whose satellite (tile 9) carries ParentId=8.
    private const string FixtureXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="64" columns="4">
         <image source="Heroes.png" width="64" height="256"/>
         <tile id="0">
          <animation>
           <frame tileid="0" duration="100"/>
           <frame tileid="1" duration="100"/>
          </animation>
         </tile>
         <tile id="8">
          <animation>
           <frame tileid="8" duration="150"/>
           <frame tileid="12" duration="150"/>
          </animation>
         </tile>
         <tile id="9">
          <properties>
           <property name="ParentId" type="int" value="8"/>
          </properties>
          <animation>
           <frame tileid="9" duration="150"/>
           <frame tileid="13" duration="150"/>
          </animation>
         </tile>
        </tileset>
        """;

    [Fact]
    public void LoadMapApplyWrite_RoundTrip_PreservesSingleTileAndMultiTileGroupAnimations()
    {
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        var fixturePath = Path.Combine(tempDir, "Heroes.tsx");
        File.WriteAllText(fixturePath, FixtureXml);
        var tileset = Loader.Default().LoadTileset(fixturePath);

        var acls = TiledAnimationToAchjMapper.Map(tileset, out _);
        Assert.Equal(2, acls.AnimationChains.Count);
        Assert.Contains(acls.AnimationChains, c => c.Name == "ID:0");
        var groupChain = acls.AnimationChains.Single(c => c.Name == "ID:8");
        // Anchor tile 8 + satellite tile 9 (one column to the right) -> 32px-wide frame rect;
        // the achx model is UV, and the fixture's texture is 64px wide, so that's 32/64 = 0.5.
        Assert.Equal(0.5f, groupChain.Frames[0].RightCoordinate - groupChain.Frames[0].LeftCoordinate, tolerance: 0.0001f);

        var tilesetInfo = new TilesetAnimationInfo
        {
            TileWidth = tileset.TileWidth,
            TileHeight = tileset.TileHeight,
            ColumnCount = tileset.Columns,
            ImageFileName = tileset.Image.Value.Source.Value,
            TextureWidth = tileset.Image.Value.Width.Value,
            TextureHeight = tileset.Image.Value.Height.Value,
        };
        var mapped = MultiTileToTiledAnimationMapper.Map(acls, tilesetInfo);
        NativeTsxAnimationSync.Apply(tileset, mapped);

        var outputPath = Path.Combine(tempDir, "Heroes.written.tsx");
        TsxWriter.Write(tileset, outputPath);
        var reloaded = Loader.Default().LoadTileset(outputPath);

        var reloadedTile0 = reloaded.Tiles.Single(t => t.ID == 0);
        Assert.Equal([((uint)0, 100), ((uint)1, 100)], reloadedTile0.Animation.Select(f => (f.TileID, f.Duration)));

        var reloadedAnchor = reloaded.Tiles.Single(t => t.ID == 8);
        Assert.Equal([((uint)8, 150), ((uint)12, 150)], reloadedAnchor.Animation.Select(f => (f.TileID, f.Duration)));

        var reloadedSatellite = reloaded.Tiles.Single(t => t.ID == 9);
        Assert.Equal(8, reloadedSatellite.GetProperty<IntProperty>("ParentId").Value);
        Assert.Equal([((uint)9, 150), ((uint)13, 150)], reloadedSatellite.Animation.Select(f => (f.TileID, f.Duration)));
    }

    // Tile 5 owns an animation whose frames are [6, 7] -- 5 itself never appears as a frame. This
    // is an entirely ordinary hand-authored-in-Tiled pattern (the tile shown at rest is never one
    // of the cycled frames), and is exactly the shape that shipped broken: recomputing the "owning"
    // tile from frame[0] every save relocated this animation from 5 to 6, leaving 5 blank.
    private const string OwnerNotFirstFrameFixtureXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="64" columns="4">
         <image source="Heroes.png" width="64" height="256"/>
         <tile id="2">
          <animation>
           <frame tileid="2" duration="100"/>
           <frame tileid="3" duration="100"/>
          </animation>
         </tile>
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

    [Fact]
    public void LoadEditUnrelatedChainSave_OwnerTileNotItsOwnFirstFrame_StaysOnItsOriginalTile()
    {
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        var fixturePath = Path.Combine(tempDir, "Heroes.tsx");
        File.WriteAllText(fixturePath, OwnerNotFirstFrameFixtureXml);
        var tileset = Loader.Default().LoadTileset(fixturePath);

        var acls = TiledAnimationToAchjMapper.Map(tileset, out var entryTileIdsByChain);
        var tilesetInfo = new TilesetAnimationInfo
        {
            TileWidth = tileset.TileWidth,
            TileHeight = tileset.TileHeight,
            ColumnCount = tileset.Columns,
            ImageFileName = tileset.Image.Value.Source.Value,
            TextureWidth = tileset.Image.Value.Width.Value,
            TextureHeight = tileset.Image.Value.Height.Value,
        };

        // Simulate "I removed one animation": drop the unrelated ID:2 chain, leave RiseUp alone.
        acls.AnimationChains.RemoveAll(c => c.Name == "ID:2");

        var mapped = MultiTileToTiledAnimationMapper.Map(acls, tilesetInfo, entryTileIdsByChain);
        NativeTsxAnimationSync.Apply(tileset, mapped);

        var outputPath = Path.Combine(tempDir, "Heroes.written.tsx");
        TsxWriter.Write(tileset, outputPath);
        var reloaded = Loader.Default().LoadTileset(outputPath);

        var riseUp = reloaded.Tiles.SingleOrDefault(t => t.ID == 5);
        Assert.NotNull(riseUp);
        Assert.Equal([((uint)6, 300), ((uint)7, 300)], riseUp!.Animation.Select(f => (f.TileID, f.Duration)));
        Assert.Equal("RiseUp", riseUp.GetProperty<StringProperty>("Name").Value);

        // No new tile should have been invented at id 6 for the same content.
        Assert.DoesNotContain(reloaded.Tiles, t => t.ID == 6 && t.Animation.Count > 0);

        // The removed chain's tile is actually cleared, not left dangling.
        var clearedTile = reloaded.Tiles.SingleOrDefault(t => t.ID == 2);
        Assert.True(clearedTile is null || clearedTile.Animation.Count == 0);
    }

    // Tile 8 is the anchor of a 2-tile-wide group whose satellite (tile 9) also carries an
    // unrelated hand-authored property, to confirm shrinking doesn't wipe more than this sync
    // owns.
    private const string TwoWideGroupFixtureXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="64" columns="4">
         <image source="Heroes.png" width="64" height="256"/>
         <tile id="8">
          <animation>
           <frame tileid="8" duration="150"/>
           <frame tileid="12" duration="150"/>
          </animation>
         </tile>
         <tile id="9">
          <properties>
           <property name="ParentId" type="int" value="8"/>
           <property name="Collidable" type="bool" value="true"/>
          </properties>
          <animation>
           <frame tileid="9" duration="150"/>
           <frame tileid="13" duration="150"/>
          </animation>
         </tile>
        </tileset>
        """;

    [Fact]
    public void LoadShrinkGroupFootprintSave_UnusedSatelliteCleared_ButUnrelatedPropertyKept()
    {
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        var fixturePath = Path.Combine(tempDir, "Heroes.tsx");
        File.WriteAllText(fixturePath, TwoWideGroupFixtureXml);
        var tileset = Loader.Default().LoadTileset(fixturePath);

        var acls = TiledAnimationToAchjMapper.Map(tileset, out var entryTileIdsByChain);
        var groupChain = acls.AnimationChains.Single(c => c.Name == "ID:8");

        // Shrink the chain's frame rect from 2 tiles wide to 1 tile wide (drops the satellite).
        foreach (var frame in groupChain.Frames)
            frame.RightCoordinate = frame.LeftCoordinate + (16f / 64f);

        var tilesetInfo = new TilesetAnimationInfo
        {
            TileWidth = tileset.TileWidth,
            TileHeight = tileset.TileHeight,
            ColumnCount = tileset.Columns,
            ImageFileName = tileset.Image.Value.Source.Value,
            TextureWidth = tileset.Image.Value.Width.Value,
            TextureHeight = tileset.Image.Value.Height.Value,
        };

        var mapped = MultiTileToTiledAnimationMapper.Map(acls, tilesetInfo, entryTileIdsByChain);
        Assert.Empty(mapped.Single().Satellites);
        NativeTsxAnimationSync.Apply(tileset, mapped);

        var outputPath = Path.Combine(tempDir, "Heroes.written.tsx");
        TsxWriter.Write(tileset, outputPath);
        var reloaded = Loader.Default().LoadTileset(outputPath);

        var anchor = reloaded.Tiles.Single(t => t.ID == 8);
        Assert.Equal([((uint)8, 150), ((uint)12, 150)], anchor.Animation.Select(f => (f.TileID, f.Duration)));

        var formerSatellite = reloaded.Tiles.SingleOrDefault(t => t.ID == 9);
        Assert.NotNull(formerSatellite);
        Assert.Empty(formerSatellite!.Animation);
        Assert.DoesNotContain(formerSatellite.Properties, p => p.Name == "ParentId");
        Assert.True(formerSatellite.GetProperty<BoolProperty>("Collidable").Value);
    }

    // Tile 8 is the anchor of a 2-tile-wide group. Tile 9's on-disk <animation> was hand-edited in
    // Tiled directly and is now inconsistent with what the anchor+its own grid offset would derive
    // (1 frame with duration 999 instead of the 2 frames of duration 150 lockstep would produce).
    private const string SatelliteHandEditedFixtureXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="64" columns="4">
         <image source="Heroes.png" width="64" height="256"/>
         <tile id="8">
          <animation>
           <frame tileid="8" duration="150"/>
           <frame tileid="12" duration="150"/>
          </animation>
         </tile>
         <tile id="9">
          <properties>
           <property name="ParentId" type="int" value="8"/>
          </properties>
          <animation>
           <frame tileid="9" duration="999"/>
          </animation>
         </tile>
        </tileset>
        """;

    [Fact]
    public void LoadMapApplySave_SatelliteHandEditedFramesInconsistentWithAnchor_IgnoredOnLoadWarnedByValidatorOverwrittenOnSave()
    {
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        var fixturePath = Path.Combine(tempDir, "Heroes.tsx");
        File.WriteAllText(fixturePath, SatelliteHandEditedFixtureXml);
        var tileset = Loader.Default().LoadTileset(fixturePath);

        // (b) The safety net: the validator must flag tile 9's on-disk content as wrong, even
        // though nothing downstream will ever read it.
        var issues = TsxAnimationValidator.Validate(tileset);
        var issue = Assert.Single(issues);
        Assert.Equal((uint)9, issue.TileId);
        Assert.Contains("has 1 animation frame(s) but its group anchor (tile 8) has 2", issue.Message);

        // (a) The satellite's actual on-disk frames (1 frame, duration 999) never make it into the
        // editable model -- only the anchor's frames (2 frames, duration 150) do.
        var acls = TiledAnimationToAchjMapper.Map(tileset, out var entryTileIdsByChain);
        var chain = Assert.Single(acls.AnimationChains);
        Assert.Equal(2, chain.Frames.Count);
        Assert.All(chain.Frames, f => Assert.Equal(0.15f, f.FrameLength, tolerance: 0.0001f));

        // (c) Saving without addressing the warning overwrites tile 9's on-disk content to the
        // correctly-derived sequence -- ignored, then overwritten to correct, never corrupted.
        var tilesetInfo = new TilesetAnimationInfo
        {
            TileWidth = tileset.TileWidth,
            TileHeight = tileset.TileHeight,
            ColumnCount = tileset.Columns,
            ImageFileName = tileset.Image.Value.Source.Value,
            TextureWidth = tileset.Image.Value.Width.Value,
            TextureHeight = tileset.Image.Value.Height.Value,
        };
        var mapped = MultiTileToTiledAnimationMapper.Map(acls, tilesetInfo, entryTileIdsByChain);
        NativeTsxAnimationSync.Apply(tileset, mapped);

        var outputPath = Path.Combine(tempDir, "Heroes.written.tsx");
        TsxWriter.Write(tileset, outputPath);
        var reloaded = Loader.Default().LoadTileset(outputPath);

        var reloadedSatellite = reloaded.Tiles.Single(t => t.ID == 9);
        Assert.Equal([((uint)9, 150), ((uint)13, 150)], reloadedSatellite.Animation.Select(f => (f.TileID, f.Duration)));
    }

    // Tile 0 is a plain single-tile animation. Tile 1 pre-exists with an unrelated hand-authored
    // property but no animation -- it's exactly where the new satellite will need to land once
    // the chain grows to a 2-tile-wide footprint.
    private const string SingleTileWithNeighborFixtureXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="64" columns="4">
         <image source="Heroes.png" width="64" height="256"/>
         <tile id="0">
          <animation>
           <frame tileid="0" duration="100"/>
           <frame tileid="1" duration="100"/>
          </animation>
         </tile>
         <tile id="1">
          <properties>
           <property name="Foo" value="Bar"/>
          </properties>
         </tile>
        </tileset>
        """;

    [Fact]
    public void LoadGrowChainFootprintSave_NewSatelliteCreated_ButUnrelatedPropertyOnExistingTileKept()
    {
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        var fixturePath = Path.Combine(tempDir, "Heroes.tsx");
        File.WriteAllText(fixturePath, SingleTileWithNeighborFixtureXml);
        var tileset = Loader.Default().LoadTileset(fixturePath);

        var acls = TiledAnimationToAchjMapper.Map(tileset, out var entryTileIdsByChain);
        var chain = acls.AnimationChains.Single(c => c.Name == "ID:0");

        // Grow the chain's frame rect from 1 tile wide to 2 tiles wide (gains a satellite).
        foreach (var frame in chain.Frames)
            frame.RightCoordinate = frame.LeftCoordinate + (32f / 64f);

        var tilesetInfo = new TilesetAnimationInfo
        {
            TileWidth = tileset.TileWidth,
            TileHeight = tileset.TileHeight,
            ColumnCount = tileset.Columns,
            ImageFileName = tileset.Image.Value.Source.Value,
            TextureWidth = tileset.Image.Value.Width.Value,
            TextureHeight = tileset.Image.Value.Height.Value,
        };

        var mapped = MultiTileToTiledAnimationMapper.Map(acls, tilesetInfo, entryTileIdsByChain);
        Assert.Single(mapped.Single().Satellites);
        NativeTsxAnimationSync.Apply(tileset, mapped);

        var outputPath = Path.Combine(tempDir, "Heroes.written.tsx");
        TsxWriter.Write(tileset, outputPath);
        var reloaded = Loader.Default().LoadTileset(outputPath);

        var anchor = reloaded.Tiles.Single(t => t.ID == 0);
        Assert.Equal([((uint)0, 100), ((uint)1, 100)], anchor.Animation.Select(f => (f.TileID, f.Duration)));

        var newSatellite = reloaded.Tiles.Single(t => t.ID == 1);
        Assert.Equal([((uint)1, 100), ((uint)2, 100)], newSatellite.Animation.Select(f => (f.TileID, f.Duration)));
        Assert.Equal(0, newSatellite.GetProperty<IntProperty>("ParentId").Value);
        Assert.Equal("Bar", newSatellite.GetProperty<StringProperty>("Foo").Value);
    }
}
