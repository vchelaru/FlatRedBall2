using System;
using System.Collections.Generic;
using System.Linq;
using FlatRedBall2.Collision;
using FlatRedBall2.Glue.Model;

namespace FlatRedBall2.Glue;

/// <summary>
/// Turns Glue's collision relationships into FRB2 ones.
/// </summary>
/// <remarks>
/// A relationship has no save class — it is a <see cref="NamedObjectSave"/> recognised by its type
/// string, with every setting in its property bag.
/// <para><b>The variant is derived, never read from the type string.</b> Glue recomputes
/// <c>SourceClassType</c> on load, so a project last saved by an older Glue carries a stale value —
/// LadderDemo has one whose <c>SourceFile</c> names a different relationship class than its
/// <c>SourceClassType</c>. What the relationship *is* comes from the two instance names it points at
/// and its <c>CollisionType</c>.</para>
/// </remarks>
internal static class GlueCollisionBuilder
{
    // Ported verbatim from NamedObjectSaveExtensionMethods.IsCollisionRelationship. The trailing
    // '<' on the Delegate entries is load-bearing: without it a longer type name that merely starts
    // the same way would match.
    private static readonly string[] Prefixes =
    {
        "FlatRedBall.Math.Collision.CollisionRelationship",
        "FlatRedBall.Math.Collision.PositionedObjectVsPositionedObjectRelationship",
        "FlatRedBall.Math.Collision.PositionedObjectVsListRelationship",
        "FlatRedBall.Math.Collision.ListVsPositionedObjectRelationship",
        "FlatRedBall.Math.Collision.AlwaysCollidingListCollisionRelationship",
        "FlatRedBall.Math.Collision.PositionedObjectVsShapeCollection",
        "FlatRedBall.Math.Collision.ListVsShapeCollectionRelationship",
        "FlatRedBall.Math.Collision.ListVsListRelationship",
        "FlatRedBall.Math.Collision.CollidableListVsTileShapeCollectionRelationship",
        "FlatRedBall.Math.Collision.CollidableVsTileShapeCollectionRelationship",
        "FlatRedBall.Math.Collision.DelegateCollisionRelationship<",
        "FlatRedBall.Math.Collision.DelegateCollisionRelationshipBase<",
        "FlatRedBall.Math.Collision.DelegateListVsSingleRelationship<",
        "FlatRedBall.Math.Collision.DelegateSingleVsListRelationship<",
        "FlatRedBall.Math.Collision.DelegateListVsListRelationship<",
        "CollisionRelationship<",
    };

    /// <summary>Whether this type string names a collision relationship.</summary>
    internal static bool IsRelationship(string? sourceClassType)
    {
        if (string.IsNullOrEmpty(sourceClassType))
            return false;

        if (sourceClassType == "CollisionRelationship")
            return true;

        return Prefixes.Any(p => sourceClassType.StartsWith(p, StringComparison.Ordinal));
    }

    /// <summary>Whether this object is a relationship, by its type string.</summary>
    internal static bool IsRelationship(NamedObjectSave save) => IsRelationship(save.SourceClassType);

    /// <summary>
    /// Builds every relationship in <paramref name="namedObjects"/> against the objects already
    /// built.
    /// </summary>
    /// <remarks>
    /// Runs after everything else, because a relationship references objects by name. FRB1 codifies
    /// the same ordering rather than relying on declaration order, and so does this.
    /// </remarks>
    internal static void Build(
        List<NamedObjectSave> namedObjects,
        string? elementName,
        Dictionary<string, object> objects,
        List<GlueLoadDiagnostic> diagnostics,
        GlueProject? project,
        Screen? screen)
    {
        foreach (var save in namedObjects)
        {
            if (!IsRelationship(save) || string.IsNullOrEmpty(save.InstanceName) || save.IsDisabled)
                continue;

            var built = BuildOne(save, elementName, objects, diagnostics, project, screen);

            if (built is not null)
                objects[save.InstanceName] = built;
        }
    }

