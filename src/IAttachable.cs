namespace FlatRedBall2;

/// <summary>
/// Provides spatial parent-child attachment with position inheritance.
/// <para>
/// Implementations interpret <see cref="X"/>/<see cref="Y"/>/<see cref="Z"/> as offsets
/// from <see cref="Parent"/> when attached, or as world-space coordinates when <see cref="Parent"/>
/// is <c>null</c>. The <c>Absolute*</c> properties walk the parent chain and return the final
/// world-space value.
/// </para>
/// </summary>
public interface ISpatialAttachable
{
    /// <summary>The entity this object is attached to, or <c>null</c> if it is a root.</summary>
    Entity? Parent { get; set; }

    /// <summary>X offset from <see cref="Parent"/>, or world X when <see cref="Parent"/> is <c>null</c>.</summary>
    float X { get; set; }

    /// <summary>Y offset from <see cref="Parent"/>, or world Y when <see cref="Parent"/> is <c>null</c>. Y+ is up.</summary>
    float Y { get; set; }

    /// <summary>Z offset from <see cref="Parent"/>, or world Z when <see cref="Parent"/> is <c>null</c>. See <see cref="Entity.Z"/> for draw-order semantics.</summary>
    float Z { get; set; }

    /// <summary>Final world-space X after walking the <see cref="Parent"/> chain.</summary>
    float AbsoluteX { get; }

    /// <summary>Final world-space Y after walking the <see cref="Parent"/> chain. Y+ is up.</summary>
    float AbsoluteY { get; }

    /// <summary>Final Z after walking the <see cref="Parent"/> chain.</summary>
    float AbsoluteZ { get; }
}

/// <summary>
/// Signals that this object can be removed from the engine and any parent hierarchy.
/// </summary>
public interface IDestroyable
{
    /// <summary>Releases this object's resources. Called recursively by <see cref="Entity.Destroy"/> on every child.</summary>
    void Destroy();
}

/// <summary>
/// Combined spatial attachment and destruction contract. Prefer <see cref="ISpatialAttachable"/>
/// or <see cref="IDestroyable"/> individually when only one concern is needed.
/// </summary>
public interface IAttachable : ISpatialAttachable, IDestroyable { }
