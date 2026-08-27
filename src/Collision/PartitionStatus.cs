namespace FlatRedBall2.Collision;

/// <summary>
/// Whether a collision relationship is using the sweep-and-prune broad phase, and when it is not,
/// whether that is something the game can act on. Reported per relationship by
/// <see cref="FlatRedBall2.Diagnostics.PerformanceMonitor.GetCollisionReport"/>.
/// </summary>
public enum PartitionStatus
{
    /// <summary>
    /// Both lists are a <see cref="Factory{T}"/> sharing the same non-null
    /// <see cref="Factory{T}.PartitionAxis"/>, so sweep-and-prune culls candidate pairs.
    /// </summary>
    Partitioned,

    /// <summary>
    /// Both lists are a <see cref="Factory{T}"/> and so <em>could</em> partition, but their
    /// <see cref="Factory{T}.PartitionAxis"/> values are unset or don't match, leaving the full
    /// O(n×m) pass. The only status worth acting on — set a matching axis on both factories.
    /// </summary>
    Unpartitioned,

    /// <summary>
    /// At least one list is not a <see cref="Factory{T}"/>, so sweep-and-prune cannot apply no
    /// matter how <see cref="Factory{T}.PartitionAxis"/> is set. Usually not a problem: the
    /// <c>staticGeometry</c> and single-entity overloads wrap their side in a one-element list, and
    /// <see cref="TileShapes"/> does its own cell-range lookup. A large plain <c>List&lt;T&gt;</c>
    /// is the exception — move it to a <see cref="Factory{T}"/> to make partitioning available.
    /// </summary>
    NotApplicable,
}
