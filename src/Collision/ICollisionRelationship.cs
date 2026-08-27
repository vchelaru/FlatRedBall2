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
    /// <c>false</c> when this relationship falls back to the O(n×m) check because its lists don't
    /// share a matching <see cref="Factory{T}.PartitionAxis"/>. Read by
    /// <see cref="FlatRedBall2.Diagnostics.PerformanceMonitor"/> for its collision severity report.
    /// </summary>
    bool IsPartitioned { get; }

    /// <summary>Human-readable label for diagnostics, e.g. "Enemy vs PlayerBullet".</summary>
    string DisplayName { get; }
}
