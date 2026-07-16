using System.Collections.Generic;

namespace FlatRedBall2.Tiled;

/// <summary>
/// A read-only snapshot of one object on a Tiled object layer, returned by
/// <see cref="TileMap.GetObjectLayerData"/> for game code that wants raw Tiled object data
/// without spawning an entity (<see cref="TileMap.CreateEntities{T}"/>) or generating collision
/// (<see cref="TileMap.GenerateCollisionFromClass"/>).
/// </summary>
/// <param name="X">Left edge, in world space (already includes the owning <see cref="TileMap"/>'s position).</param>
/// <param name="Y">
/// Top edge, in world space — matches <see cref="TileMap.Y"/>'s convention where a larger value
/// is higher up. Note this holds regardless of the source object's Tiled anchor: rectangle
/// objects anchor at their top-left in Tiled, tile-insert objects anchor at their bottom-left —
/// both are normalized to a top-left world corner here.
/// </param>
/// <param name="Width">Width in world units. Zero for object types with no size (e.g. point objects).</param>
/// <param name="Height">Height in world units. Zero for object types with no size.</param>
/// <param name="Class">The object's Tiled "Class" value, or an empty string if unset.</param>
/// <param name="GlobalId">
/// The tile's global ID for tile-insert objects (placed with Tiled's "Insert Tile" tool);
/// zero for objects not tied to a tile.
/// </param>
/// <param name="Properties">
/// Custom properties as strings, keyed by name. For tile-insert objects this merges the tile's
/// class-level properties (defined on the tile's type in the tileset) with instance-level
/// properties, instance values winning on collision — the same merge
/// <see cref="TileMap.CreateEntities{T}"/> performs. Empty if the object has no properties.
/// </param>
public readonly record struct ObjectLayerEntry(
    float X,
    float Y,
    float Width,
    float Height,
    string Class,
    int GlobalId,
    IReadOnlyDictionary<string, string> Properties);
