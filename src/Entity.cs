using System;
using System.Collections.Generic;
using System.Numerics;
using FlatRedBall2.Collision;
using FlatRedBall2.Math;
using FlatRedBall2.Rendering;
using Gum.Forms.Controls;
using Gum.Wireframe;

namespace FlatRedBall2;

/// <summary>
/// Base class for everything in the game world that has a position, can attach children
/// (shapes, sprites, Gum visuals, sub-entities), and participates in per-frame update
/// and collision.
/// <para>
/// <b>Create via <see cref="Factory{T}.Create()"/></b> — the factory injects <see cref="Engine"/>
/// and calls <see cref="CustomInitialize"/> at the correct time. Direct <c>new Entity()</c>
/// is only safe when followed immediately by <see cref="Screen.Register"/> plus a manual
/// <see cref="CustomInitialize"/> call.
/// </para>
/// <para>
/// <b>Root vs. child behavior:</b> a root entity (no <see cref="Parent"/>) runs its own
/// physics each frame — position, velocity, acceleration, drag, and rotation integration.
/// A child entity (attached via <see cref="Add(IAttachable, Layer?)"/>) moves rigidly with
/// its parent — <see cref="X"/>/<see cref="Y"/>/<see cref="Rotation"/> are interpreted as
/// offsets from the parent, and per-child physics is skipped.
/// </para>
/// <para>
/// <b>Lifecycle:</b> <see cref="CustomInitialize"/> → per-frame <see cref="CustomActivity"/> →
/// <see cref="Destroy"/> calls <see cref="CustomDestroy"/> and recursively destroys all
/// attached children.
/// </para>
/// </summary>
public class Entity : ICollidable, IAttachable
{
    private readonly List<IAttachable> _children = new();
    private readonly List<ICollidable> _shapes = new();
    private readonly List<GraphicalUiElement> _gumChildren = new();

    /// <summary>
    /// Optional logical name for diagnostics, snapshots, and game-specific lookup.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// 2D position. Interpreted in world space when this entity has no <see cref="Parent"/>,
    /// or as an offset from the parent otherwise. Exposed as a field (not a property) so it
    /// can be mutated component-wise without triggering finite-value validation on every write.
    /// Prefer <see cref="X"/> / <see cref="Y"/> if you want NaN/∞ to throw.
    /// </summary>
    public Vector2 Position;

    /// <summary>
    /// X-axis position. Throws <see cref="InvalidOperationException"/> on set if the value
    /// is not finite (NaN or ±∞). Relative to <see cref="Parent"/> when attached; world when root.
    /// </summary>
    public float X { get => Position.X; set { ThrowIfNotFinite(value, nameof(X)); Position.X = value; } }

    /// <summary>
    /// Y-axis position (Y+ up in world space). Throws <see cref="InvalidOperationException"/> on
    /// set if the value is not finite. Relative to <see cref="Parent"/> when attached; world when root.
    /// </summary>
    public float Y { get => Position.Y; set { ThrowIfNotFinite(value, nameof(Y)); Position.Y = value; } }
    /// <summary>
    /// Contributes to <see cref="AbsoluteZ"/> for position calculations (e.g. attachment to a parent).
    /// Does <b>not</b> directly control draw order — rendering is sorted by each child renderable's own
    /// <c>Z</c> value (e.g. <c>myCircle.Z</c>, <c>mySprite.Z</c>), not by this property.
    /// <para>
    /// Draw order within a layer: lower Z is drawn first (behind); higher Z is drawn last (in front).
    /// Two renderables at the same layer and same Z preserve their insertion order (stable sort) —
    /// whichever was added to the screen first is drawn first. If you need explicit ordering between
    /// two same-layer renderables, give them distinct Z values.
    /// </para>
    /// <para>
    /// Layer takes priority over Z: a renderable on a lower-indexed layer always draws behind one on a
    /// higher-indexed layer, regardless of Z. Set <see cref="Layer"/> to change layers at runtime.
    /// </para>
    /// </summary>
    public float Z { get; set; }

    private Layer? _layer;

