using AnimationEditor.Core.Tiled;
using DotTiled;
using System;
using Xunit;

namespace AnimationEditor.Core.Tests.Tiled;

public class TsxAnimationValidatorTests
{
    // 4 columns, so tile 9 is one column right of tile 8 (row 2).
    private static Tileset TilesetWithColumns(int columns) => new()
    {
        Name = "Heroes",
        TileWidth = 16,
        TileHeight = 16,
        TileCount = 64,
        Columns = columns,
    };

    [Fact]
    public void Validate_ConsistentGroup_ReturnsNoIssues()
    {
        var tileset = TilesetWithColumns(4);
        var anchor = new Tile { ID = 8, Width = 0, Height = 0 };
        anchor.Animation.Add(new Frame { TileID = 8, Duration = 150 });
        anchor.Animation.Add(new Frame { TileID = 12, Duration = 150 });
        tileset.Tiles.Add(anchor);

        var satellite = new Tile { ID = 9, Width = 0, Height = 0 };
        satellite.Animation.Add(new Frame { TileID = 9, Duration = 150 });
        satellite.Animation.Add(new Frame { TileID = 13, Duration = 150 });
        satellite.Properties.Add(new IntProperty { Name = "ParentId", Value = 8 });
        tileset.Tiles.Add(satellite);

        var issues = TsxAnimationValidator.Validate(tileset);

        Assert.Empty(issues);
    }

    [Fact]
    public void Validate_SatelliteDurationOutOfLockstep_ReturnsIssue()
    {
        var tileset = TilesetWithColumns(4);
        var anchor = new Tile { ID = 8, Width = 0, Height = 0 };
        anchor.Animation.Add(new Frame { TileID = 8, Duration = 150 });
        anchor.Animation.Add(new Frame { TileID = 12, Duration = 150 });
        tileset.Tiles.Add(anchor);

        // Tile ids stay in lockstep, but frame 1's duration was hand-edited to 999 -- this
        // duration is silently discarded and overwritten to the anchor's on the next save
        // (NativeTsxAnimationSync never reads a satellite's own Duration), so it must be flagged.
        var satellite = new Tile { ID = 9, Width = 0, Height = 0 };
        satellite.Animation.Add(new Frame { TileID = 9, Duration = 150 });
        satellite.Animation.Add(new Frame { TileID = 13, Duration = 999 });
        satellite.Properties.Add(new IntProperty { Name = "ParentId", Value = 8 });
        tileset.Tiles.Add(satellite);

        var issues = TsxAnimationValidator.Validate(tileset);

        var issue = Assert.Single(issues);
        Assert.Equal((uint)9, issue.TileId);
        Assert.Contains("lockstep", issue.Message);
    }

    [Fact]
    public void Validate_SatelliteFrameOutOfLockstep_ReturnsIssue()
    {
        var tileset = TilesetWithColumns(4);
        var anchor = new Tile { ID = 8, Width = 0, Height = 0 };
        anchor.Animation.Add(new Frame { TileID = 8, Duration = 150 });
        anchor.Animation.Add(new Frame { TileID = 12, Duration = 150 });
        tileset.Tiles.Add(anchor);

        // Second frame should be tile 13 (12 + 1 column) to stay in lockstep -- hand-edited to 14 instead.
        var satellite = new Tile { ID = 9, Width = 0, Height = 0 };
        satellite.Animation.Add(new Frame { TileID = 9, Duration = 150 });
        satellite.Animation.Add(new Frame { TileID = 14, Duration = 150 });
        satellite.Properties.Add(new IntProperty { Name = "ParentId", Value = 8 });
        tileset.Tiles.Add(satellite);

        var issues = TsxAnimationValidator.Validate(tileset);

        var issue = Assert.Single(issues);
        Assert.Equal((uint)9, issue.TileId);
        Assert.Contains("lockstep", issue.Message);
    }

    [Fact]
    public void Validate_BackwardParentId_ReferencesAnchorAtLargerColumnOrRow_ReturnsIssue()
    {
        // Anchor at tile 9 (column 1, row 2, 4 columns/tileset). Tile 8 (column 0, same row)
        // points ParentId at it -- physically to the LEFT of the anchor, a footprint shape
        // AnimationEditor's own UI can never produce (a group only ever grows right/down from its
        // anchor). TiledAnimationToAchjMapper now surfaces tile 8 as its own independent chain (its
        // ParentId grouping never takes effect), so the validator must flag it.
        var tileset = TilesetWithColumns(4);
        var anchor = new Tile { ID = 9, Width = 0, Height = 0 };
        anchor.Animation.Add(new Frame { TileID = 9, Duration = 150 });
        tileset.Tiles.Add(anchor);

        var backwardTile = new Tile { ID = 8, Width = 0, Height = 0 };
        backwardTile.Animation.Add(new Frame { TileID = 8, Duration = 150 });
        backwardTile.Properties.Add(new IntProperty { Name = "ParentId", Value = 9 });
        tileset.Tiles.Add(backwardTile);

        var issues = TsxAnimationValidator.Validate(tileset);

        var issue = Assert.Single(issues);
        Assert.Equal((uint)9, issue.AnchorTileId);
        Assert.Equal((uint)8, issue.TileId);
        Assert.Contains("backward", issue.Message);
    }

