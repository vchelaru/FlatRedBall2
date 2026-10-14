using System;
using System.Collections.Generic;
using System.Linq;
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
    /// <para>Every top-level list is created empty before anything is built, so a nested entity that
    /// joins a factory-associated list finds it whatever order the element declares things in.
    /// <c>onEntityList</c> is told about each entity list at that point, which is how a screen
    /// registers its factory-associated ones.</para>
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
        Screen? owningScreen = null,
        Action<NamedObjectSave, GlueEntityList>? onEntityList = null)
    {
        var builder = new GlueObjectBuilder(diagnostics, content, project, owningScreen);

        foreach (var save in namedObjects)
        {
            if (!save.IsList || string.IsNullOrEmpty(save.InstanceName))
                continue;

            var list = NewList(save);
            objects[save.InstanceName] = list;

            if (list is GlueEntityList entityList)
                onEntityList?.Invoke(save, entityList);
        }

        BuildInto(namedObjects, builder, elementName, objects, diagnostics, addSingle);

        // Tile objects come last and in their own order: a map is loaded from a file, and a
        // collection is derived from a map, so neither fits the construct-and-configure pass.
        GlueTileBuilder.Build(
            namedObjects, elementName, objects, diagnostics, content, register, builder);

        // The camera follows a list and bounds itself to a map, so it is finished once both exist.
        GlueCameraBuilder.Build(namedObjects, elementName, objects, diagnostics);

        // Relationships reference other objects by name, so they come last. FRB1 codifies the same
        // ordering rather than relying on declaration order.
        GlueCollisionBuilder.Build(namedObjects, elementName, objects, diagnostics, owningScreen);
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
                ? BuildList(builder, save, elementName, objects, diagnostics)
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
    /// Puts the objects of an element that is being reused back to their authored state: shapes,
    /// sprites and Gum visuals get their instructions again, and an entity list that lost its
    /// authored members when the previous life ended gets them back.
    /// </summary>
    /// <remarks>
    /// Nothing is rebuilt, so references game code took to the built objects stay valid. That is the
    /// point: a registered pooled entity runs <c>CustomInitialize</c> once, so what it captured there
    /// has to outlive a reuse.
    /// </remarks>
    internal static void Reapply(
        List<NamedObjectSave> namedObjects,
        string? elementName,
        Dictionary<string, object> objects,
        GlueObjectBuilder builder,
        List<GlueLoadDiagnostic> diagnostics)
    {
        foreach (var save in namedObjects)
        {
            if (string.IsNullOrEmpty(save.InstanceName)
                || GlueTileBuilder.IsTileObject(save)
                || GlueCollisionBuilder.IsRelationship(save)
                || !objects.TryGetValue(save.InstanceName, out object? built))
            {
                continue;
            }

            if (save.IsList)
            {
                if (built is GlueEntityList)
                    BuildList(builder, save, elementName, objects, diagnostics);

                continue;
            }

            builder.Reapply(built, save, elementName);

            if (save.ContainedObjects.Count > 0)
                Reapply(save.ContainedObjects, elementName, objects, builder, diagnostics);
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
            else if (item is GlueEntityList entities)
            {
                foreach (var member in entities)
                    yield return member;
            }
            else
            {
                yield return item;
            }
        }
    }

    /// <summary>
    /// Whether a list holds entities: its generic type names an element, or — when it names none —
    /// every member it declares is one.
    /// </summary>
    private static bool IsEntityList(NamedObjectSave save)
    {
        if (!string.IsNullOrEmpty(save.SourceClassGenericType))
            return GlueTypeName.Parse(save.SourceClassGenericType).IsElementReference;

        return save.ContainedObjects.Count > 0 &&
               save.ContainedObjects.All(c => GlueTypeName.Parse(c.SourceClassType).IsElementReference);
    }

    /// <summary>
    /// An entity list is a live <see cref="GlueEntityList"/>: the owner's own collection, which
    /// spawns can join and destroyed members leave. Any other list is a plain
    /// snapshot of whatever could be constructed — FRB2 has no equivalent of Glue's
    /// <c>PositionedObjectList&lt;T&gt;</c>.
    /// </summary>
    private static object NewList(NamedObjectSave save) =>
        IsEntityList(save) ? new GlueEntityList() : new List<object>();

    /// <summary>
    /// Fills a list with its authored members. The list is the one the owner already holds in
    /// <paramref name="objects"/> (created up front for a top-level list).
    /// </summary>
    /// <remarks>
    /// A nested entity member is registered on its owning screen as a side effect of being created
    /// (see <see cref="GlueObjectBuilder.Create"/>) and joins only this list: a declared member's
    /// placement is unambiguous, so it never feeds the type's spawn destinations (<see cref="GlueProject.ListsToAddTo"/>).
    /// </remarks>
    private static object BuildList(
        GlueObjectBuilder builder,
        NamedObjectSave save,
        string? elementName,
        Dictionary<string, object> objects,
        List<GlueLoadDiagnostic> diagnostics)
    {
        if (!objects.TryGetValue(save.InstanceName!, out object? list))
            list = NewList(save);

        foreach (var contained in save.ContainedObjects)
        {
            object? item = builder.Create(contained, elementName, isListMember: true);

            if (item is null)
                continue;

            switch (list)
            {
                case GlueEntityList entities when item is GlueEntity entity:
                    entities.Add(entity);
                    break;
                case GlueEntityList:
                    diagnostics.Add(new GlueLoadDiagnostic(
                        GlueDiagnosticSeverity.Warning,
                        $"'{contained.InstanceName}' is not an entity but sits in the entity list " +
                        $"'{save.InstanceName}'; it was skipped.",
                        elementName));
                    break;
                case List<object> others:
                    others.Add(item);
                    break;
            }
        }

        return list;
    }

    /// <summary>The live entity list the owner declared under this name.</summary>
    /// <exception cref="ArgumentException">The owner declares no entity list by that name.</exception>
    internal static GlueEntityList FindEntityList(
        IReadOnlyDictionary<string, object> objects, string listName, string? ownerName)
    {
        if (objects.TryGetValue(listName, out object? found) && found is GlueEntityList list)
            return list;

        string known = string.Join(
            ", ", objects.Where(o => o.Value is GlueEntityList).Select(o => $"'{o.Key}'"));

        throw new ArgumentException(
            $"'{ownerName}' declares no entity list named '{listName}'. " +
            (known.Length == 0 ? "It declares none." : $"Its entity lists are {known}."));
    }
}