    /// <summary>
    /// The rendering layer for this entity's children. Setting this propagates to all
    /// renderable children (shapes, sprites, Gum visuals) and child entities recursively.
    /// New children added after this is set inherit the current layer automatically.
    /// </summary>
    public Layer? Layer
    {
        get => _layer;
        set
        {
            _layer = value;
            foreach (var child in _children)
            {
                if (child is IRenderable renderable)
                    renderable.Layer = value;
                if (child is Entity childEntity)
                    childEntity.Layer = value;
            }
            foreach (var visual in _gumChildren)
                _engine?.CurrentScreen?.SetGumRenderableLayer(visual, value);
        }
    }

    /// <summary>
    /// Final world-space X after walking the parent chain. Equal to <see cref="X"/> when this
    /// entity is a root; otherwise <c>Parent.AbsoluteX + X</c>.
    /// </summary>
    public float AbsoluteX => Parent != null ? Parent.AbsoluteX + X : X;

    /// <summary>
    /// Final world-space Y after walking the parent chain. Equal to <see cref="Y"/> when this
    /// entity is a root; otherwise <c>Parent.AbsoluteY + Y</c>.
    /// </summary>
    public float AbsoluteY => Parent != null ? Parent.AbsoluteY + Y : Y;

    /// <summary>
    /// Final Z after walking the parent chain. Equal to <see cref="Z"/> when this entity is a
    /// root; otherwise <c>Parent.AbsoluteZ + Z</c>. Does not directly control draw order —
    /// see <see cref="Z"/> for the rendering implications.
    /// </summary>
    public float AbsoluteZ => Parent != null ? Parent.AbsoluteZ + Z : Z;

    /// <summary>
    /// Maximum distance from <see cref="AbsoluteX"/>/<see cref="AbsoluteY"/> to the far edge of
    /// any attached collision shape, used by the broad phase to cull entity pairs before
    /// per-shape collision checks. Recomputed on access; reflects current shape positions.
    /// </summary>
    public float BroadPhaseRadius
    {
        get
        {
            float max = 0f;
            foreach (var shape in _shapes)
            {
                float offsetDist = MathF.Sqrt(
                    (shape.AbsoluteX - AbsoluteX) * (shape.AbsoluteX - AbsoluteX) +
                    (shape.AbsoluteY - AbsoluteY) * (shape.AbsoluteY - AbsoluteY));
                float r = offsetDist + shape.BroadPhaseRadius;
                if (r > max) max = r;
            }
            return max;
        }
    }

    /// <summary>
    /// Rotation about the Z axis. Relative to <see cref="Parent"/> when attached, world when root.
    /// Integrated by <see cref="RotationVelocity"/> each frame on root entities.
    /// </summary>
    public Angle Rotation { get; set; }

    /// <summary>
    /// Final world-space rotation after walking the parent chain. Equal to <see cref="Rotation"/>
    /// when this entity is a root; otherwise <c>Parent.AbsoluteRotation + Rotation</c>.
    /// </summary>
    public Angle AbsoluteRotation => Parent != null ? Parent.AbsoluteRotation + Rotation : Rotation;

    /// <summary>
    /// Angular velocity applied to <see cref="Rotation"/> each frame on root entities. Child
    /// entities inherit rotation from their parent and this value is ignored.
    /// </summary>
    public Angle RotationVelocity { get; set; }

    /// <summary>
    /// Linear velocity in units/second. Applied each frame on root entities via
    /// <c>Position += Velocity * dt + Acceleration * dt² / 2</c>. Ignored on child entities.
    /// Field (not property) to allow component-wise mutation without per-write validation —
    /// use <see cref="VelocityX"/>/<see cref="VelocityY"/> for finite-value checking.
    /// </summary>
    public Vector2 Velocity;

    /// <summary>X component of <see cref="Velocity"/>; throws on non-finite values.</summary>
    public float VelocityX { get => Velocity.X; set { ThrowIfNotFinite(value, nameof(VelocityX)); Velocity.X = value; } }

    /// <summary>Y component of <see cref="Velocity"/>; throws on non-finite values. Y+ is up.</summary>
    public float VelocityY { get => Velocity.Y; set { ThrowIfNotFinite(value, nameof(VelocityY)); Velocity.Y = value; } }

    /// <summary>
    /// Linear acceleration in units/second². Applied each frame on root entities; ignored on
    /// children. Common use: gravity via <c>AccelerationY = -800f</c> (Y+ is up, so a negative
    /// value pulls down).
    /// </summary>
    public Vector2 Acceleration;

