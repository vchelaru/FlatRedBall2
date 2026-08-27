namespace FlatRedBall2.Collision;

internal interface ICollisionRelationship
{
    void RunCollisions();
    int DeepCollisionCount { get; }

    /// <summary>
    /// When <c>false</c>, <see cref="CollisionSystem.RunAllCollisions"/> skips this relationship and
    /// <see cref="FlatRedBall2.Diagnostics.PerformanceMonitor"/> omits it from the collision report.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Whether the sweep-and-prune broad phase engaged, and when it did not, whether a matching
    /// <see cref="Factory{T}.PartitionAxis"/> could make it. Read by
    /// <see cref="FlatRedBall2.Diagnostics.PerformanceMonitor"/> for its collision severity report.
    /// </summary>
    PartitionStatus PartitionStatus { get; }

    /// <summary>Human-readable label for diagnostics, e.g. "Enemy vs PlayerBullet".</summary>
    string DisplayName { get; }
}
