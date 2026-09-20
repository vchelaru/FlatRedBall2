using AnimationEditor.Core.Tiled;
using DotTiled;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Tiled;

/// <summary>
/// The in-memory <see cref="AnimationChainListSave"/> model must always be UV (0-1) coordinates
/// and Second-based durations -- the same invariant every achx load produces via
/// <c>ProjectManager.NormalizeCoordinatesToUv</c> (see its doc comment: "the in-memory
/// representation... is always UV so the rendering pipeline can render at any texture size"). A
/// real-world bug (issue #1140 follow-up) shipped this mapper storing raw pixel values with
/// CoordinateType=Pixel instead: nothing downstream actually respects that as "leave these alone,"
/// so every consumer re-multiplied an already-in-pixels value by the texture size again (e.g. a
/// frame length of 200ms rendered as "200 seconds"; a coordinate of 864px rendered as
/// 864 * 2048 = 1,769,472). These tests assert the UV/seconds output directly.
/// </summary>
public class TiledAnimationToAchjMapperTests
{
    // 4 columns, 16x16 tiles, 64x64 texture (so UV = pixel / 64).
    private static Tileset EmptyTileset() => new()
    {
        Name = "Heroes",
        TileWidth = 16,
        TileHeight = 16,
        TileCount = 256,
        Columns = 4,
        Image = new Image { Source = "Heroes.png", Width = 64, Height = 64 },
    };

    [Fact]
    public void Map_AnyTile_UsesUvCoordinatesAndSecondsBasedDuration()
    {
        var tileset = EmptyTileset();
        var tile = new Tile { ID = 5, Width = 0, Height = 0 };
        tile.Animation.Add(new Frame { TileID = 5, Duration = 200 });
        tileset.Tiles.Add(tile);

        var acls = TiledAnimationToAchjMapper.Map(tileset, out _, out _);

        Assert.Equal(TextureCoordinateType.UV, acls.CoordinateType);
        Assert.Equal(TimeMeasurementUnit.Second, acls.TimeMeasurementUnit);
        var chain = Assert.Single(acls.AnimationChains);
        // Tile 5, 4 columns -> column 1, row 1 -> pixel left=16, top=16; texture is 64x64.
        Assert.Equal(16f / 64f, chain.Frames[0].LeftCoordinate, tolerance: 0.0001f);
        Assert.Equal(16f / 64f, chain.Frames[0].TopCoordinate, tolerance: 0.0001f);
        Assert.Equal(0.2f, chain.Frames[0].FrameLength, tolerance: 0.0001f);
    }

    [Fact]
    public void Map_RealWorldColumnAndLargeTexture_MatchesReportedGarbageValueRootCause()
    {
        // Regression for the exact numbers reported against a real file: 128 columns, 16px
        // tiles, 2048px texture, tile at column 54 -> pixel Left=864. The bug produced
        // 864 * 2048 = 1,769,472 downstream by treating an already-pixel value as UV and
        // reconverting; the correct UV value is 864/2048.
        var tileset = new Tileset
        {
            Name = "ChibiCthulhuTiles",
            TileWidth = 16,
            TileHeight = 16,
            TileCount = 16384,
            Columns = 128,
            Image = new Image { Source = "ChibiCthulhuTiles.png", Width = 2048, Height = 2048 },
        };
        // Column 54, row 93 -> tileId = 93*128 + 54 = 11958.
        var tile = new Tile { ID = 11958, Width = 0, Height = 0 };
        tile.Animation.Add(new Frame { TileID = 11958, Duration = 200 });
        tileset.Tiles.Add(tile);

        var acls = TiledAnimationToAchjMapper.Map(tileset, out _, out _);

        var chain = Assert.Single(acls.AnimationChains);
        Assert.Equal(864f / 2048f, chain.Frames[0].LeftCoordinate, tolerance: 0.00001f);
        Assert.NotEqual(1769472f, chain.Frames[0].LeftCoordinate);
    }

    [Fact]
    public void Map_MultiTileSatelliteWithParentId_FoldsIntoOneWideFrameChain()
    {
        var tileset = EmptyTileset();
        var anchor = new Tile { ID = 0, Width = 0, Height = 0 };
        anchor.Animation.Add(new Frame { TileID = 0, Duration = 100 });
        anchor.Animation.Add(new Frame { TileID = 1, Duration = 100 });
        tileset.Tiles.Add(anchor);

        // Satellite sits one column to the right of the anchor (tile 1 = column 1, row 0).
        var satellite = new Tile { ID = 1, Width = 0, Height = 0 };
        satellite.Animation.Add(new Frame { TileID = 1, Duration = 100 });
        satellite.Animation.Add(new Frame { TileID = 2, Duration = 100 });
        satellite.Properties.Add(new IntProperty { Name = "ParentId", Value = 0 });
        tileset.Tiles.Add(satellite);

        var acls = TiledAnimationToAchjMapper.Map(tileset, out _, out _);

        var chain = Assert.Single(acls.AnimationChains);
        Assert.Equal(2, chain.Frames.Count);

        // Frame 0: anchor tile 0 -> column 0, row 0; footprint is 2 tiles wide -> right edge at 2*16=32px.
        Assert.Equal(0f, chain.Frames[0].LeftCoordinate, tolerance: 0.0001f);
        Assert.Equal(32f / 64f, chain.Frames[0].RightCoordinate, tolerance: 0.0001f);
        Assert.Equal(0f, chain.Frames[0].TopCoordinate, tolerance: 0.0001f);
        Assert.Equal(16f / 64f, chain.Frames[0].BottomCoordinate, tolerance: 0.0001f);

        // Frame 1: anchor tile 1 -> column 1, row 0 -> left edge at 16px, right edge at (1+2)*16=48px.
        Assert.Equal(16f / 64f, chain.Frames[1].LeftCoordinate, tolerance: 0.0001f);
        Assert.Equal(48f / 64f, chain.Frames[1].RightCoordinate, tolerance: 0.0001f);
    }

