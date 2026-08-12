using System;
using System.Collections.Generic;
using FlatRedBall2.Collision;
using FlatRedBall2.Glue.Model;
using FlatRedBall2.Tiled;

namespace FlatRedBall2.Glue;

/// <summary>
/// Builds the tile objects in an element: the map itself, and the collections whose geometry is
/// derived from it.
/// </summary>
/// <remarks>
/// Separate from <see cref="GlueObjectBuilder"/> because these are not constructed and configured —
/// a map comes from a file and a collection is derived from a map, so both need context a
/// per-object builder does not have.
/// <para>Ordering is explicit rather than inherited from the file: maps first, then the collections
/// that read them. Every real project happens to declare them in that order, which is exactly the
/// kind of accident worth not depending on.</para>
/// </remarks>
internal static class GlueTileBuilder
{
    /// <summary>Whether this object is one of the tile types built here rather than constructed.</summary>
    internal static bool IsTileObject(NamedObjectSave save) =>
        IsMap(save) || IsCollection(save) || IsNodeNetwork(save);

    private static bool IsMap(NamedObjectSave save) =>
        GlueTypeName.Parse(save.SourceClassType).OpenTypeName
            is "FlatRedBall.TileGraphics.LayeredTileMap" or "LayeredTileMap"
        || IsTmxFileObject(save);

    /// <summary>
    /// An object added from a <c>.tmx</c>, which Glue writes with no <c>SourceClassType</c> at all.
    /// </summary>
    /// <remarks>
    /// FRB1 takes the type from the file's own <c>AssetTypeInfo</c>, so the class type is redundant
    /// there and Glue omits it; recognising a map by class type alone skipped the object, and
    /// anything keyed to its instance name — a <c>TileShapeCollection</c>'s <c>SourceTmxName</c> —
    /// then read from nothing.
    /// <para>
    /// Such an object is an alias for the loaded file rather than a second map: <c>BuildMap</c>
    /// resolves through the content source's per-path cache, so the alias and the referenced file
    /// are one instance.
    /// </para>
    /// </remarks>
    private static bool IsTmxFileObject(NamedObjectSave save) =>
        save.SourceType == SourceType.File
        && save.SourceFile is string file
        && file.EndsWith(".tmx", StringComparison.OrdinalIgnoreCase);

    private static bool IsCollection(NamedObjectSave save) =>
        GlueTypeName.Parse(save.SourceClassType).OpenTypeName
            is "FlatRedBall.TileCollisions.TileShapeCollection" or "TileShapeCollection";

    internal static bool IsNodeNetwork(NamedObjectSave save) =>
        GlueTypeName.Parse(save.SourceClassType).OpenTypeName
            is "FlatRedBall.AI.Pathfinding.TileNodeNetwork" or "TileNodeNetwork";

    /// <summary>
    /// Builds every tile object in <paramref name="namedObjects"/>, in dependency order, adding what
    /// it creates to <paramref name="objects"/>.
    /// </summary>
    /// <remarks>
    /// <paramref name="builder"/> applies each object's authored instructions. Tile objects are built
    /// here rather than in the construct-and-configure pass, and skipping that pass skipped its
    /// instruction step with it — so <c>Visible</c> on a collection, and every other authored value,
    /// was silently dropped.
    /// </remarks>
    internal static void Build(
        List<NamedObjectSave> namedObjects,
        string? elementName,
        Dictionary<string, object> objects,
        List<GlueLoadDiagnostic> diagnostics,
        GlueContentSource? content,
        Action<object> register,
        GlueObjectBuilder? builder = null)
    {
        foreach (var save in namedObjects)
        {
            if (!IsMap(save) || string.IsNullOrEmpty(save.InstanceName))
                continue;

            var map = BuildMap(save, elementName, diagnostics, content);

            if (map is not null)
            {
                objects[save.InstanceName] = map;
                builder?.ApplyInstructions(map, save, elementName);
                register(map);
            }
        }

        foreach (var save in namedObjects)
        {
            if (!IsCollection(save) || string.IsNullOrEmpty(save.InstanceName))
                continue;

            var shapes = BuildCollection(save, elementName, objects, diagnostics);

            if (shapes is not null)
            {
                objects[save.InstanceName] = shapes;

                // Before register: Screen.Add copies the current tiles into the render list, and a
                // tile's own visibility comes from the collection's when it is added.
                builder?.ApplyInstructions(shapes, save, elementName);
                register(shapes);
            }
        }

        // Node networks last: like a collection, a network is derived from a map, so the map has to
        // exist first. Not registered for rendering — a network is navigation data, not a visual.
        foreach (var save in namedObjects)
        {
            if (!IsNodeNetwork(save) || string.IsNullOrEmpty(save.InstanceName))
                continue;

            var network = BuildNodeNetwork(save, elementName, objects, diagnostics);

            if (network is not null)
            {
                objects[save.InstanceName] = network;
                builder?.ApplyInstructions(network, save, elementName);
            }
        }
    }