    /// <summary>X component of <see cref="Acceleration"/>; throws on non-finite values.</summary>
    public float AccelerationX { get => Acceleration.X; set { ThrowIfNotFinite(value, nameof(AccelerationX)); Acceleration.X = value; } }

    /// <summary>Y component of <see cref="Acceleration"/>; throws on non-finite values.</summary>
    public float AccelerationY { get => Acceleration.Y; set { ThrowIfNotFinite(value, nameof(AccelerationY)); Acceleration.Y = value; } }

    /// <summary>
    /// Velocity decay coefficient applied each frame as <c>Velocity -= Velocity * (Drag * dt)</c>.
    /// Units: 1/seconds. A value of <c>0</c> disables drag; <c>1</c> removes ~63% of velocity per
    /// second; higher values feel stickier. Ignored on child entities.
    /// </summary>
    public float Drag { get; set; }

    /// <summary>
    /// Cumulative collision separation applied to this entity during the current frame. Reset
    /// to zero at the start of each physics update. Used by collision response code to
    /// reconcile multiple overlapping separations without double-counting.
    /// </summary>
    public Vector2 LastReposition;

    /// <summary>Position at the start of the current frame, captured by <c>PhysicsUpdate</c> before
    /// movement is applied. Used by one-way collision to verify the entity was actually above
    /// the platform before separating it onto the top.</summary>
    public Vector2 LastPosition;

    /// <summary>
    /// The entity this one is attached to, or <c>null</c> if this is a root entity. Set
    /// automatically by <see cref="Add(IAttachable, Layer?)"/> / <see cref="Remove(IAttachable)"/>; manual
    /// assignment is permitted but bypasses child-list bookkeeping and is rarely correct.
    /// </summary>
    public Entity? Parent { get; set; }

    /// <summary>
    /// All attached children (shapes, sprites, Gum visuals, sub-entities). Populated by
    /// <see cref="Add(IAttachable, Layer?)"/> and <see cref="Add{T}(T, bool, Layer?)"/>.
    /// </summary>
    public IReadOnlyList<IAttachable> Children => _children;

    /// <summary>
    /// Hides this entity and all of its renderable descendants (sprites, shapes, Gum visuals,
    /// child entities) when <c>false</c>. Applied at draw time via the parent chain, so per-child
    /// <c>IsVisible</c> values are preserved — toggling this on and off does not clobber
    /// individually-hidden shapes or sprites.
    /// <para>
    /// Does not affect physics, collision, or <see cref="CustomActivity"/>: an invisible entity
    /// still moves, collides, and ticks its activity. Use <see cref="Screen.PauseThisScreen"/>
    /// or a custom gameplay flag to stop behavior.
    /// </para>
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// <c>true</c> only when this entity and every ancestor entity have <see cref="IsVisible"/>
    /// set to <c>true</c>. The render loop consults this for every renderable's parent chain and
    /// skips drawing anything whose absolute visibility is <c>false</c>. Naming mirrors
    /// <see cref="AbsoluteX"/> / <see cref="AbsoluteY"/>: the final computed value after walking
    /// the parent chain.
    /// </summary>
    public bool IsAbsoluteVisible => IsVisible && (Parent == null || Parent.IsAbsoluteVisible);

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

    // Tween list — advanced by Screen.Update each frame, cleared on Destroy.
    internal readonly Tweening.TweenList _tweens = new();

    /// <summary>
    /// Controls whether this entity's tweens advance this frame. Default <c>true</c>.
    /// Override to pause tweens for this entity without stopping them (e.g., during a
    /// stun or per-entity pause state).
    /// </summary>
    protected internal virtual bool ShouldAdvanceTweens => true;

    /// <summary>
    /// Attaches <paramref name="child"/> to this entity and registers it for rendering.
    /// If <paramref name="child"/> is an <see cref="ICollidable"/> shape, it is included in default collision.
    /// To attach a collidable shape without including it in default collision, use
    /// <see cref="Add{T}(T, bool, Layer?)"/> with <c>isDefaultCollision: false</c>.
    /// </summary>
    public void Add(IAttachable child, Layer? layer = null)
    {
        child.Parent = this;
        _children.Add(child);

        if (child is ICollidable collidable)
            _shapes.Add(collidable);

        if (child is IRenderable renderable && _engine?.CurrentScreen != null)
            _engine!.CurrentScreen.Add(renderable, layer ?? Layer);

        if (child is Entity childEntity)
        {
            if (_engine is not null)
                childEntity.Engine = _engine;
            if (layer != null || Layer != null)
                childEntity.Layer = layer ?? Layer;
        }
    }

