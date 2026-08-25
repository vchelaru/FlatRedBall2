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
    /// <c>false</c> when this relationship is not benefiting from the sweep-and-prune broad phase
    /// — its lists don't share a matching <c>Factory&lt;T&gt;.PartitionAxis</c> — so every check
    /// runs the full O(n×m) pass. Mirrors FRB1's "NOT PARTITIONED" collision debugger warning.
    /// </summary>
    public bool IsPartitioned { get; init; }
}
