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

        FoldEnumReferences(tileset, XDocument.Load(path).Root);
        return tileset;
    }

    /// <summary>Swaps every enum-typed property DotTiled flattened for an <see cref="EnumReferenceProperty"/>.
    /// Covers tileset, tile, tile object-layer, object and wangset properties, plus members nested
    /// in class properties.</summary>
    private static void FoldEnumReferences(Tileset tileset, XElement? root)
    {
        if (root is null)
            return;

        FoldProperties(tileset.Properties, root);

        var tileElements = root.Elements("tile").ToDictionary(e => (string?)e.Attribute("id") ?? "", e => e);
        foreach (var tile in tileset.Tiles)
        {
            if (!tileElements.TryGetValue(tile.ID.ToString(), out var tileElement))
                continue;
            FoldProperties(tile.Properties, tileElement);
            if (!tile.ObjectLayer.HasValue || tileElement.Element("objectgroup") is not { } group)
                continue;
            FoldProperties(tile.ObjectLayer.Value.Properties, group);
            var objectElements = group.Elements("object").ToList();
            var objects = tile.ObjectLayer.Value.Objects;
            if (objectElements.Count == objects.Count)
                for (var i = 0; i < objects.Count; i++)
                    FoldProperties(objects[i].Properties, objectElements[i]);
        }

        var wangsetElements = root.Element("wangsets")?.Elements("wangset").ToList() ?? [];
        if (wangsetElements.Count != tileset.Wangsets.Count)
            return;
        for (var i = 0; i < wangsetElements.Count; i++)
        {
            FoldProperties(tileset.Wangsets[i].Properties, wangsetElements[i]);
            var colorElements = wangsetElements[i].Elements("wangcolor").ToList();
            if (colorElements.Count == tileset.Wangsets[i].WangColors.Count)
                for (var c = 0; c < colorElements.Count; c++)
                    FoldProperties(tileset.Wangsets[i].WangColors[c].Properties, colorElements[c]);
        }
    }

    /// <param name="owner">The element whose <c>&lt;properties&gt;</c> child lists
    /// <paramref name="properties"/>; skipped when the two do not line up one to one.</param>
    private static void FoldProperties(IList<IProperty> properties, XElement owner)
    {
        var elements = owner.Element("properties")?.Elements("property").ToList();
        if (elements is null || elements.Count != properties.Count)
            return;

        for (var i = 0; i < properties.Count; i++)
        {
            var element = elements[i];
            if ((string?)element.Attribute("name") != properties[i].Name)
                continue;

            if (properties[i] is ClassProperty classProperty)
            {
                FoldProperties(classProperty.Value, element);
                continue;
            }

            var propertyType = (string?)element.Attribute("propertytype");
            if (!string.IsNullOrEmpty(propertyType) && properties[i] is StringProperty or IntProperty)
                properties[i] = new EnumReferenceProperty
                {
                    Name = properties[i].Name,
                    PropertyType = propertyType,
                    Value = (string?)element.Attribute("value") ?? "",
                    StorageType = (string?)element.Attribute("type"),
                };
        }
    }
}
