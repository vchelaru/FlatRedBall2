using AnimationEditor.Core.Tiled;
using DotTiled;
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
}