    [Fact]
    public void Map_TileWithNameProperty_UsesNameAsChainName()
    {
        var tileset = EmptyTileset();
        var tile = new Tile { ID = 2, Width = 0, Height = 0 };
        tile.Animation.Add(new Frame { TileID = 2, Duration = 100 });
        tile.Properties.Add(new StringProperty { Name = "Name", Value = "Torch" });
        tileset.Tiles.Add(tile);

        var acls = TiledAnimationToAchjMapper.Map(tileset, out _, out _);

        Assert.Equal("Torch", acls.AnimationChains.Single().Name);
    }

    [Fact]
    public void Map_TileWithoutNameProperty_UsesIdLabel()
    {
        var tileset = EmptyTileset();
        var tile = new Tile { ID = 5, Width = 0, Height = 0 };
        tile.Animation.Add(new Frame { TileID = 5, Duration = 200 });
        tileset.Tiles.Add(tile);

        var acls = TiledAnimationToAchjMapper.Map(tileset, out _, out _);

        var chain = Assert.Single(acls.AnimationChains);
        Assert.Equal("ID:5", chain.Name);
    }

    [Fact]
    public void Map_ColumnsIsZero_ThrowsInsteadOfDivideByZero()
    {
        // A corrupt/hand-edited tsx with Columns=0 makes every tile-position "% columns"/"/
        // columns" computation in this method either divide by zero (uint DivideByZeroException)
        // or, for a negative Columns, unchecked-wrap into nonsense positions -- fail loudly
        // instead, same "fail loud, not corrupt" precedent as the negative-ParentId fix.
        var tileset = new Tileset
        {
            Name = "Corrupt",
            TileWidth = 16,
            TileHeight = 16,
            TileCount = 16,
            Columns = 0,
            Image = new Image { Source = "Corrupt.png", Width = 64, Height = 64 },
        };
        var tile = new Tile { ID = 0, Width = 0, Height = 0 };
        tile.Animation.Add(new Frame { TileID = 0, Duration = 100 });
        tileset.Tiles.Add(tile);

        Assert.Throws<InvalidOperationException>(() => TiledAnimationToAchjMapper.Map(tileset, out _, out _));
    }

    [Fact]
    public void Map_NegativeParentId_TreatedAsAnchorNotUncheckedCastToHugeId()
    {
        // A negative ParentId (hand-edited/corrupt file) must not unchecked-cast into a huge
        // uint (-1 -> 4294967295) and go looking for a nonexistent anchor; treat it like a
        // missing ParentId instead, so the tile becomes its own chain rather than disappearing.
        var tileset = EmptyTileset();
        var tile = new Tile { ID = 3, Width = 0, Height = 0 };
        tile.Animation.Add(new Frame { TileID = 3, Duration = 100 });
        tile.Properties.Add(new IntProperty { Name = "ParentId", Value = -1 });
        tileset.Tiles.Add(tile);

        var acls = TiledAnimationToAchjMapper.Map(tileset, out _, out _);

        var chain = Assert.Single(acls.AnimationChains);
        Assert.Equal("ID:3", chain.Name);
    }

    [Fact]
    public void Map_ParentIdDoesNotResolveToAnimatedTile_SurfacesAsItsOwnChainInsteadOfDropped()
    {
        // ParentId 99 doesn't reference any animated tile in this tileset (typo, hand-edit
        // mistake, or the anchor it used to point to was separately deleted). Prior behavior
        // silently dropped this tile's animation from the returned model entirely -- it was
        // excluded from the anchor loop (it has a ParentId) but never folded into any anchor's
        // satellites either (no anchor with ID 99 exists to claim it). It must now surface as its
        // own chain so the data survives into what the user can edit/save.
        var tileset = EmptyTileset();
        var orphan = new Tile { ID = 5, Width = 0, Height = 0 };
        orphan.Animation.Add(new Frame { TileID = 5, Duration = 100 });
        orphan.Properties.Add(new IntProperty { Name = "ParentId", Value = 99 });
        tileset.Tiles.Add(orphan);

        var acls = TiledAnimationToAchjMapper.Map(tileset, out var entryTileIdsByChain, out _);

        var chain = Assert.Single(acls.AnimationChains);
        Assert.Equal("ID:5", chain.Name);
        Assert.Single(chain.Frames);
        Assert.Equal((uint)5, entryTileIdsByChain[chain]);
    }

