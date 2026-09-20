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

    /// <summary>
    /// Builds the achx-shaped model in UV (0-1) coordinates and Second-based durations -- the
    /// same invariant every achx load produces (see <c>ProjectManager.NormalizeCoordinatesToUv</c>'s
    /// doc comment: the in-memory representation is always UV). Storing raw pixel values here
    /// with <see cref="TextureCoordinateType.Pixel"/> instead was a real shipped bug (issue #1140
    /// follow-up): nothing downstream treats <c>Pixel</c> as "already converted, leave it alone,"
    /// so every consumer re-multiplied an already-in-pixels value by the texture size again (a
    /// 200ms duration rendered as "200 seconds"; a 864px coordinate rendered as 864*2048).
    /// </summary>
    /// <param name="entryTileIdsByChain">Every returned chain's own source tile id, keyed by
    /// chain object reference. A native-tsx save (<see cref="MultiTileToTiledAnimationMapper"/>)
    /// must feed this back in as its <c>knownEntryTileIds</c> so a chain whose owning tile id
    /// doesn't match its own first frame -- a perfectly ordinary hand-authored Tiled pattern --
    /// keeps writing to the same tile instead of relocating (and orphaning the original tile)
    /// every save. Keyed by reference, not name, so a rename doesn't look like delete+create.</param>
    public static AnimationChainListSave Map(Tileset tileset, out IReadOnlyDictionary<AnimationChainSave, uint> entryTileIdsByChain)
    {
        var imageFileName = tileset.Image.HasValue ? (tileset.Image.Value.Source.HasValue ? tileset.Image.Value.Source.Value : string.Empty) : string.Empty;
        var (textureWidth, textureHeight) = GetTextureSize(tileset);
        var acls = new AnimationChainListSave();
        var entryTileIds = new Dictionary<AnimationChainSave, uint>(ReferenceEqualityComparer.Instance);

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
                    LeftCoordinate = (col * tileset.TileWidth) / (float)textureWidth,
                    TopCoordinate = (row * tileset.TileHeight) / (float)textureHeight,
                    RightCoordinate = ((col + footprintColumns) * tileset.TileWidth) / (float)textureWidth,
                    BottomCoordinate = ((row + footprintRows) * tileset.TileHeight) / (float)textureHeight,
                    FrameLength = frame.Duration / 1000f,
                });
            }

            acls.AnimationChains.Add(chain);
            entryTileIds[chain] = anchor.ID;
        }

        entryTileIdsByChain = entryTileIds;
        return acls;
    }

    /// <summary>
    /// The image's own pixel dimensions when the tsx records them (as every real Tiled-authored
    /// file does), otherwise the size implied by the tile grid itself
    /// (<c>Columns * TileWidth</c> by however many rows <c>TileCount</c> needs) -- keeps UV
    /// conversion well-defined instead of dividing by a missing/zero size.
    /// </summary>
    private static (int Width, int Height) GetTextureSize(Tileset tileset)
    {
        if (tileset.Image.HasValue)
        {
            var image = tileset.Image.Value;
            if (image.Width.HasValue && image.Height.HasValue && image.Width.Value > 0 && image.Height.Value > 0)
                return (image.Width.Value, image.Height.Value);
        }

        var rows = (tileset.TileCount + tileset.Columns - 1) / tileset.Columns;
        return (tileset.Columns * tileset.TileWidth, rows * tileset.TileHeight);
    }

    private static string ChainName(Tile tile)
    {
        var name = tile.Properties.OfType<StringProperty>().FirstOrDefault(p => p.Name == NamePropertyName)?.Value;
        return string.IsNullOrEmpty(name) ? $"ID:{tile.ID}" : name;
    }

    /// <summary>A negative <c>ParentId</c> (hand-edited or corrupt file) is treated the same as a
    /// missing one rather than unchecked-cast into a huge <see cref="uint"/> -- silently wrapping
    /// -1 into 4294967295 would send lookups into nonsense territory instead of failing loudly.</summary>
    internal static uint? GetParentId(Tile tile)
    {
        var property = tile.Properties.FirstOrDefault(p => p.Name == ParentIdPropertyName);
        return property switch
        {
            IntProperty { Value: >= 0 } intProperty => (uint)intProperty.Value,
            _ => null,
        };
    }
}
