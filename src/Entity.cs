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
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public Vector2 Position
    {
        get => new Vector2(X, Y);
        set { X = value.X; Y = value.Y; }
    }

    // Absolute world position
    public float AbsoluteX => Parent != null ? Parent.AbsoluteX + X : X;
    public float AbsoluteY => Parent != null ? Parent.AbsoluteY + Y : Y;
    public float AbsoluteZ => Parent != null ? Parent.AbsoluteZ + Z : Z;

    // Rotation
    public Angle Rotation { get; set; }
    public Angle AbsoluteRotation => Parent != null ? Parent.AbsoluteRotation + Rotation : Rotation;

    // Physics
    public float VelocityX { get; set; }
    public float VelocityY { get; set; }
    public Vector2 Velocity
    {
        get => new Vector2(VelocityX, VelocityY);
        set { VelocityX = value.X; VelocityY = value.Y; }
    }
    public float AccelerationX { get; set; }
    public float AccelerationY { get; set; }
    public Vector2 Acceleration
    {
        get => new Vector2(AccelerationX, AccelerationY);
        set { AccelerationX = value.X; AccelerationY = value.Y; }
    }
    public float Drag { get; set; }

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

    public void AddChild(IAttachable child)
    {
        child.Parent = this;
        _children.Add(child);

        if (child is ICollidable collidable)
            _shapes.Add(collidable);

        if (child is IRenderable renderable && _engine?.CurrentScreen != null)
            _engine!.CurrentScreen.RenderList.Add(renderable);

        if (child is Entity childEntity)
            childEntity.Engine = _engine;
    }

    public void RemoveChild(IAttachable child)
    {
        _children.Remove(child);
        child.Parent = null;

        if (child is ICollidable collidable)
            _shapes.Remove(collidable);

        if (child is IRenderable renderable)
            _engine?.CurrentScreen?.RenderList.Remove(renderable);
    }

    /// <summary>
    /// Adds a Gum visual to this entity in world space. The visual's screen position tracks
    /// this entity's <c>AbsoluteX/Y</c> each frame. Automatically removed when the entity is destroyed.
    /// </summary>
    /// <remarks>Call from <see cref="CustomInitialize"/> or later — requires the entity to be registered with the engine.</remarks>
    public void AddGum(GraphicalUiElement visual, float z = 0f)
    {
        _gumChildren.Add(visual);
        Engine.CurrentScreen.AddGumForEntity(visual, this, z);
    }

    /// <summary>
    /// Adds a Gum Forms control to this entity in world space. The visual's screen position
    /// tracks this entity's <c>AbsoluteX/Y</c> each frame. Automatically removed when the entity is destroyed.
    /// </summary>
    /// <remarks>Call from <see cref="CustomInitialize"/> or later — requires the entity to be registered with the engine.</remarks>
    public void AddGum(FrameworkElement element, float z = 0f)
        => AddGum(element.Visual, z);

    /// <summary>Removes a Gum visual previously added with <see cref="AddGum(GraphicalUiElement, float)"/>.</summary>
    public void RemoveGum(GraphicalUiElement visual)
    {
        _gumChildren.Remove(visual);
        _engine?.CurrentScreen?.RemoveGum(visual);
    }

    /// <summary>Removes a Gum Forms control previously added with <see cref="AddGum(FrameworkElement, float)"/>.</summary>
    public void RemoveGum(FrameworkElement element)
        => RemoveGum(element.Visual);

    // Called by Screen each frame before CustomActivity
    internal void PhysicsUpdate(FrameTime frameTime)
    {
        if (Parent != null) return; // only root entities drive physics; children move with parent

        float dt = frameTime.DeltaSeconds;
        float halfDt2 = 0.5f * dt * dt;

        X += VelocityX * dt + AccelerationX * halfDt2;
        Y += VelocityY * dt + AccelerationY * halfDt2;
        VelocityX += AccelerationX * dt;
        VelocityY += AccelerationY * dt;
        VelocityX -= VelocityX * Drag * dt;
        VelocityY -= VelocityY * Drag * dt;
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
            _engine?.CurrentScreen?.RemoveGum(visual);
        _gumChildren.Clear();
        Parent?.RemoveChild(this);
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
        X += offset.X;
        Y += offset.Y;
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
            float relVelAlongNormal = Vector2.Dot(
                new Vector2(VelocityX - otherEntity.VelocityX, VelocityY - otherEntity.VelocityY),
                normal);

            // Skip if already separating — prevents double-bouncing on the same frame.
            if (relVelAlongNormal >= 0) return;

            float impulse = -(1f + elasticity) * relVelAlongNormal;

            VelocityX += impulse * thisRatio * normal.X;
            VelocityY += impulse * thisRatio * normal.Y;
            if (otherMass != 0)
            {
                otherEntity.VelocityX -= impulse * otherRatio * normal.X;
                otherEntity.VelocityY -= impulse * otherRatio * normal.Y;
            }
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
}
