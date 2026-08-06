using System;
using System.Collections.Generic;
using FlatRedBall2.Glue.Model;

namespace FlatRedBall2.Glue;

/// <summary>
/// Builds the whole set of objects an element declares. Shared by <see cref="GlueScreen"/> and
/// <see cref="GlueEntity"/>, which differ only in how a single object gets registered.
/// </summary>
internal static class GlueElementBuilder
{
    /// <remarks>
    /// <c>addSingle</c> is how one object gets registered on its owner — a screen adds renderables,
    /// an entity attaches children. Lists have no owner to register with, so their members are built
    /// but not added.
    /// </remarks>
    internal static void Build(
        List<NamedObjectSave> namedObjects,
        string? elementName,
        Dictionary<string, object> objects,
        List<GlueLoadDiagnostic> diagnostics,
        Func<GlueObjectBuilder, NamedObjectSave, object?> addSingle,
        GlueContentSource? content,
        Action<object> register,
        GlueProject? project = null,
        Screen? owningScreen = null)
    {
        var builder = new GlueObjectBuilder(diagnostics, content, project, owningScreen);
        BuildInto(namedObjects, builder, elementName, objects, diagnostics, addSingle);

        // Tile objects come last and in their own order: a map is loaded from a file, and a
        // collection is derived from a map, so neither fits the construct-and-configure pass.
        GlueTileBuilder.Build(namedObjects, elementName, objects, diagnostics, content, register);

        // The camera follows a list and bounds itself to a map, so it is finished once both exist.
        GlueCameraBuilder.Build(namedObjects, elementName, objects, diagnostics);

        // Relationships reference other objects by name, so they come last. FRB1 codifies the same
        // ordering rather than relying on declaration order.
        GlueCollisionBuilder.Build(
            namedObjects, elementName, objects, diagnostics, project, owningScreen);
    }

    /// <remarks>
    /// Recurses into <c>ContainedObjects</c> for <em>every</em> object, not only lists. Glue nests
    /// freely — Beefball's arena walls are children of a <c>ShapeCollection</c>, a type FRB2 has no
    /// equivalent for — and an unbuildable container must not take its buildable children with it.
    /// FRB1's generator recurses unconditionally for the same reason.
    /// </remarks>
    private static void BuildInto(
        List<NamedObjectSave> namedObjects,
        GlueObjectBuilder builder,
        string? elementName,
        Dictionary<string, object> objects,
        List<GlueLoadDiagnostic> diagnostics,
        Func<GlueObjectBuilder, NamedObjectSave, object?> addSingle)
    {
        foreach (var save in namedObjects)
        {
            if (string.IsNullOrEmpty(save.InstanceName)
                || GlueTileBuilder.IsTileObject(save)
                || GlueCollisionBuilder.IsRelationship(save))
            {
                continue;
            }

            object? built = save.IsList
                ? BuildList(builder, save, elementName, diagnostics)
                : addSingle(builder, save);

            if (built is not null)
                objects[save.InstanceName] = built;

            // A list already consumed its children as members; anything else may still contain
            // objects that belong to the same owner.
            if (!save.IsList && save.ContainedObjects.Count > 0)
                BuildInto(save.ContainedObjects, builder, elementName, objects, diagnostics, addSingle);
        }
    }

    /// <summary>
    /// Walks built objects, replacing each list with its members, so callers can treat lists and
    /// single objects uniformly when unregistering.
    /// </summary>
    internal static IEnumerable<object> Flatten(IEnumerable<object> built)
    {
        foreach (var item in built)
        {
            if (item is List<object> list)
            {
                foreach (var member in list)
                    yield return member;
            }
            else
            {
                yield return item;
            }
        }
    }

    /// <summary>
    /// Builds a list member's contents. The list itself is a plain <see cref="List{T}"/> of whatever
    /// could be constructed — FRB2 has no equivalent of Glue's <c>PositionedObjectList&lt;T&gt;</c>,
    /// and this phase does not need one.
    /// </summary>
    /// <remarks>
    /// Members are built but not registered anywhere: a list in Glue is usually a spawn pool whose
    /// contents an entity factory owns, which is Phase 8. Anything unbuildable — most commonly a
    /// nested entity, which needs Phase 6 — is reported and skipped.
    /// </remarks>
    private static object BuildList(
        GlueObjectBuilder builder,
        NamedObjectSave save,
        string? elementName,
        List<GlueLoadDiagnostic> diagnostics)
    {
        var items = new List<object>();

        foreach (var contained in save.ContainedObjects)
        {
            object? item = builder.Create(contained, elementName);
            if (item is not null)
                items.Add(item);
        }

        return items;
    }
}