    /// <summary>
    /// Spawns an entity for every tile whose type names one, the way FRB1's tile instantiator does.
    /// </summary>
    /// <remarks>
    /// Glue matches a tile's type against an entity's name, so a tile typed <c>Door</c> spawns
    /// <c>Entities\Door</c>. Tiles are located with the same by-class query collision uses, which is
    /// what keeps the two consistent.
    /// <para>Spawning goes through <see cref="GlueProject.CreateEntity(EntitySave, Screen)"/> rather than
    /// <see cref="TileMap.CreateEntities{T}"/>: the latter needs a <c>Factory&lt;T&gt;</c>, and every
    /// loaded entity is a <see cref="GlueEntity"/>, so one factory could not tell a Door from a
    /// Player (Phase 8 G80).</para>
    /// </remarks>
    /// <returns>Every entity spawned, across all types.</returns>
    internal static List<GlueEntity> CreateEntitiesFromTiles(
        TileMap map,
        GlueProject project,
        Screen screen,
        List<GlueLoadDiagnostic> diagnostics,
        string? elementName = null)
    {
        var spawned = new List<GlueEntity>();

        foreach (var entity in project.Result.Project.Entities)
        {
            if (entity.Name is null || entity.IsAbstract)
                continue;

            // Tiles carry the leaf name; the project keys on the full Entities\Name form.
            string leafName = entity.Name.Substring(entity.Name.LastIndexOf('\\') + 1);
            TileShapes tiles;

            try
            {
                tiles = map.GenerateCollisionFromClass(leafName);
            }
            catch (Exception e)
            {
                Warn(diagnostics, elementName,
                    $"Looking for '{leafName}' tiles to spawn '{entity.Name}' failed: {e.Message}");
                continue;
            }

            int columns = (int)System.Math.Ceiling(map.Width / System.Math.Max(map.TileWidth, 1));
            int rows = (int)System.Math.Ceiling(map.Height / System.Math.Max(map.TileHeight, 1));

            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    if (tiles.GetTileAtCell(column, row) is not AARect tile)
                        continue;

                    var instance = project.CreateEntity(entity, screen);

                    // The tile's own centre, so the entity lands where it was painted.
                    instance.X = tile.X;
                    instance.Y = tile.Y;
                    spawned.Add(instance);
                }
            }
        }

        return spawned;
    }

    /// <summary>
    /// Builds a <see cref="AI.TileNodeNetwork"/> from the tiles of a map that match the authored
    /// creation option.
    /// </summary>
    /// <remarks>
    /// The network's grid is taken from the <see cref="TileShapes"/> the same query produces, so
    /// nodes land on the same centres the collision does. Deriving it from the map's own bounds
    /// instead would drift by half a tile wherever the map's origin is not the collection's.
    /// </remarks>
    private static AI.TileNodeNetwork? BuildNodeNetwork(
        NamedObjectSave save,
        string? elementName,
        Dictionary<string, object> objects,
        List<GlueLoadDiagnostic> diagnostics)
    {
        string? mapName = GlueTileDefaults.SourceTmxName(save);

        if (string.IsNullOrEmpty(mapName))
        {
            Warn(diagnostics, elementName,
                $"'{save.InstanceName}' names no source map; no node network was built.");
            return null;
        }

        if (!objects.TryGetValue(mapName, out object? candidate) || candidate is not TileMap map)
        {
            Warn(diagnostics, elementName,
                $"'{save.InstanceName}' reads from '{mapName}', which is not a loaded tile map; " +
                "no node network was built.");
            return null;
        }

        var options = GlueTileDefaults.NodeNetworkCreationOptions(save);
        string? layer = GlueTileDefaults.NodeNetworkLayerName(save);
        TileShapes? occupied;

        switch (options)
        {
            case TileNodeNetworkCreationOptions.FromType:
                string? type = GlueTileDefaults.NodeNetworkTileTypeName(save);

                if (string.IsNullOrEmpty(type))
                {
                    Warn(diagnostics, elementName,
                        $"'{save.InstanceName}' builds nodes from a tile type but names none.");
                    return null;
                }

                occupied = map.GenerateCollisionFromClass(type, NullIfEmpty(layer));
                break;

            case TileNodeNetworkCreationOptions.FromProperties:
                string? property = GlueTileDefaults.NodeNetworkPropertyName(save);

                if (string.IsNullOrEmpty(property))
                {
                    Warn(diagnostics, elementName,
                        $"'{save.InstanceName}' builds nodes from a tile property but names none.");
                    return null;
                }

                occupied = map.GenerateCollisionFromProperty(property, NullIfEmpty(layer));
                break;

            default:
                Warn(diagnostics, elementName,
                    $"'{save.InstanceName}' uses node network creation option '{options}', which " +
                    "this build does not support; no node network was built.");
                return null;
        }

        // System.Math, not FlatRedBall2.Math, which this namespace would otherwise resolve to.
        int columns = (int)System.Math.Ceiling(map.Width / System.Math.Max(map.TileWidth, 1));
        int rows = (int)System.Math.Ceiling(map.Height / System.Math.Max(map.TileHeight, 1));

        var network = new AI.TileNodeNetwork(
            occupied.X, occupied.Y, occupied.GridSize, columns, rows,
            GlueTileDefaults.NodeNetworkDirectionalType(save));

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                if (occupied.GetTileAtCell(column, row) is not null)
                    network.AddAndLinkNode(column, row);
            }
        }

        if (GlueTileDefaults.EliminateCutCorners(save))
            network.EliminateCutCorners();

        return network;
    }

    private static TileMap? BuildMap(
        NamedObjectSave save,
        string? elementName,
        List<GlueLoadDiagnostic> diagnostics,
        GlueContentSource? content)
    {
        // The map is file-sourced, so its path is on the object rather than in a referenced file.
        if (string.IsNullOrEmpty(save.SourceFile) || !save.SourceFile.EndsWith(".tmx", StringComparison.OrdinalIgnoreCase))
        {
            // The abstract base declares the slot without a file and expects a derived element to
            // supply one. Nothing to build, and nothing wrong.
            if (!save.SetByDerived)
            {
                Warn(diagnostics, elementName,
                    $"'{save.InstanceName}' is a tile map with no .tmx file; nothing was built.");
            }

            return null;
        }

        if (content is null)
        {
            Warn(diagnostics, elementName,
                $"'{save.InstanceName}' needs a content source to load '{save.SourceFile}'.");
            return null;
        }

        return content.LoadTileMap(save.SourceFile, elementName, diagnostics);
    }

    private static TileShapes? BuildCollection(
        NamedObjectSave save,
        string? elementName,
        Dictionary<string, object> objects,
        List<GlueLoadDiagnostic> diagnostics)
    {
        string? mapName = GlueTileDefaults.SourceTmxName(save);

        if (string.IsNullOrEmpty(mapName))
        {
            Warn(diagnostics, elementName,
                $"'{save.InstanceName}' names no source map; no collision was built.");
            return null;
        }

        if (!objects.TryGetValue(mapName, out object? candidate) || candidate is not TileMap map)
        {
            Warn(diagnostics, elementName,
                $"'{save.InstanceName}' reads from '{mapName}', which is not a loaded tile map; " +
                "no collision was built.");
            return null;
        }

        var options = GlueTileDefaults.CreationOptions(save);
        string? layer = GlueTileDefaults.CollisionLayerName(save);
        TileShapes? shapes;

        switch (options)
        {
            case CollisionCreationOptions.FromType:
                string? type = GlueTileDefaults.CollisionTileTypeName(save);

                if (string.IsNullOrEmpty(type))
                {
                    Warn(diagnostics, elementName,
                        $"'{save.InstanceName}' builds collision from a tile type but names none.");
                    return null;
                }

                shapes = map.GenerateCollisionFromClass(type, NullIfEmpty(layer));
                break;

            case CollisionCreationOptions.FromProperties:
                string? property = GlueTileDefaults.CollisionPropertyName(save);

                if (string.IsNullOrEmpty(property))
                {
                    Warn(diagnostics, elementName,
                        $"'{save.InstanceName}' builds collision from a tile property but names none.");
                    return null;
                }

                shapes = map.GenerateCollisionFromProperty(property, NullIfEmpty(layer));
                break;

            default:
                Warn(diagnostics, elementName,
                    $"'{save.InstanceName}' uses collision creation option '{options}', which this " +
                    "build does not support; no collision was built.");
                return null;
        }

        // GenerateCollisionFromProperty leaves the name unset, unlike its by-class sibling. Set it
        // either way so a collection is findable by the name its author gave it.
        shapes.Name = save.InstanceName;
        return shapes;
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    private static void Warn(List<GlueLoadDiagnostic> diagnostics, string? elementName, string message) =>
        diagnostics.Add(new GlueLoadDiagnostic(GlueDiagnosticSeverity.Warning, message, elementName));
}