    /// <summary>
    /// Attaches a collidable shape to this entity and registers it for rendering.
    /// Pass <c>isDefaultCollision: false</c> to attach the shape for rendering and positioning only —
    /// it will not participate in <see cref="CollidesWith"/> or <see cref="GetSeparationVector"/> checks.
    /// Use <see cref="SetDefaultCollision"/> to change participation after the fact.
    /// </summary>
    public void Add<T>(T child, bool isDefaultCollision, Layer? layer = null) where T : class, IAttachable, ICollidable
    {
        child.Parent = this;
        _children.Add(child);

        if (isDefaultCollision)
            _shapes.Add(child);

        if (child is IRenderable renderable && _engine?.CurrentScreen != null)
            _engine!.CurrentScreen.Add(renderable, layer ?? Layer);

        if (child is Entity childEntity)
        {
            if (_engine is not null)
                childEntity.Engine = _engine;
            if (layer != null || Layer != null)
                childEntity.Layer = layer ?? Layer;
        }
    }

    /// <summary>
    /// Sets whether <paramref name="shape"/> participates in this entity's default collision.
    /// When excluded, the shape remains attached and rendered but is not included in
    /// <see cref="CollidesWith"/> or <see cref="GetSeparationVector"/> checks.
    /// Safe to call multiple times with the same value — idempotent, no error, no duplicate.
    /// </summary>
    /// <remarks>
    /// "Default collision" refers to the set of shapes checked by standard collision relationships.
    /// A shape excluded from default collision can still be targeted by a per-shape collision
    /// relationship when that feature is supported.
    /// </remarks>
    public void SetDefaultCollision(ICollidable shape, bool isDefaultCollision)
    {
        if (shape is IAttachable attachable && !_children.Contains(attachable))
            throw new InvalidOperationException(
                "SetDefaultCollision called with a shape that is not a child of this entity. Call Add() first.");

        if (isDefaultCollision)
        {
            if (!_shapes.Contains(shape))
                _shapes.Add(shape);
        }
        else
        {
            _shapes.Remove(shape);
        }
    }

    /// <summary>
    /// Detaches <paramref name="child"/> from this entity: clears its <see cref="Parent"/>,
    /// removes it from collision participation if applicable, and unregisters it from the
    /// screen's render list. Does not destroy the child — use <see cref="Destroy"/> for that.
    /// </summary>
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
    public void Add(GraphicalUiElement visual, Layer? layer = null)
    {
        _gumChildren.Add(visual);
        Engine.CurrentScreen.AddGumForEntity(visual, this, layer ?? Layer);
    }

    /// <summary>
    /// Adds a Gum Forms control to this entity in world space. The visual's screen position
    /// tracks this entity's <c>AbsoluteX/Y</c> each frame. Automatically removed when the entity is destroyed.
    /// </summary>
    /// <remarks>Call from <see cref="CustomInitialize"/> or later — requires the entity to be registered with the engine.</remarks>
    public void Add(FrameworkElement element, Layer? layer = null)
        => Add(element.Visual, layer);

    /// <summary>Removes a Gum visual previously added with <see cref="Add(GraphicalUiElement, Layer?)"/>.</summary>
    public void Remove(GraphicalUiElement visual)
    {
        _gumChildren.Remove(visual);
        _engine?.CurrentScreen?.Remove(visual);
    }

    /// <summary>Removes a Gum Forms control previously added with <see cref="Add(FrameworkElement, Layer?)"/>.</summary>
    public void Remove(FrameworkElement element)
        => Remove(element.Visual);

    // Called by Screen each frame before CustomActivity
    internal void PhysicsUpdate(FrameTime frameTime)
    {
        if (Parent != null) return; // only root entities drive physics; children move with parent

        LastReposition = Vector2.Zero;
        LastPosition = new Vector2(Position.X, Position.Y);
        float dt = frameTime.DeltaSeconds;
        float halfDt2 = 0.5f * dt * dt;

        Position += Velocity * dt + Acceleration * halfDt2;
        Velocity += Acceleration * dt;
        Velocity -= Velocity * (Drag * dt);
        Rotation += RotationVelocity * dt;
    }

