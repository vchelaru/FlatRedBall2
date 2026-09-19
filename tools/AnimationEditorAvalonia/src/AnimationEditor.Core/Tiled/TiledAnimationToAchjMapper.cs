using DotTiled;
using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Tiled;

/// <summary>
/// Maps a Tiled <see cref="Tileset"/>'s per-tile animations onto an <see
/// cref="AnimationChainListSave"/> — the reverse of <see cref="AchjToTiledAnimationMapper"/>, used
/// to open a <c>.tsx</c> as a native AnimationEditor project (issue #1140) instead of pushing an
/// achx/achj onto an associated tileset.
/// </summary>
/// <remarks>
/// One animated tile with no <see cref="ParentIdPropertyName"/> property becomes one chain (an
/// "anchor"). A tile carrying a <see cref="ParentIdPropertyName"/> property is a multi-tile-group
/// "satellite" -- it is folded into its anchor's chain as part of a wider per-frame rect instead of
/// becoming its own chain; the satellite's own <c>Animation</c> frames are not read here, only its
/// static grid position relative to the anchor (see <see cref="MultiTileToTiledAnimationMapper"/>
/// for why that's safe to trust). A chain's name is the tile's <see cref="NamePropertyName"/>
/// property when present, otherwise a synthetic <c>"ID:{tileId}"</c> label.
/// </remarks>
public static class TiledAnimationToAchjMapper
{
    /// <summary>Custom tile property AnimationEditor writes/reads for a user-assigned animation name.</summary>
    public const string NamePropertyName = "Name";

    /// <summary>Custom tile property marking a tile as a multi-tile-group satellite, valued with its anchor tile's id.</summary>
    public const string ParentIdPropertyName = "ParentId";

    public static AnimationChainListSave Map(Tileset tileset)
    {
        var imageFileName = tileset.Image.HasValue ? (tileset.Image.Value.Source.HasValue ? tileset.Image.Value.Source.Value : string.Empty) : string.Empty;
        var acls = new AnimationChainListSave
        {
            CoordinateType = TextureCoordinateType.Pixel,
            TimeMeasurementUnit = TimeMeasurementUnit.Millisecond,
        };

        var animatedTiles = tileset.Tiles.Where(t => t.Animation.Count > 0).ToList();
        var parentIdByTileId = animatedTiles.ToDictionary(t => t.ID, GetParentId);
        var satellitesByAnchor = animatedTiles
            .Where(t => parentIdByTileId[t.ID].HasValue)
            .ToLookup(t => parentIdByTileId[t.ID]!.Value);

        var columns = (uint)tileset.Columns;

        foreach (var anchor in animatedTiles.Where(t => !parentIdByTileId[t.ID].HasValue))
        {
            var chain = new AnimationChainSave { Name = ChainName(anchor) };

            var anchorCol = anchor.ID % columns;
            var anchorRow = anchor.ID / columns;

            var footprintColumns = 1u;
            var footprintRows = 1u;
            foreach (var satellite in satellitesByAnchor[anchor.ID])
            {
                var dx = (satellite.ID % columns) - anchorCol;
                var dy = (satellite.ID / columns) - anchorRow;
                footprintColumns = System.Math.Max(footprintColumns, dx + 1);
                footprintRows = System.Math.Max(footprintRows, dy + 1);
            }

            foreach (var frame in anchor.Animation)
            {
                var col = frame.TileID % columns;
                var row = frame.TileID / columns;

                chain.Frames.Add(new AnimationFrameSave
                {
                    TextureName = imageFileName,
                    LeftCoordinate = col * tileset.TileWidth,
                    TopCoordinate = row * tileset.TileHeight,
                    RightCoordinate = (col + footprintColumns) * tileset.TileWidth,
                    BottomCoordinate = (row + footprintRows) * tileset.TileHeight,
                    FrameLength = frame.Duration,
                });
            }

            acls.AnimationChains.Add(chain);
        }

        return acls;
    }

    private static string ChainName(Tile tile)
    {
        var name = tile.Properties.OfType<StringProperty>().FirstOrDefault(p => p.Name == NamePropertyName)?.Value;
        return string.IsNullOrEmpty(name) ? $"ID:{tile.ID}" : name;
    }

    internal static uint? GetParentId(Tile tile)
    {
        var property = tile.Properties.FirstOrDefault(p => p.Name == ParentIdPropertyName);
        return property switch
        {
            IntProperty intProperty => (uint)intProperty.Value,
            _ => null,
        };
    }
}
