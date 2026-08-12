using FlatRedBall2.Glue.Model;

namespace FlatRedBall2.Glue;

/// <summary>
/// The collision response Glue authored.
/// </summary>
/// <remarks>
/// These are the ordinals Glue's <em>editor plugin</em> persists, which are **not** FRB1's runtime
/// <c>CollisionType</c>. The two agree on <see cref="BounceCollision"/> by coincidence and disagree
/// from there — decoding with the runtime one misreads real projects.
/// </remarks>
public enum GlueCollisionType
{
    /// <summary>Report the overlap; move nothing. The default when the key is absent.</summary>
    NoPhysics = 0,

    /// <summary>Separate the two along the collision normal.</summary>
    MoveCollision = 1,

    /// <summary>Separate and reflect velocity.</summary>
    BounceCollision = 2,

    /// <summary>Solid ground for a platformer character.</summary>
    PlatformerSolidCollision = 3,

    /// <summary>A jump-through platform.</summary>
    PlatformerCloudCollision = 4,

    /// <summary>The response is user C#, which a data-driven load has none of.</summary>
    DelegateCollision = 5,

    /// <summary>Stacking physics.</summary>
    StackingCollision = 6,

    /// <summary>Soft/spring separation.</summary>
    MoveSoftCollision = 7,
}

/// <summary>
/// A collision relationship's authored settings, read from its property bag.
/// </summary>
/// <remarks>
/// Every default here is Glue's own rather than <c>default(T)</c>, and three of them invert the
/// obvious reading: an absent <c>CollisionType</c> means event-only, absent masses and elasticity
/// mean <c>1</c> rather than <c>0</c>, and an absent active flag means active. Getting any of them
/// backwards is silent and severe — zero masses turn "bounce off the wall" into "pass through it",
/// and a false active flag disables every relationship in every project that omits the key.
/// </remarks>
public sealed class GlueCollisionSettings
{
    private GlueCollisionSettings()
    {
    }

    /// <summary>The instance name of the first collidable, as authored.</summary>
    public string? FirstCollisionName { get; private init; }

    /// <summary>The second collidable's instance name; null means "always colliding".</summary>
    public string? SecondCollisionName { get; private init; }

    /// <summary>The response to apply.</summary>
    public GlueCollisionType CollisionType { get; private init; }

    /// <summary>First side's mass. Zero means that side takes the full separation.</summary>
    public float FirstMass { get; private init; }

    /// <summary>Second side's mass.</summary>
    public float SecondMass { get; private init; }

    /// <summary>Bounce elasticity.</summary>
    public float Elasticity { get; private init; }

    /// <summary>Whether the relationship runs at all.</summary>
    public bool IsActive { get; private init; }

    /// <summary>
    /// Glue's "automatically apply physics on collision". Absent means true — Glue omits the
    /// property until it is unchecked.
    /// </summary>
    public bool ArePhysicsAppliedAutomatically { get; private init; }

    /// <summary>A shape inside the first entity to collide with, rather than the whole entity.</summary>
    public string? FirstSubCollision { get; private init; }

    /// <summary>A shape inside the second entity to collide with.</summary>
    public string? SecondSubCollision { get; private init; }

    /// <summary>Reads the settings off a relationship's property bag.</summary>
    public static GlueCollisionSettings From(NamedObjectSave save) => new()
    {
        FirstCollisionName = Text(save, "FirstCollisionName"),
        SecondCollisionName = Text(save, "SecondCollisionName"),
        CollisionType = (GlueCollisionType)save.Properties.GetValue<int>("CollisionType"),
        FirstMass = Number(save, "FirstCollisionMass", 1f),
        SecondMass = Number(save, "SecondCollisionMass", 1f),
        Elasticity = Number(save, "CollisionElasticity", 1f),
        IsActive = !save.Properties.ContainsValue("IsCollisionActive")
                   || save.Properties.GetValue<bool>("IsCollisionActive"),
        ArePhysicsAppliedAutomatically =
            !save.Properties.ContainsValue("IsAutomaticallyApplyPhysicsChecked")
            || save.Properties.GetValue<bool>("IsAutomaticallyApplyPhysicsChecked"),
        FirstSubCollision = SubCollision(save, "FirstSubCollisionSelectedItem"),
        SecondSubCollision = SubCollision(save, "SecondSubCollisionSelectedItem"),
    };

    private static string? Text(NamedObjectSave save, string name)
    {
        string? value = save.Properties.GetValue<string>(name);
        return string.IsNullOrEmpty(value) ? null : value;
    }

    /// <summary>"&lt;Entire Object&gt;" is Glue's sentinel for "no sub-collision".</summary>
    private static string? SubCollision(NamedObjectSave save, string name)
    {
        string? value = Text(save, name);
        return value == "<Entire Object>" ? null : value;
    }

    private static float Number(NamedObjectSave save, string name, float fallback) =>
        save.Properties.ContainsValue(name) ? save.Properties.GetValue<float>(name) : fallback;
}