    [Fact]
    public void Map_BackwardParentId_SatelliteAboveAnchorSurfacesAsItsOwnChainInsteadOfBeingSilentlyExcluded()
    {
        // Anchor at tile 5 (column 1, row 1, 4 columns/tileset). "Satellite" at tile 1 (column 1,
        // row 0) -- directly ABOVE the anchor, a footprint shape AnimationEditor's own UI can
        // never produce (a group only ever grows right/down from its anchor). Its dy relative to
        // the anchor is -1; the mapper's uint dy/footprintRows arithmetic underflows for this case
        // and silently excludes tile 1 from the anchor's mapped frame rect instead of preserving
        // its data. Tile 1 must not vanish: it should surface as its own independent chain, same
        // treatment as an orphaned/chained ParentId.
        var tileset = EmptyTileset();

        var anchor = new Tile { ID = 5, Width = 0, Height = 0 };
        anchor.Animation.Add(new Frame { TileID = 5, Duration = 100 });
        tileset.Tiles.Add(anchor);

        var backwardSatellite = new Tile { ID = 1, Width = 0, Height = 0 };
        backwardSatellite.Animation.Add(new Frame { TileID = 1, Duration = 100 });
        backwardSatellite.Properties.Add(new IntProperty { Name = "ParentId", Value = 5 });
        tileset.Tiles.Add(backwardSatellite);

        var acls = TiledAnimationToAchjMapper.Map(tileset, out var entryTileIdsByChain, out _);

        Assert.Equal(2, acls.AnimationChains.Count);
        var chainNames = acls.AnimationChains.Select(c => c.Name).ToList();
        Assert.Contains("ID:5", chainNames);
        Assert.Contains("ID:1", chainNames);

        var backwardChain = acls.AnimationChains.Single(c => c.Name == "ID:1");
        Assert.Single(backwardChain.Frames);
        Assert.Equal((uint)1, entryTileIdsByChain[backwardChain]);
    }

    [Fact]
    public void Map_ChainedParentId_SatelliteOfASatelliteSurfacesAsItsOwnChainInsteadOfDropped()
    {
        // A -- no ParentId, a true top-level anchor.
        // B -- ParentId=A.ID, a legitimate satellite of A.
        // C -- ParentId=B.ID, chained through a satellite rather than a true anchor. This has no
        // corresponding multi-tile-group shape AnimationEditor's own UI could ever produce (a
        // footprint is always a simple rectangle relative to ONE anchor), so C must not be silently
        // dropped: it should surface as its own independent chain, the same treatment as an
        // orphaned/unresolvable ParentId.
        var tileset = EmptyTileset();

        var anchorA = new Tile { ID = 0, Width = 0, Height = 0 };
        anchorA.Animation.Add(new Frame { TileID = 0, Duration = 100 });
        tileset.Tiles.Add(anchorA);

        var satelliteB = new Tile { ID = 1, Width = 0, Height = 0 };
        satelliteB.Animation.Add(new Frame { TileID = 1, Duration = 100 });
        satelliteB.Properties.Add(new IntProperty { Name = "ParentId", Value = 0 });
        tileset.Tiles.Add(satelliteB);

        var chainedC = new Tile { ID = 2, Width = 0, Height = 0 };
        chainedC.Animation.Add(new Frame { TileID = 2, Duration = 100 });
        chainedC.Properties.Add(new IntProperty { Name = "ParentId", Value = 1 });
        tileset.Tiles.Add(chainedC);

        var acls = TiledAnimationToAchjMapper.Map(tileset, out var entryTileIdsByChain, out _);

        Assert.Equal(2, acls.AnimationChains.Count);
        var chainNames = acls.AnimationChains.Select(c => c.Name).ToList();
        Assert.Contains("ID:0", chainNames);
        Assert.Contains("ID:2", chainNames);

        var cChain = acls.AnimationChains.Single(c => c.Name == "ID:2");
        Assert.Single(cChain.Frames);
        Assert.Equal((uint)2, entryTileIdsByChain[cChain]);
    }

    [Fact]
    public void Map_ReturnsEntryTileIdForEachChainKeyedByChainReference()
    {
        var tileset = EmptyTileset();
        var tile = new Tile { ID = 5, Width = 0, Height = 0 };
        tile.Animation.Add(new Frame { TileID = 6, Duration = 100 });
        tile.Properties.Add(new StringProperty { Name = "Name", Value = "RiseUp" });
        tileset.Tiles.Add(tile);

        var acls = TiledAnimationToAchjMapper.Map(tileset, out var entryTileIdsByChain, out _);

        var chain = Assert.Single(acls.AnimationChains);
        Assert.Equal((uint)5, entryTileIdsByChain[chain]);
    }
}