    private static object? BuildOne(
        NamedObjectSave save,
        string? elementName,
        Dictionary<string, object> objects,
        List<GlueLoadDiagnostic> diagnostics,
        GlueProject? project,
        Screen? screen)
    {
        var settings = GlueCollisionSettings.From(save);

        // Glue's own editor-side validation, ported so a malformed relationship reports rather than
        // failing to compile as it would in FRB1.
        if (string.IsNullOrEmpty(settings.FirstCollisionName))
        {
            Warn(diagnostics, elementName,
                $"'{save.InstanceName}' names no first collidable; no relationship was created.");
            return null;
        }

        if (screen is null)
        {
            Warn(diagnostics, elementName,
                $"'{save.InstanceName}' needs a running screen to register against; it was skipped.");
            return null;
        }

        if (!settings.IsActive)
        {
            // FRB2 has no per-relationship enable flag, so "inactive" can only mean "never
            // registered" — which game code cannot undo the way FRB1's IsActive can.
            Warn(diagnostics, elementName,
                $"'{save.InstanceName}' is authored inactive. FRB2 has no per-relationship enable " +
                "flag, so it was not registered and cannot be switched on later.");
            return null;
        }

        var first = ResolveSide(settings.FirstCollisionName!, objects, project);

        if (first is null)
        {
            Warn(diagnostics, elementName,
                $"'{save.InstanceName}' references '{settings.FirstCollisionName}', which this " +
                "build did not create; no relationship was created.");
            return null;
        }

        // No second side is Glue's "always colliding" variant — a per-frame event with one argument,
        // which FRB2's inherently two-sided relationship cannot express.
        if (settings.SecondCollisionName is null)
        {
            Warn(diagnostics, elementName,
                $"'{save.InstanceName}' is an always-colliding relationship, which has no FRB2 " +
                "equivalent; it was skipped.");
            return null;
        }

        var second = ResolveSide(settings.SecondCollisionName, objects, project);

        if (second is null)
        {
            Warn(diagnostics, elementName,
                $"'{save.InstanceName}' references '{settings.SecondCollisionName}', which this " +
                "build did not create; no relationship was created.");
            return null;
        }

        return Register(save, settings, first, second, elementName, diagnostics, screen);
    }

    /// <summary>
    /// Resolves one side to something collidable: an entity list, a single entity, or tile shapes.
    /// </summary>
    private static object? ResolveSide(
        string name, Dictionary<string, object> objects, GlueProject? project)
    {
        if (!objects.TryGetValue(name, out object? side))
            return null;

        // A Glue list holds whatever could be built; a relationship needs the entities in it.
        if (side is List<object> list)
        {
            var entities = list.OfType<GlueEntity>().ToList();
            return entities.Count == list.Count ? entities : null;
        }

        return side;
    }

    private static object? Register(
        NamedObjectSave save,
        GlueCollisionSettings settings,
        object first,
        object second,
        string? elementName,
        List<GlueLoadDiagnostic> diagnostics,
        Screen screen)
    {
        CollisionRelationship<GlueEntity, GlueEntity>? entityPair = null;
        CollisionRelationship<GlueEntity, TileShapes>? tilePair = null;

        switch (first, second)
        {
            case (List<GlueEntity> a, List<GlueEntity> b):
                entityPair = ReferenceEquals(a, b) || (a.Count > 0 && a.SequenceEqual(b))
                    ? screen.AddSelfCollisionRelationship(a)
                    : screen.AddCollisionRelationship(a, b);
                break;

            case (List<GlueEntity> a, GlueEntity single):
                entityPair = screen.AddCollisionRelationship(a, new List<GlueEntity> { single });
                break;

            case (GlueEntity single, List<GlueEntity> b):
                entityPair = screen.AddCollisionRelationship(new List<GlueEntity> { single }, b);
                break;

            case (List<GlueEntity> a, TileShapes tiles):
                tilePair = screen.AddCollisionRelationship(a, tiles);
                break;

            case (GlueEntity single, TileShapes tiles):
                tilePair = screen.AddCollisionRelationship(new List<GlueEntity> { single }, tiles);
                break;

            default:
                Warn(diagnostics, elementName,
                    $"'{save.InstanceName}' collides a {Describe(first)} against a {Describe(second)}, " +
                    "which this build cannot express; it was skipped.");
                return null;
        }

        if (entityPair is not null)
        {
            ApplyResponse(entityPair, save, settings, elementName, diagnostics);
            ApplySubCollisions(entityPair, settings);
            return entityPair;
        }

        ApplyResponse(tilePair!, save, settings, elementName, diagnostics);
        return tilePair;
    }