    [Fact]
    public void Validate_ChainedParentId_ReferencesTileThatIsItselfASatellite_ReturnsIssue()
    {
        // A -- true anchor. B -- ParentId=A.ID, a legitimate satellite. C -- ParentId=B.ID,
        // chained through a satellite rather than a true anchor. TiledAnimationToAchjMapper now
        // surfaces C as its own independent chain (its ParentId grouping doesn't take effect), so
        // the validator must flag this instead of silently treating it as a valid group -- same
        // spirit as the "ParentId doesn't reference an animated tile" check just above.
        var tileset = TilesetWithColumns(4);
        var anchorA = new Tile { ID = 8, Width = 0, Height = 0 };
        anchorA.Animation.Add(new Frame { TileID = 8, Duration = 150 });
        tileset.Tiles.Add(anchorA);

        var satelliteB = new Tile { ID = 9, Width = 0, Height = 0 };
        satelliteB.Animation.Add(new Frame { TileID = 9, Duration = 150 });
        satelliteB.Properties.Add(new IntProperty { Name = "ParentId", Value = 8 });
        tileset.Tiles.Add(satelliteB);

        var chainedC = new Tile { ID = 10, Width = 0, Height = 0 };
        chainedC.Animation.Add(new Frame { TileID = 10, Duration = 150 });
        chainedC.Properties.Add(new IntProperty { Name = "ParentId", Value = 9 });
        tileset.Tiles.Add(chainedC);

        var issues = TsxAnimationValidator.Validate(tileset);

        var issue = Assert.Single(issues);
        Assert.Equal((uint)9, issue.AnchorTileId);
        Assert.Equal((uint)10, issue.TileId);
        Assert.Contains("itself a satellite", issue.Message);
    }

    [Fact]
    public void Validate_ColumnsIsZero_ThrowsInsteadOfDivideByZero()
    {
        var tileset = TilesetWithColumns(0);
        var anchor = new Tile { ID = 8, Width = 0, Height = 0 };
        anchor.Animation.Add(new Frame { TileID = 8, Duration = 150 });
        tileset.Tiles.Add(anchor);

        var satellite = new Tile { ID = 9, Width = 0, Height = 0 };
        satellite.Animation.Add(new Frame { TileID = 9, Duration = 150 });
        satellite.Properties.Add(new IntProperty { Name = "ParentId", Value = 8 });
        tileset.Tiles.Add(satellite);

        Assert.Throws<InvalidOperationException>(() => TsxAnimationValidator.Validate(tileset));
    }

    [Fact]
    public void Validate_ParentIdReferencesNonAnimatedTile_ReturnsIssue()
    {
        var tileset = TilesetWithColumns(4);
        var satellite = new Tile { ID = 9, Width = 0, Height = 0 };
        satellite.Animation.Add(new Frame { TileID = 9, Duration = 150 });
        satellite.Properties.Add(new IntProperty { Name = "ParentId", Value = 8 });
        tileset.Tiles.Add(satellite);

        var issues = TsxAnimationValidator.Validate(tileset);

        var issue = Assert.Single(issues);
        Assert.Equal((uint)8, issue.AnchorTileId);
        Assert.Contains("does not reference an animated tile", issue.Message);
    }

    [Fact]
    public void Validate_TilesetHasDuplicateAnimatedTileIds_ThrowsClearErrorInsteadOfRawDictionaryException()
    {
        // Two animated <tile> elements sharing one id used to hit the internal id-keyed
        // dictionary's own unchecked ArgumentException instead of this codebase's "fail loud with
        // a clear message" precedent (same category as the Columns<=0 guard above).
        var tileset = TilesetWithColumns(4);
        var first = new Tile { ID = 8, Width = 0, Height = 0 };
        first.Animation.Add(new Frame { TileID = 8, Duration = 150 });
        tileset.Tiles.Add(first);
        var second = new Tile { ID = 8, Width = 0, Height = 0 };
        second.Animation.Add(new Frame { TileID = 8, Duration = 150 });
        tileset.Tiles.Add(second);

        var exception = Assert.Throws<InvalidOperationException>(() => TsxAnimationValidator.Validate(tileset));

        Assert.Contains("8", exception.Message);
    }
}
