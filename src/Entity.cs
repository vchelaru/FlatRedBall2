using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Xna.Framework.Graphics;
using FlatRedBall2.Collision;
using FlatRedBall2.Math;
using FlatRedBall2.Rendering;
using Gum.Forms.Controls;
using Gum.Wireframe;

namespace FlatRedBall2;

public class Entity : ICollidable, IAttachable
{
    private readonly List<IAttachable> _children = new();
    private readonly List<ICollidable> _shapes = new();
    private readonly List<GraphicalUiElement> _gumChildren = new();

    // Position — relative to parent when attached, world when root
    public Vector2 Position;
    public float X { get => Position.X; set { ThrowIfNotFinite(value, nameof(X)); Position.X = value; } }
    public float Y { get => Position.Y; set { ThrowIfNotFinite(value, nameof(Y)); Position.Y = value; } }
    public float Z { get; set; }

    // Absolute world position
    public float AbsoluteX => Parent != null ? Parent.AbsoluteX + X : X;
    public float AbsoluteY => Parent != null ? Parent.AbsoluteY + Y : Y;
    public float AbsoluteZ => Parent != null ? Parent.AbsoluteZ + Z : Z;

    // Rotation
    public Angle Rotation { get; set; }
    public Angle AbsoluteRotation => Parent != null ? Parent.AbsoluteRotation + Rotation : Rotation;
    public Angle RotationVelocity { get; set; }

    // Physics
    public Vector2 Velocity;
    public float VelocityX { get => Velocity.X; set { ThrowIfNotFinite(value, nameof(VelocityX)); Velocity.X = value; } }
    public float VelocityY { get => Velocity.Y; set { ThrowIfNotFinite(value, nameof(VelocityY)); Velocity.Y = value; } }
    public Vector2 Acceleration;
    public float AccelerationX { get => Acceleration.X; set { ThrowIfNotFinite(value, nameof(AccelerationX)); Acceleration.X = value; } }
    public float AccelerationY { get => Acceleration.Y; set { ThrowIfNotFinite(value, nameof(AccelerationY)); Acceleration.Y = value; } }
    public float Drag { get; set; }

    // Records cumulative separation applied this frame; reset at the start of each PhysicsUpdate.
    public Vector2 LastReposition;

    // Hierarchy
    public Entity? Parent { get; set; }
    public IReadOnlyList<IAttachable> Children => _children;

    // Visibility
    public bool IsVisible { get; set; } = true;

    // Engine reference — injected by Factory or Screen.Register
    private FlatRedBallService? _engine;

    /// <summary>
    /// The engine instance injected by <see cref="Factory{T}"/> or <c>Screen.Register</c> before
    /// <see cref="CustomInitialize"/> is called. Never null during normal game-code execution.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if accessed before the entity has been registered with the engine.
    /// Create entities via <c>Factory&lt;T&gt;.Create()</c> or <c>Screen.Register()</c>.
    /// </exception>
    public FlatRedBallService Engine
    {
        get => _engine ?? throw new InvalidOperationException(
            "Entity.Engine is null. Create entities via Factory<T>.Create() or Screen.Register().");
        internal set => _engine = value;
    }

    // Set by Factory or Screen.Register; called at the end of Destroy() to remove this entity
    // from its owning container without requiring a back-reference to the factory or screen.
    internal Action? _onDestroy;

    // Internal access to shapes for collision
    internal IReadOnlyList<ICollidable> Shapes => _shapes;

    public void Add(IAttachable child)
    {
        child.Parent = this;
        _children.Add(child);

        if (child is ICollidable collidable)
            _shapes.Add(collidable);

        if (child is IRenderable renderable && _engine?.CurrentScreen != null)
            _engine!.CurrentScreen.Add(renderable);

        if (child is Entity childEntity && _engine is not null)
            childEntity.Engine = _engine;
    }

    public void Remove(IAttachable child)
    {
        _children.Remove(child);
        child.Parent = null;

        if (child is ICollidable collidable)
            _shapes.Remove(collidable);

        if (child is IRenderable renderable)
            _engine?.CurrentScreen?.Remove(renderable);
    }

    /// <summary>
    /// Adds a Gum visual to this entity in world space. The visual's screen position tracks
    /// this entity's <c>AbsoluteX/Y</c> each frame. Automatically removed when the entity is destroyed.
    /// </summary>
    /// <remarks>Call from <see cref="CustomInitialize"/> or later — requires the entity to be registered with the engine.</remarks>
    public void Add(GraphicalUiElement visual, float z = 0f)
    {
        _gumChildren.Add(visual);
        Engine.CurrentScreen.AddGumForEntity(visual, this, z);
    }

    /// <summary>
    /// Adds a Gum Forms control to this entity in world space. The visual's screen position
    /// tracks this entity's <c>AbsoluteX/Y</c> each frame. Automatically removed when the entity is destroyed.
    /// </summary>
    /// <remarks>Call from <see cref="CustomInitialize"/> or later — requires the entity to be registered with the engine.</remarks>
    public void Add(FrameworkElement element, float z = 0f)
        => Add(element.Visual, z);