    private static void ApplyResponse<TA, TB>(
        CollisionRelationship<TA, TB> relationship,
        NamedObjectSave save,
        GlueCollisionSettings settings,
        string? elementName,
        List<GlueLoadDiagnostic> diagnostics)
        where TA : ICollidable
        where TB : ICollidable
    {
        // Unchecked in Glue means the response is the game's to apply, per collision, from the
        // event. The type below is still configured — it is withheld, not discarded.
        relationship.ArePhysicsAppliedAutomatically = settings.ArePhysicsAppliedAutomatically;

        switch (settings.CollisionType)
        {
            case GlueCollisionType.NoPhysics:
                // Event-only. Nothing to configure — and defaulting to a response here would shove
                // apart every trigger in every project.
                break;

            case GlueCollisionType.MoveCollision:
                relationship.MoveBothOnCollision(settings.FirstMass, settings.SecondMass);
                break;

            case GlueCollisionType.BounceCollision:
                relationship.BounceOnCollision(
                    settings.FirstMass, settings.SecondMass, settings.Elasticity);
                break;

            case GlueCollisionType.PlatformerSolidCollision:
                relationship.SlopeMode = SlopeCollisionMode.PlatformerFloor;
                relationship.BounceFirstOnCollision(0f);
                break;

            case GlueCollisionType.PlatformerCloudCollision:
                relationship.SlopeMode = SlopeCollisionMode.PlatformerFloor;
                relationship.OneWayDirection = OneWayDirection.Up;
                relationship.CanDropThrough = true;
                relationship.BounceFirstOnCollision(0f);
                break;

            default:
                Warn(diagnostics, elementName,
                    $"'{save.InstanceName}' uses collision type '{settings.CollisionType}', which " +
                    "has no FRB2 equivalent; it collides but applies no physics.");
                break;
        }
    }

    /// <summary>
    /// Points a side at one shape inside the entity rather than the whole entity.
    /// </summary>
    /// <remarks>
    /// Glue names the shape; FRB2 wants a selector. A loaded entity keeps its shapes in
    /// <c>Objects</c> rather than as properties, so the selector looks the name up there.
    /// </remarks>
    private static void ApplySubCollisions(
        CollisionRelationship<GlueEntity, GlueEntity> relationship, GlueCollisionSettings settings)
    {
        if (settings.FirstSubCollision is not null)
        {
            string name = settings.FirstSubCollision;
            relationship.WithFirstShape(e =>
                e.Objects.TryGetValue(name, out object? shape) && shape is ICollidable collidable
                    ? collidable
                    : e);
        }

        if (settings.SecondSubCollision is not null)
        {
            string name = settings.SecondSubCollision;
            relationship.WithSecondShape(e =>
                e.Objects.TryGetValue(name, out object? shape) && shape is ICollidable collidable
                    ? collidable
                    : e);
        }
    }

    private static string Describe(object side) => side switch
    {
        List<GlueEntity> => "list of entities",
        GlueEntity => "single entity",
        TileShapes => "tile collection",
        _ => side.GetType().Name,
    };

    private static void Warn(List<GlueLoadDiagnostic> diagnostics, string? elementName, string message) =>
        diagnostics.Add(new GlueLoadDiagnostic(GlueDiagnosticSeverity.Warning, message, elementName));
}
