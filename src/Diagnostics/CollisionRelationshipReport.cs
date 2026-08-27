using FlatRedBall2.Collision;

namespace FlatRedBall2.Diagnostics;

/// <summary>
/// One collision relationship's severity as of the last frame <see cref="PerformanceMonitor"/>
/// recorded — mirrors FRB1's collision debugger output. See
/// <see cref="PerformanceMonitor.GetCollisionReport"/>.
/// </summary>
public readonly struct CollisionRelationshipReport
{
    /// <summary>The relationship's two collidable type names, e.g. "Enemy vs PlayerBullet".</summary>
    public string Name { get; init; }

    /// <summary>Narrow-phase checks this relationship performed on the last recorded frame.</summary>
    public int DeepCollisionCount { get; init; }

    /// <summary>
    /// Whether the sweep-and-prune broad phase engaged. Only
    /// <see cref="Collision.PartitionStatus.Unpartitioned"/> is worth acting on — see that enum
    /// for why <see cref="Collision.PartitionStatus.NotApplicable"/> is not a defect.
    /// </summary>
    public PartitionStatus PartitionStatus { get; init; }
}
