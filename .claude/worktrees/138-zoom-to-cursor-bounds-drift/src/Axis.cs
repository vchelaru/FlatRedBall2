namespace FlatRedBall2;

/// <summary>
/// World-space axis used by <see cref="Factory{T}.PartitionAxis"/> to sort entities for
/// broad-phase collision culling. Choose the axis along which your entities are most spread out.
/// </summary>
public enum Axis
{
    /// <summary>Horizontal axis. Choose for entities spread mostly left/right (most platformers).</summary>
    X,
    /// <summary>Vertical axis. Choose for entities spread mostly up/down (vertical scrollers, climb-ups).</summary>
    Y,
}