    // Lifecycle

    /// <summary>
    /// Called by <see cref="Factory{T}.Create()"/> immediately after engine injection.
    /// <see cref="Engine"/> and <see cref="Engine"/>.<see cref="FlatRedBallService.CurrentScreen"/>
    /// are both available — use this (not the constructor) to access engine services or add child shapes.
    /// </summary>
    /// <remarks>
    /// <see cref="Screen.Register"/> does <b>not</b> call this automatically. If you instantiate an
    /// entity with <c>new</c> and register it manually, call <c>CustomInitialize()</c> yourself after
    /// registration.
    /// </remarks>
    public virtual void CustomInitialize() { }

    /// <summary>
    /// Override to run per-frame game logic. Called by <see cref="Screen"/> every frame after
    /// physics and collision have resolved, so positions and velocities reflect the current
    /// frame's state. Skipped while the screen is paused (<see cref="Screen.IsPaused"/>).
    /// </summary>
    public virtual void CustomActivity(FrameTime time) { }

    /// <summary>
    /// Override to release game-specific resources when this entity is destroyed. Called by
    /// <see cref="Destroy"/> before children are torn down, while engine services, tweens,
    /// and attached shapes are still valid.
    /// </summary>
    public virtual void CustomDestroy() { }

    /// <summary>
    /// Removes this entity from the game. Safe to call at any time, including from inside a
    /// collision handler or <see cref="CustomActivity"/> on this or a different entity.
    /// <para>
    /// Order of operations: <see cref="CustomDestroy"/> runs first (with children/shapes/tweens
    /// still intact), then tweens clear, Gum children detach, the parent detaches this entity,
    /// all attached children are destroyed recursively, shape/child lists clear, and finally
    /// the factory/screen registration is released.
    /// </para>
    /// </summary>
    public void Destroy()
    {
        CustomDestroy();
        _tweens.Clear();
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

    /// <summary>
    /// Cancels every tween currently owned by this entity. Each tween's value is left frozen
    /// wherever its curve was last sampled — no terminal snap to the target value, no
    /// <c>Ended</c> invocation. Matches the no-setter-fired semantics of <see cref="FlatRedBall.Glue.StateInterpolation.Tweener.Stop"/>
    /// and the auto-cleanup that runs during <see cref="Destroy"/>. Safe to call when no tweens
    /// are active.
    /// </summary>
    public void StopAllTweens() => _tweens.Clear();

    /// <summary>
    /// Returns <c>true</c> if any shape attached to this entity (recursively, through child
    /// entities) overlaps <paramref name="other"/>. Shapes excluded from default collision
    /// via <see cref="SetDefaultCollision"/> or <c>isDefaultCollision: false</c> are skipped.
    /// </summary>
    public bool CollidesWith(ICollidable other)
    {
        foreach (var myLeaf in GetLeafShapes(this))
            foreach (var otherLeaf in GetLeafShapes(other))
                if (CollisionDispatcher.CollidesWith(myLeaf, otherLeaf))
                    return true;
        return false;
    }

    /// <summary>
    /// Returns the minimum translation vector that pushes this entity's shapes out of
    /// <paramref name="other"/>. Zero if there is no overlap. Returns the first non-zero
    /// separation found — not a combined MTV across multiple overlapping shape pairs.
    /// </summary>
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

    /// <summary>
    /// Pushes this entity out of <paramref name="other"/> using the mass-weighted share of the
    /// separation vector. A mass ratio of <c>thisMass = 1, otherMass = 0</c> applies the full
    /// separation to this entity; equal masses split the separation 50/50. Updates
    /// <see cref="LastReposition"/>.
    /// </summary>
    public void SeparateFrom(ICollidable other, float thisMass = 1f, float otherMass = 1f)
    {
        var offset = CollisionDispatcher.ComputeSeparationOffset(GetSeparationVector(other), thisMass, otherMass);
        Position += offset;
        LastReposition += offset;
    }

    /// <summary>
    /// Adds <paramref name="offset"/> to this entity's position and accumulates it into
    /// <see cref="LastReposition"/>. Use when you've already computed the separation externally
    /// (e.g., from a custom collision query) and need to apply it while keeping the frame's
    /// reposition accounting consistent.
    /// </summary>
    public void ApplySeparationOffset(Vector2 offset)
    {
        Position += offset;
        LastReposition += offset;
    }

    /// <summary>
    /// Reflects this entity's velocity away from <paramref name="other"/> using the collision
    /// normal derived from <see cref="GetSeparationVector"/>. If <paramref name="other"/> is
    /// an <see cref="Entity"/>, both entities' velocities are updated; otherwise
    /// <paramref name="other"/> is treated as immovable static geometry.
    /// </summary>
    /// <param name="other">The other collidable.</param>
    /// <param name="thisMass">Mass of this entity.</param>
    /// <param name="otherMass">Mass of the other entity.</param>
    /// <param name="elasticity">
    /// <c>0</c> = fully inelastic (no bounce, velocity zeroed along the normal);
    /// <c>1</c> = perfectly elastic (full bounce). Values above 1 amplify energy.
    /// </param>
    public void AdjustVelocityFrom(ICollidable other, float thisMass = 1f, float otherMass = 1f, float elasticity = 1f)
        => AdjustVelocityFromSeparation(GetSeparationVector(other), other, thisMass, otherMass, elasticity);

    /// <summary>
    /// Lower-level variant of <see cref="AdjustVelocityFrom"/> that accepts a pre-computed
    /// separation vector <paramref name="sep"/>. Use when you've already called
    /// <see cref="GetSeparationVector"/> and want to reuse the result instead of recomputing.
    /// A zero <paramref name="sep"/> short-circuits — no-op, no division.
    /// </summary>
    public void AdjustVelocityFromSeparation(Vector2 sep, ICollidable other, float thisMass = 1f, float otherMass = 1f, float elasticity = 1f)
        => AdjustVelocityFromSeparation(sep, other, thisMass, otherMass, elasticity, axisAlignedSeparation: false);

    /// <inheritdoc cref="ICollidable.AdjustVelocityFromSeparation(Vector2, ICollidable, float, float, float, bool)"/>
    public void AdjustVelocityFromSeparation(Vector2 sep, ICollidable other, float thisMass, float otherMass, float elasticity, bool axisAlignedSeparation)
    {
        if (sep == Vector2.Zero) return;

        // Perpendicular-contact decomposition: when the sep is known to be a sum of
        // axis-aligned per-tile pushes (wall + floor corner), resolve as two independent
        // axis impulses instead of one diagonal normal. A single normalize(sep) impulse
        // would tilt large horizontal momentum into vertical (wall-slam pop-up).
        // For genuinely diagonal normals (slope polygon SAT) this flag is false and the
        // original single-normal formula runs — otherwise slope reflection is wrong.
        if (axisAlignedSeparation && sep.X != 0f && sep.Y != 0f && other is not Entity)
        {
            ApplyOneSidedAxisImpulse(new Vector2(MathF.Sign(sep.X), 0f), thisMass, otherMass, elasticity);
            ApplyOneSidedAxisImpulse(new Vector2(0f, MathF.Sign(sep.Y)), thisMass, otherMass, elasticity);
            return;
        }

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

    // Helper for the TileShapeCollection per-axis decomposition path above.
    // TileShapeCollection is static geometry, so this mirrors the non-Entity branch
    // of AdjustVelocityFromSeparation but against a pre-chosen axis-aligned normal.
    private void ApplyOneSidedAxisImpulse(Vector2 normal, float thisMass, float otherMass, float elasticity)
    {
        float totalMass = thisMass + otherMass;
        if (totalMass == 0f) return;

        float thisRatio = otherMass == 0f ? 1f : otherMass / totalMass;
        float relVelAlongNormal = Vector2.Dot(Velocity, normal);
        if (relVelAlongNormal >= 0f) return;

        float impulse = -(1f + elasticity) * relVelAlongNormal;
        Velocity += impulse * thisRatio * normal;
    }

    // Recursively yields the primitive shapes (Circle, AxisAlignedRectangle, Polygon) reachable
    // from this collidable. Child entities are transparent containers — their shapes are yielded
    // in-place rather than the child entity itself, so CollisionDispatcher always receives
    // concrete shape types it can handle.
    // Internal so CollisionRelationship can iterate leaf shapes for per-shape selector dispatch.
    internal static IEnumerable<ICollidable> GetLeafShapes(ICollidable collidable)
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
