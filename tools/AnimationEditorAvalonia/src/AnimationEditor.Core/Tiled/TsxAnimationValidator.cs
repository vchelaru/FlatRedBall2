using DotTiled;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Tiled;

/// <summary>One problem found on a satellite tile of a multi-tile animation group -- see
/// <see cref="TsxAnimationValidator"/>.</summary>
public sealed record TsxGroupIssue(uint AnchorTileId, uint TileId, string Message);

/// <summary>
/// Validates multi-tile animation groups in a Tiled <see cref="Tileset"/> for a native <c>.tsx</c>
/// project (issue #1140). AnimationEditor's own UI can only ever produce a consistent group (it
/// controls both the anchor and every satellite's frames), so these issues are only reachable
/// through a hand-edited <c>.tsx</c> or one hand-authored directly in Tiled -- e.g. editing a
/// satellite tile's animation in Tiled without knowing it's a group member. Flip/color/shape/
/// off-grid checks aren't needed here: those fields simply don't exist on the reverse-mapped model
/// (<see cref="TiledAnimationToAchjMapper"/> never produces them), and the native-project UI hides
/// the controls that would let a user introduce them in the first place.
/// </summary>
public static class TsxAnimationValidator
{
    public static IReadOnlyList<TsxGroupIssue> Validate(Tileset tileset)
    {
        // Every dx/dy/expectedTileId computation below is "% columns" / "/ columns" -- a corrupt
        // tsx with Columns <= 0 would either divide by zero or unchecked-cast a negative value
        // into a huge uint. Same guard as TiledAnimationToAchjMapper.Map, which this validator's
        // math mirrors.
        if (tileset.Columns <= 0)
            throw new InvalidOperationException(
                $"Can't validate tile animations: tileset \"{tileset.Name}\" has Columns={tileset.Columns}, which isn't a valid tile-grid width.");

        var issues = new List<TsxGroupIssue>();
        var animatedTilesById = new Dictionary<uint, Tile>();
        foreach (var tile in tileset.Tiles.Where(t => t.Animation.Count > 0))
            if (!animatedTilesById.TryAdd(tile.ID, tile))
                throw new InvalidOperationException(
                    $"Can't validate tile animations: tileset \"{tileset.Name}\" has more than one animated tile with id {tile.ID}, which isn't valid Tiled data.");
        var columns = (uint)tileset.Columns;

        foreach (var tile in tileset.Tiles)
        {
            if (TiledAnimationToAchjMapper.GetParentId(tile) is not { } anchorId)
                continue;

            if (!animatedTilesById.TryGetValue(anchorId, out var anchor))
            {
                issues.Add(new TsxGroupIssue(anchorId, tile.ID,
                    $"tile {tile.ID}: ParentId {anchorId} does not reference an animated tile."));
                continue;
            }

            if (TiledAnimationToAchjMapper.GetParentId(anchor) is { } anchorsOwnParentId
                && animatedTilesById.ContainsKey(anchorsOwnParentId))
            {
                issues.Add(new TsxGroupIssue(anchorId, tile.ID,
                    $"tile {tile.ID}: ParentId {anchorId} references tile {anchorId}, which is itself a satellite (chained/nested ParentId) rather than a true anchor."));
                continue;
            }

            // A ParentId pointing "backward" -- to an anchor with a larger column or row than the
            // satellite itself -- is a footprint shape AnimationEditor's own UI can never produce
            // (a group only ever grows right/down from its anchor). Must be checked before the
            // dx/dy lockstep math below: that math is uint subtraction and underflows for exactly
            // this case, which can coincidentally wrap back around to the tile's own correct
            // TileID and report zero issues instead of flagging the broken reference.
            if ((tile.ID % columns) < (anchorId % columns) || (tile.ID / columns) < (anchorId / columns))
            {
                issues.Add(new TsxGroupIssue(anchorId, tile.ID,
                    $"tile {tile.ID}: ParentId {anchorId} references an anchor at a larger column or row than tile {tile.ID} itself (backward offset) -- AnimationEditor's own UI only ever grows a group's footprint to the right/below its anchor."));
                continue;
            }

            if (anchor.Animation.Count != tile.Animation.Count)
            {
                issues.Add(new TsxGroupIssue(anchorId, tile.ID,
                    $"tile {tile.ID}: has {tile.Animation.Count} animation frame(s) but its group anchor (tile {anchorId}) has {anchor.Animation.Count}."));
                continue;
            }

            var dx = (tile.ID % columns) - (anchorId % columns);
            var dy = (tile.ID / columns) - (anchorId / columns);

            for (var i = 0; i < anchor.Animation.Count; i++)
            {
                var anchorFrameId = anchor.Animation[i].TileID;
                var expectedTileId = ((anchorFrameId / columns) + dy) * columns + ((anchorFrameId % columns) + dx);
                if (tile.Animation[i].TileID != expectedTileId)
                {
                    issues.Add(new TsxGroupIssue(anchorId, tile.ID,
                        $"tile {tile.ID}: frame {i} is tile {tile.Animation[i].TileID}, expected {expectedTileId} to stay in lockstep with anchor tile {anchorId}."));
                }

                var expectedDuration = anchor.Animation[i].Duration;
                if (tile.Animation[i].Duration != expectedDuration)
                {
                    issues.Add(new TsxGroupIssue(anchorId, tile.ID,
                        $"tile {tile.ID}: frame {i} has duration {tile.Animation[i].Duration}, expected {expectedDuration} to stay in lockstep with anchor tile {anchorId}."));
                }
            }
        }

        return issues;
    }
}
