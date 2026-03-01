using System.Collections.Generic;
using System.Numerics;
using Microsoft.Xna.Framework.Graphics;
using FlatRedBall2.Collision;
using FlatRedBall2.Math;
using FlatRedBall2.Rendering;

namespace FlatRedBall2;

public class Entity : ICollidable, IAttachable
{
    private readonly List<IAttachable> _children = new();
    private readonly List<ICollidable> _shapes = new();

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
    public FlatRedBallService? Engine { get; internal set; }

    // Internal access to shapes for collision
    internal IReadOnlyList<ICollidable> Shapes => _shapes;

    public void AddChild(IAttachable child)
    {
        child.Parent = this;
        _children.Add(child);

        if (child is ICollidable collidable)
            _shapes.Add(collidable);

        if (child is IRenderable renderable && Engine?.CurrentScreen != null)
            Engine.CurrentScreen.RenderList.Add(renderable);

        if (child is Entity childEntity)
            childEntity.Engine = Engine;
    }

    public void RemoveChild(IAttachable child)
    {
        _children.Remove(child);
        child.Parent = null;

        if (child is ICollidable collidable)
            _shapes.Remove(collidable);

        if (child is IRenderable renderable)
            Engine?.CurrentScreen?.RenderList.Remove(renderable);
    }

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
        Parent?.RemoveChild(this);
        foreach (var child in new List<IAttachable>(_children))
            child.Destroy();
        _children.Clear();
        _shapes.Clear();
    }

    // ICollidable — aggregates all attached shapes
    public bool CollidesWith(ICollidable other)
    {
        var otherShapes = GetShapes(other);
        foreach (var myShape in _shapes)
            foreach (var otherShape in otherShapes)
                if (CollisionDispatcher.CollidesWith(myShape, otherShape))
                    return true;
        return false;
    }

    public Vector2 GetSeparationVector(ICollidable other)
    {
        var otherShapes = GetShapes(other);
        foreach (var myShape in _shapes)
            foreach (var otherShape in otherShapes)
            {
                var sep = CollisionDispatcher.GetSeparationVector(myShape, otherShape);
                if (sep != Vector2.Zero)
                    return sep;
            }
        return Vector2.Zero;
    }

    public void SeparateFrom(ICollidable other, float thisMass = 1f, float otherMass = 1f)
    {
        var sep = GetSeparationVector(other);
        if (sep == Vector2.Zero) return;

        float totalMass = thisMass + otherMass;
        if (totalMass == 0) return;

        if (thisMass != 0)
        {
            float ratio = otherMass == 0 ? 1f : otherMass / totalMass;
            X += sep.X * ratio;
            Y += sep.Y * ratio;
        }

        if (otherMass != 0 && other is Entity otherEntity)
        {
            float ratio = thisMass == 0 ? 1f : thisMass / totalMass;
            otherEntity.X -= sep.X * ratio;
            otherEntity.Y -= sep.Y * ratio;
        }
    }

    public void AdjustVelocityFrom(ICollidable other, float thisMass = 1f, float otherMass = 1f, float elasticity = 1f)
    {
        if (!CollidesWith(other)) return;

        if (other is Entity otherEntity)
        {
            float totalMass = thisMass + otherMass;
            if (totalMass == 0) return;

            float thisRatio = otherMass == 0 ? 1f : otherMass / totalMass;
            float otherRatio = thisMass == 0 ? 1f : thisMass / totalMass;

            float dvx = (otherEntity.VelocityX - VelocityX) * elasticity;
            float dvy = (otherEntity.VelocityY - VelocityY) * elasticity;

            VelocityX += dvx * thisRatio;
            VelocityY += dvy * thisRatio;
            if (otherMass != 0)
            {
                otherEntity.VelocityX -= dvx * otherRatio;
                otherEntity.VelocityY -= dvy * otherRatio;
            }
        }
    }

    private static IReadOnlyList<ICollidable> GetShapes(ICollidable collidable)
    {
        if (collidable is Entity entity) return entity._shapes;
        return new[] { collidable };
    }
}
