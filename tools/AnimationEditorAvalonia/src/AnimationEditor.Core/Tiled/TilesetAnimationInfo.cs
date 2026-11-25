using AnimationEditor.Core.IO;
using AnimationEditor.Core.Paths;
using AnimationEditor.Core.Rendering;
using FlatRedBall2.AnimationEditorCommon;
using System;

namespace AnimationEditor.Core.Tiled;

/// <summary>A single mapped Tiled tile-animation frame: which tile id to show, for how long (ms).</summary>
public readonly record struct MappedFrame(uint TileId, int Duration);

/// <summary>The subset of a Tiled tileset's geometry needed to map achx/achj frame rects onto
/// tile ids. Deliberately a plain data record (not <c>DotTiled.Tileset</c> directly) so mapping
/// logic has no DotTiled dependency and stays testable without constructing one.</summary>
public sealed record TilesetAnimationInfo
{
    public required int TileWidth { get; init; }
    public required int TileHeight { get; init; }
    public required int ColumnCount { get; init; }
    /// <summary>Total number of tiles the tileset declares (Tiled's <c>tilecount</c> attribute).
    /// A partial last row is normal -- this is not necessarily <c>ColumnCount</c> times a whole
    /// number of rows -- so a computed tile id is only ever validated against this directly,
    /// never against a derived row count.</summary>
    public required int TileCount { get; init; }
    public int Margin { get; init; }
    public int TileSpacing { get; init; }
    /// <summary>The same geometry as a <see cref="TileGrid"/>, for the shared cell math.</summary>
    public TileGrid Grid => new(TileWidth, TileHeight, Margin, TileSpacing);
    public required string ImageFileName { get; init; }
    /// <summary>The folder a relative <see cref="ImageFileName"/> or frame texture name is resolved against.</summary>
    public string Folder { get; init; } = string.Empty;
    /// <summary>Whether <paramref name="textureName"/> is this tileset's image, compared by resolved path.</summary>
    public bool IsImage(string textureName) =>
        textureName == ImageFileName || Resolve(textureName) == Resolve(ImageFileName);
    private FilePath Resolve(string name) => new(TexturePathHelper.ResolveDisplayPath(name, Folder));
    /// <summary>Required only when the source uses <see cref="TextureCoordinateType.UV"/> frames,
    /// to convert normalized (0-1) coordinates to pixels.</summary>
    public int? TextureWidth { get; init; }
    public int? TextureHeight { get; init; }
}

/// <summary>Where a frame's rect lies on a tileset's image and grid.</summary>
internal static class TiledFrameGeometry
{
    internal const float Epsilon = 0.001f;

    internal static (float Left, float Top, float Width, float Height) FrameRectPixels(
        AnimationFrameSave frame, TextureCoordinateType coordinateType, TilesetAnimationInfo tilesetInfo)
    {
        if (coordinateType == TextureCoordinateType.UV)
        {
            var textureWidth = tilesetInfo.TextureWidth!.Value;
            var textureHeight = tilesetInfo.TextureHeight!.Value;
            return (
                frame.LeftCoordinate * textureWidth,
                frame.TopCoordinate * textureHeight,
                (frame.RightCoordinate - frame.LeftCoordinate) * textureWidth,
                (frame.BottomCoordinate - frame.TopCoordinate) * textureHeight);
        }

        return (
            frame.LeftCoordinate,
            frame.TopCoordinate,
            frame.RightCoordinate - frame.LeftCoordinate,
            frame.BottomCoordinate - frame.TopCoordinate);
    }

    /// <summary>Resolves a frame's pixel rect to the single Tiled tile id it lands on, or <see
    /// langword="null"/> if it doesn't cleanly map (off-grid, wrong size, out of range). Display-only
    /// (Inspector "Tile: N" readout, issue #1182); unrelated to which tile OWNS a chain's animation
    /// (see <see cref="MultiTileToTiledAnimationMapper"/>'s entry-tile-id hint tracking).</summary>
    internal static uint? TryGetTileId(AnimationFrameSave frame, TextureCoordinateType coordinateType, TilesetAnimationInfo tilesetInfo)
    {
        if (coordinateType == TextureCoordinateType.UV && (tilesetInfo.TextureWidth is null or <= 0 || tilesetInfo.TextureHeight is null or <= 0))
            return null;

        var (left, top, width, height) = FrameRectPixels(frame, coordinateType, tilesetInfo);
        if (Math.Abs(width - tilesetInfo.TileWidth) > Epsilon || Math.Abs(height - tilesetInfo.TileHeight) > Epsilon)
            return null;
        if (tilesetInfo.Grid.TryLocate(left, top, width, height, Epsilon) is not { } footprint)
            return null;

        var (column, row, _, _) = footprint;
        if (column < 0 || row < 0 || column >= tilesetInfo.ColumnCount)
            return null;

        var tileId = (uint)((row * tilesetInfo.ColumnCount) + column);
        return tileId < tilesetInfo.TileCount ? tileId : null;
    }
}
