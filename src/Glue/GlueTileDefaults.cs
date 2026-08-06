using FlatRedBall2.Glue.Model;

namespace FlatRedBall2.Glue;

/// <summary>
/// How Glue authors a <c>TileShapeCollection</c>'s geometry.
/// </summary>
/// <remarks>
/// Values are pinned because Glue writes the bare ordinal. **Do not share a decoder with
/// <see cref="TileNodeNetworkCreationOptions"/>** — the two read from similarly named keys and their
/// numbering disagrees from 2 onward, so one shared path silently misreads one of them.
/// </remarks>
public enum CollisionCreationOptions
{
    /// <summary>No geometry; the game fills it itself.</summary>
    Empty = 0,

    /// <summary>A solid rectangle of tiles.</summary>
    FillCompletely = 1,

    /// <summary>A hollow rectangle of tiles.</summary>
    BorderOutline = 2,

    /// <summary>Every tile carrying a named custom property.</summary>
    FromProperties = 3,

    /// <summary>Every tile of a named type.</summary>
    FromType = 4,

    /// <summary>A named collision layer in the map.</summary>
    FromLayer = 5,

    /// <summary>A named collision object already in the map.</summary>
    FromMapCollision = 6,
}

/// <summary>How Glue authors a tile node network's geometry.</summary>
/// <remarks>See the warning on <see cref="CollisionCreationOptions"/>: the ordinals differ.</remarks>
public enum TileNodeNetworkCreationOptions
{
    /// <summary>No nodes.</summary>
    Empty = 0,

    /// <summary>A solid rectangle of nodes.</summary>
    FillCompletely = 1,

    /// <summary>Every tile carrying a named custom property.</summary>
    FromProperties = 2,

    /// <summary>Every tile of a named type.</summary>
    FromType = 3,

    /// <summary>A named layer in the map.</summary>
    FromLayer = 4,
}

/// <summary>
/// The values Glue falls back to when a tile-related key is absent from an object's property bag.
/// </summary>
/// <remarks>
/// Glue does not fall back to <c>default(T)</c> — it reflects the editor view-model's
/// <c>[DefaultValue]</c>. The difference is not cosmetic: a collection whose grid size reads
/// <c>0</c> instead of <c>16</c> produces no geometry at all, and reports nothing wrong.
/// </remarks>
internal static class GlueTileDefaults
{
    /// <summary>Which shape the collection's geometry is built from.</summary>
    internal static CollisionCreationOptions CreationOptions(NamedObjectSave save) =>
        (CollisionCreationOptions)save.Properties.GetValue<int>("CollisionCreationOptions");

    /// <summary>The instance name of the map this collection reads from.</summary>
    internal static string? SourceTmxName(NamedObjectSave save) =>
        save.Properties.GetValue<string>("SourceTmxName");

    /// <summary>The tile type whose tiles become geometry, under <see cref="CollisionCreationOptions.FromType"/>.</summary>
    internal static string? CollisionTileTypeName(NamedObjectSave save) =>
        save.Properties.GetValue<string>("CollisionTileTypeName");

    /// <summary>The tile property whose tiles become geometry, under <see cref="CollisionCreationOptions.FromProperties"/>.</summary>
    internal static string? CollisionPropertyName(NamedObjectSave save) =>
        save.Properties.GetValue<string>("CollisionPropertyName");

    /// <summary>The map layer to restrict geometry to, when one is named.</summary>
    internal static string? CollisionLayerName(NamedObjectSave save) =>
        save.Properties.GetValue<string>("CollisionLayerName");

    /// <summary>Grid size. Absent means 16, never 0.</summary>
    internal static float CollisionTileSize(NamedObjectSave save) =>
        Or(save, "CollisionTileSize", 16f);

    /// <summary>Fill width in tiles. Absent means 32.</summary>
    internal static int CollisionFillWidth(NamedObjectSave save) =>
        Or(save, "CollisionFillWidth", 32);

    /// <summary>Fill height in tiles. Absent means 1.</summary>
    internal static int CollisionFillHeight(NamedObjectSave save) =>
        Or(save, "CollisionFillHeight", 1);

    /// <summary>Left edge of a filled region.</summary>
    internal static float CollisionFillLeft(NamedObjectSave save) =>
        Or(save, "CollisionFillLeft", 0f);

    /// <summary>Top edge of a filled region.</summary>
    internal static float CollisionFillTop(NamedObjectSave save) =>
        Or(save, "CollisionFillTop", 0f);

    private static float Or(NamedObjectSave save, string name, float fallback) =>
        save.Properties.ContainsValue(name) ? save.Properties.GetValue<float>(name) : fallback;

    private static int Or(NamedObjectSave save, string name, int fallback) =>
        save.Properties.ContainsValue(name) ? save.Properties.GetValue<int>(name) : fallback;
}