    /// <summary>Removes a Gum visual previously added with <see cref="Add(GraphicalUiElement, float)"/>.</summary>
    public void Remove(GraphicalUiElement visual)
    {
        _gumChildren.Remove(visual);
        _engine?.CurrentScreen?.Remove(visual);
    }

    /// <summary>Removes a Gum Forms control previously added with <see cref="Add(FrameworkElement, float)"/>.</summary>
    public void Remove(FrameworkElement element)
        => Remove(element.Visual);

    // Called by Screen each frame before CustomActivity
    internal void PhysicsUpdate(FrameTime frameTime)
    {
        if (Parent != null) return; // only root entities drive physics; children move with parent

        LastReposition = Vector2.Zero;
        float dt = frameTime.DeltaSeconds;
        float halfDt2 = 0.5f * dt * dt;

        Position += Velocity * dt + Acceleration * halfDt2;
        Velocity += Acceleration * dt;
        Velocity -= Velocity * (Drag * dt);
        Rotation += RotationVelocity * dt;
    }

    // Lifecycle
    public virtual void CustomInitialize() { }
    public virtual void CustomActivity(FrameTime time) { }
    public virtual void CustomDestroy() { }
    public virtual void CustomDraw(SpriteBatch spriteBatch, Camera camera) { }

    public void Destroy()
    {
        CustomDestroy();
        foreach (var visual in _gumChildren)
            _engine?.CurrentScreen?.Remove(visual);
        _gumChildren.Clear();
        Parent?.Remove(this);
        foreach (var child in new List<IAttachable>(_children))
            child.Destroy();
        _children.Clear();
        _shapes.Clear();
        _onDestroy?.Invoke();
    }

    // ICollidable — aggregates all attached shapes, recursing through child entities.
    public bool CollidesWith(ICollidable other)
    {
        foreach (var myLeaf in GetLeafShapes(this))
            foreach (var otherLeaf in GetLeafShapes(other))
                if (CollisionDispatcher.CollidesWith(myLeaf, otherLeaf))
                    return true;
        return false;
    }

    public Vector2 GetSeparationVector(ICollidable other)
    {
        foreach (var myLeaf in GetLeafShapes(this))
            foreach (var otherLeaf in GetLeafShapes(other))
            {
                var sep = CollisionDispatcher.GetSeparationVector(myLeaf, otherLeaf);
                if (sep != Vector2.Zero)
                    return sep;
            }
        return Vector2.Zero;
    }

    public void SeparateFrom(ICollidable other, float thisMass = 1f, float otherMass = 1f)
    {
        var offset = CollisionDispatcher.ComputeSeparationOffset(GetSeparationVector(other), thisMass, otherMass);
        Position += offset;
        LastReposition += offset;
    }

    public void AdjustVelocityFrom(ICollidable other, float thisMass = 1f, float otherMass = 1f, float elasticity = 1f)
    {
        var sep = GetSeparationVector(other);
        if (sep == Vector2.Zero) return;

        // Collision normal: the direction to push 'this' out of 'other'.
        var normal = Vector2.Normalize(sep);

        if (other is Entity otherEntity)
        {
            float totalMass = thisMass + otherMass;
            if (totalMass == 0) return;

            float thisRatio = otherMass == 0 ? 1f : otherMass / totalMass;
            float otherRatio = thisMass == 0 ? 0f : thisMass / totalMass;

            // Project relative velocity onto the collision normal.
            // Negative means 'this' is moving into 'other'.
            float relVelAlongNormal = Vector2.Dot(Velocity - otherEntity.Velocity, normal);

            // Skip if already separating — prevents double-bouncing on the same frame.
            if (relVelAlongNormal >= 0) return;

            float impulse = -(1f + elasticity) * relVelAlongNormal;

            Velocity += impulse * thisRatio * normal;
            if (otherMass != 0)
                otherEntity.Velocity -= impulse * otherRatio * normal;
        }
        else
        {
            // Static geometry (e.g. TileShapeCollection) — treat other as immovable with zero velocity.
            float totalMass = thisMass + otherMass;
            if (totalMass == 0) return;

            float thisRatio = otherMass == 0 ? 1f : otherMass / totalMass;
            float relVelAlongNormal = Vector2.Dot(Velocity, normal);

            if (relVelAlongNormal >= 0) return;

            float impulse = -(1f + elasticity) * relVelAlongNormal;
            Velocity += impulse * thisRatio * normal;
        }
    }

    // Recursively yields the primitive shapes (Circle, AxisAlignedRectangle, Polygon) reachable
    // from this collidable. Child entities are transparent containers — their shapes are yielded
    // in-place rather than the child entity itself, so CollisionDispatcher always receives
    // concrete shape types it can handle.
    private static IEnumerable<ICollidable> GetLeafShapes(ICollidable collidable)
    {
        if (collidable is Entity entity)
        {
            foreach (var child in entity._shapes)
                foreach (var leaf in GetLeafShapes(child))
                    yield return leaf;
        }
        else
        {
            yield return collidable;
        }
    }

    private static void ThrowIfNotFinite(float value, string propertyName)
    {
        if (!float.IsFinite(value))
            throw new InvalidOperationException(
                $"Attempted to set {propertyName} to {value}. Only finite values are allowed.");
    }
}
