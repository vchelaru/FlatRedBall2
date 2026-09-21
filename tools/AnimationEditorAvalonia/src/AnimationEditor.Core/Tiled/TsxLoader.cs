using DotTiled;
using DotTiled.Serialization;
using System.Linq;
using System.Xml.Linq;

namespace AnimationEditor.Core.Tiled;

/// <summary>
/// The one way this editor reads a <c>.tsx</c> from disk. Wraps DotTiled's loader with the fixups
/// its model needs to round-trip a real Tiled file through <see cref="TsxWriter"/> without loss.
/// </summary>
public static class TsxLoader
{
    /// <summary>
    /// Loads <paramref name="path"/> and folds a Tiled 1.9-era <c>class="..."</c> tile attribute
    /// into <see cref="Tile.Type"/> (1.10 and later write <c>type</c>, which is all DotTiled reads).
    /// A regenerated tile then keeps its class (as <c>type</c>, exactly what Tiled itself writes
    /// on resave), and <see cref="NativeTsxAnimationSync"/> no longer sees a class-only tile as
    /// empty and deletes it.
    /// </summary>
    public static Tileset LoadTileset(string path)
    {
        var tileset = Loader.Default().LoadTileset(path);

        var classByTileId = XDocument.Load(path).Root?
            .Elements("tile")
            .Select(e => (Id: (string?)e.Attribute("id"), Class: (string?)e.Attribute("class"), Type: (string?)e.Attribute("type")))
            .Where(t => t.Id is not null && !string.IsNullOrEmpty(t.Class) && string.IsNullOrEmpty(t.Type))
            .ToDictionary(t => uint.Parse(t.Id!), t => t.Class!);
        if (classByTileId is { Count: > 0 })
            foreach (var tile in tileset.Tiles)
                if (string.IsNullOrEmpty(tile.Type) && classByTileId.TryGetValue(tile.ID, out var tileClass))
                    tile.Type = tileClass;

        return tileset;
    }
}
