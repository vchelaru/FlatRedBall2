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
    internal static bool IsTileObject(NamedObjectSave save) => IsMap(save) || IsCollection(save);

    private static bool IsMap(NamedObjectSave save) =>
        GlueTypeName.Parse(save.SourceClassType).OpenTypeName
            is "FlatRedBall.TileGraphics.LayeredTileMap" or "LayeredTileMap";

    private static bool IsCollection(NamedObjectSave save) =>
        GlueTypeName.Parse(save.SourceClassType).OpenTypeName
            is "FlatRedBall.TileCollisions.TileShapeCollection" or "TileShapeCollection";

    /// <summary>
    /// Builds every tile object in <paramref name="namedObjects"/>, in dependency order, adding what
    /// it creates to <paramref name="objects"/>.
    /// </summary>
    internal static void Build(
        List<NamedObjectSave> namedObjects,
        string? elementName,
        Dictionary<string, object> objects,
        List<GlueLoadDiagnostic> diagnostics,
        GlueContentSource? content,
        Action<object> register)
    {
        foreach (var save in namedObjects)
        {
            if (!IsMap(save) || string.IsNullOrEmpty(save.InstanceName))
                continue;

            var map = BuildMap(save, elementName, diagnostics, content);

            if (map is not null)
            {
                objects[save.InstanceName] = map;
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
                register(shapes);
            }
        }
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
