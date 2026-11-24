using DotTiled;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Tiled;

/// <summary>
/// The <c>Name</c> and <c>ParentId</c> properties of a loaded <c>.tsx</c> that the editor reads but did not
/// write (issue #2014). Read once when the file is loaded: after a chain is saved on a tile, the tileset no
/// longer tells them apart.
/// </summary>
/// <param name="Names">By tile id: a string <c>Name</c> on a tile that is not a chain's own, or one that
/// names no chain (<see cref="IsNoName"/>).</param>
/// <param name="ParentIdTileIds">Unanimated tiles with a <c>ParentId</c>.</param>
public sealed record TsxUserProperties(IReadOnlyDictionary<uint, string> Names, IReadOnlySet<uint> ParentIdTileIds)
{
    public static TsxUserProperties Read(Tileset tileset)
    {
        TiledAnimationToAchjMapper.Map(tileset, out var ownTileIdsByChain, out _, out _);
        var chainTileIds = ownTileIdsByChain.Values.ToHashSet();

        var names = new Dictionary<uint, string>();
        var parentIdTileIds = new HashSet<uint>();
        foreach (var tile in tileset.Tiles)
        {
            if (TiledAnimationToAchjMapper.FindNameProperty(tile) is { Value: var name }
                && (!chainTileIds.Contains(tile.ID) || IsNoName(tile, name)))
                names[tile.ID] = name;
            if (tile.Animation.Count == 0 && TiledAnimationToAchjMapper.GetParentId(tile) is not null)
                parentIdTileIds.Add(tile.ID);
        }
        return new TsxUserProperties(names, parentIdTileIds);
    }

    /// <summary>Whether a chain on the tile can replace this name: it is empty, or the synthetic name of a
    /// tile without one.</summary>
    internal static bool IsNoName(Tile tile, string name) =>
        name == "" || name == TiledAnimationToAchjMapper.SyntheticChainName(tile.ID);
}
