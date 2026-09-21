using DotTiled;
using System;
using System.IO;
using System.Linq;

namespace AnimationEditor.Core.Tiled;

/// <summary>
/// Checks whether a Tiled <see cref="Tileset"/> can be opened as a native AnimationEditor project
/// (issue #1140) without losing data on the first save. Deliberately reuses <see
/// cref="TsxWriter"/> itself as the source of truth (a dry-run write to a throwaway stream) rather
/// than duplicating its wangsets/transformations/object-layer/property-type checks -- so this stays
/// in sync automatically if <see cref="TsxWriter"/>'s supported subset ever changes.
/// </summary>
public static class TsxCompatibilityChecker
{
    /// <returns><see langword="false"/> when opening natively would crash on the first save;
    /// <paramref name="blockingReason"/> is <see cref="TsxWriter"/>'s own explanation in that case.</returns>
    public static bool CheckOpenCompatibility(Tileset tileset, out string? blockingReason)
    {
        // Neither mapper accounts for margin/spacing when placing tiles (TiledAnimationToAchjMapper
        // puts tile N at col*tilewidth; MultiTileToTiledAnimationMapper refuses per chain), so such
        // a tileset would open with every frame rect shifted off its real pixels and no way to
        // save. Refuse up front instead.
        // An image-collection tileset (one image per tile, no shared sheet) has no tile grid for
        // any of this editor's tile-id math to work against.
        if (!tileset.Image.HasValue || tileset.Tiles.Any(t => t.Image.HasValue))
        {
            blockingReason = "the tileset is an image collection (one image per tile), which AnimationEditor doesn't support yet -- only a single-image tileset can be opened.";
            return false;
        }

        if (tileset.Margin != 0 || tileset.Spacing != 0)
        {
            blockingReason = $"the tileset uses margin ({tileset.Margin}) or spacing ({tileset.Spacing}), which AnimationEditor doesn't support yet.";
            return false;
        }

        try
        {
            TsxWriter.Write(tileset, Stream.Null);
            blockingReason = null;
            return true;
        }
        catch (NotSupportedException ex)
        {
            blockingReason = ex.Message;
            return false;
        }
    }
}
