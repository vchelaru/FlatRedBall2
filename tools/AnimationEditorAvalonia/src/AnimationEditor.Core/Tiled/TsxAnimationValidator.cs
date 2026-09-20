using DotTiled;
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
        var issues = new List<TsxGroupIssue>();
        var animatedTilesById = tileset.Tiles.Where(t => t.Animation.Count > 0).ToDictionary(t => t.ID);
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
            }
        }

        return issues;
    }
}
