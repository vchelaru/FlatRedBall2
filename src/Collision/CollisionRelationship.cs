using System;
using System.Collections.Generic;
using System.Numerics;
using FlatRedBall2;
using FlatRedBall2.Movement;

namespace FlatRedBall2.Collision;

public class CollisionRelationship<A, B> : ICollisionRelationship
    where A : ICollidable
    where B : ICollidable
{
    private readonly IEnumerable<A> _listA;
    private readonly IEnumerable<B> _listB;

    // Reused snapshot for same-list (self) collision — populated once per frame, zero allocation after warmup.
    private List<A>? _selfSnapshot;

    private bool _moveFirst;
    private bool _moveSecond;
    private bool _moveBoth;
    private float _bothFirstMass = 1f;
    private float _bothSecondMass = 1f;

    private bool _bounce;
    private float _bounceMassA = 1f;
    private float _bounceMassB = 1f;
    private float _bounceElasticity = 1f;

    private Func<A, ICollidable>? _firstShapeSelector;
    private Func<B, ICollidable>? _secondShapeSelector;

    /// <summary>
    /// Controls how this relationship resolves overlap with polygon tiles when one side is a
    /// <see cref="TileShapeCollection"/>. Default <see cref="SlopeCollisionMode.Standard"/> uses
    /// SAT (correct for top-down and non-player-vs-level pairs like a ball vs. tiles). Set to
    /// <see cref="SlopeCollisionMode.PlatformerFloor"/> for platformer player-vs-level to get
    /// heightmap-based vertical separation on floor slopes. Ignored when neither side is a
    /// <see cref="TileShapeCollection"/>.
    /// </summary>
    /// <remarks>
    /// Lives on the relationship — not the collection — so the same tile collection can be used
    /// with different semantics per relationship (e.g., player = PlatformerFloor, ball = Standard).
    /// Also required when <see cref="OneWayDirection"/> is set on a <see cref="TileShapeCollection"/>
    /// containing polygon (sloped) tiles — without PlatformerFloor, sloped cloud tiles fall back
    /// to SAT and the one-way gate won't compute the slope-aware LastPosition adjustment needed
    /// for uphill walking.
    /// </remarks>
    public SlopeCollisionMode SlopeMode { get; set; } = SlopeCollisionMode.Standard;

    /// <summary>
    /// When set to a value other than <see cref="OneWayDirection.None"/>, this relationship only
    /// resolves overlap when the computed separation pushes the entity in the configured
    /// direction — e.g. <see cref="OneWayDirection.Up"/> creates a jump-through / cloud platform
    /// that blocks downward motion only. The <see cref="CollisionOccurred"/> event does not fire
    /// on skipped pairs. For <see cref="OneWayDirection.Up"/> three gates must pass:
    /// separation has a positive Y component (pushing upward), the entity is moving downward or
    /// stationary (<c>VelocityY &lt;= 0</c>), and the entity's <c>LastPosition</c> was at or
    /// above where it will end up post-separation (i.e. it was cleanly on top last frame, not
    /// peaking inside the tile from below). The separation's X component is zeroed so an entity
    /// clipping a platform's side is lifted up rather than shoved sideways. On sloped tiles the
    /// LastPosition gate is slope-aware — the surface-Y delta between last-frame X and current X
    /// is folded in so uphill walking passes. Player drop-through (Down+Jump, or airborne
    /// Down-held) only bypasses this relationship when <see cref="AllowDropThrough"/> is set to
    /// <c>true</c> — see that property.
    /// </summary>
    /// <remarks>
    /// For sloped cloud platforms (polygon tiles in a <see cref="TileShapeCollection"/>) also set
    /// <see cref="SlopeMode"/> to <see cref="SlopeCollisionMode.PlatformerFloor"/> — otherwise
    /// SAT is used, the surface-Y delta isn't computed, and uphill walking will fall through.
    /// Only <see cref="OneWayDirection.None"/> and <see cref="OneWayDirection.Up"/> are
    /// implemented. Setting Down/Left/Right is allowed but will throw
    /// <see cref="NotImplementedException"/> on the next collision pass.
    /// </remarks>
    public OneWayDirection OneWayDirection { get; set; } = OneWayDirection.None;

    /// <summary>
    /// When <c>true</c>, this relationship is bypassed entirely on any pair where an
    /// <see cref="FlatRedBall2.Movement.IPlatformerEntity"/> side reports
    /// <see cref="FlatRedBall2.Movement.PlatformerBehavior.IsSuppressingOneWayCollision"/> as
    /// true — i.e. the player is dropping through with Down+Jump or holding Down while airborne.
    /// Use for cloud / jump-through platforms (Mario clouds, Sonic-style jump-through floors)
    /// where the player should be able to intentionally fall through.
    /// </summary>
    /// <remarks>
    /// Leave <c>false</c> (default) for hard one-way barriers such as Yoshi's Island ratchet
    /// doors — those should always block in the configured direction regardless of player input.
    /// Independent of <see cref="OneWayDirection"/>: in practice this only has an effect when
    /// <see cref="OneWayDirection"/> is non-<see cref="OneWayDirection.None"/>, but the two are
    /// deliberately orthogonal so future use cases can combine them freely. When
    /// <see cref="SlopeMode"/> is <see cref="SlopeCollisionMode.PlatformerFloor"/>, an active
    /// drop-through also skips ground-snap for this relationship — otherwise the snap raycast
    /// would yank the player back onto the cloud the frame after drop-through begins.
    /// </remarks>
    public bool AllowDropThrough { get; set; } = false;

    /// <summary>
    /// When <c>true</c> and both lists are the same reference (self-collision), fires
    /// <see cref="CollisionOccurred"/> for both orderings of each colliding pair:
    /// once as <c>(a, b)</c> and once as <c>(b, a)</c>. Physics (separation, bounce)
    /// still runs only once per pair. Default is <c>false</c>.
    /// </summary>
    public bool AllowDuplicatePairs { get; set; }

    /// <summary>
    /// Number of deep (narrow-phase) collision checks performed during the last <see cref="RunCollisions"/> call.
    /// Useful for profiling. When both lists come from a <see cref="Factory{T}"/> with a matching
    /// <see cref="Factory{T}.PartitionAxis"/>, this will be much lower than the O(n×m) worst case.
    /// </summary>
    public int DeepCollisionCount { get; private set; }

    /// <summary>
    /// Fired once per colliding pair per frame, after any physics response configured via
    /// <see cref="BounceOnCollision"/>, <see cref="MoveFirstOnCollision"/>, etc.
    /// </summary>
    /// <remarks>
    /// If you need to inspect velocity <em>before</em> and <em>after</em> the bounce (e.g. to scale
    /// a sound effect by impact force), skip the fluent physics methods and apply physics manually
    /// inside this handler instead:
    /// <code>
    /// var rel = AddCollisionRelationship(_balls, _walls); // no BounceOnCollision
    /// rel.CollisionOccurred += (ball, wall) =>
    /// {
    ///     var preVelocity = ball.Velocity;
    ///     var sep = ball.GetSeparationVector(wall);
    ///     ball.ApplySeparationOffset(sep);
    ///     ball.AdjustVelocityFromSeparation(sep, wall, thisMass: 0f, otherMass: 1f, elasticity: 0.8f);
    ///     float impact = (ball.Velocity - preVelocity).Length();
    /// };
    /// </code>
    /// </remarks>
    public event Action<A, B>? CollisionOccurred;

    internal CollisionRelationship(IEnumerable<A> listA, IEnumerable<B> listB)
    {
        _listA = listA;
        _listB = listB;
    }

    public CollisionRelationship<A, B> MoveFirstOnCollision()
    {
        _moveFirst = true; return this;
    }

    public CollisionRelationship<A, B> MoveSecondOnCollision()
    {
        _moveSecond = true; return this;
    }

    public CollisionRelationship<A, B> MoveBothOnCollision(float firstMass = 1f, float secondMass = 1f)
    {
        _moveBoth = true; _bothFirstMass = firstMass; _bothSecondMass = secondMass; return this;
    }

    /// <summary>
    /// Reflects A's velocity off B using impulse physics and separates their positions.
    /// </summary>
    /// <remarks>
    /// Separation applied to A is scaled by mass ratio:
    /// <code>A separation = sep * (secondMass / (firstMass + secondMass))</code>
    ///
    /// Common outcomes:
    /// <list type="bullet">
    /// <item><description><c>firstMass = 0f, secondMass = 1f</c>: A takes full separation, B stays fixed.</description></item>
    /// <item><description><c>firstMass = 1f, secondMass = 1f</c>: separation is shared equally.</description></item>
    /// <item><description><c>firstMass = 1f, secondMass = 0f</c>: A takes zero separation (usually not desired for wall/floor collisions).</description></item>
    /// </list>
    ///
    /// Elasticity controls post-collision speed along the collision normal:
    /// <c>1.0f</c> = perfectly elastic, <c>0f</c> = no bounce, values between 0 and 1 lose energy.
    /// </remarks>
    /// <param name="firstMass">
    /// Mass of A. Pass <c>0f</c> when A should absorb the full separation (e.g., a ball bouncing
    /// off an immovable wall). Higher values relative to <paramref name="secondMass"/> mean A moves less.
    /// </param>
    /// <param name="secondMass">
    /// Mass of B. Pass <c>1f</c> (or any non-zero value) when B is immovable. Pass <c>0f</c> only if
    /// B should absorb all separation instead.
    /// </param>
    /// <param name="elasticity">1.0 = perfectly elastic; &lt;1.0 = energy loss per bounce.</param>
    public CollisionRelationship<A, B> BounceOnCollision(float firstMass = 1f, float secondMass = 1f, float elasticity = 1f)
    {
        _bounce = true; _bounceMassA = firstMass; _bounceMassB = secondMass; _bounceElasticity = elasticity; return this;
    }

    /// <summary>
    /// Restricts collision detection (and physical response) to a specific shape on each A instance
    /// instead of testing all of A's shapes. Does not change which entity receives the response —
    /// separation and velocity adjustments still apply to A, not to the selected child shape.
    /// </summary>
    public CollisionRelationship<A, B> WithFirstShape(Func<A, ICollidable> selector)
    {
        _firstShapeSelector = selector; return this;
    }

    /// <summary>
    /// Restricts collision detection (and physical response) to a specific shape on each B instance
    /// instead of testing all of B's shapes. Does not change which entity receives the response —
    /// separation and velocity adjustments still apply to B, not to the selected child shape.
    /// </summary>
    public CollisionRelationship<A, B> WithSecondShape(Func<B, ICollidable> selector)
    {
        _secondShapeSelector = selector; return this;
    }

    void ICollisionRelationship.RunCollisions() => RunCollisions();

    internal void RunCollisions()
    {
        DeepCollisionCount = 0;

        if (ReferenceEquals(_listA, _listB))
        {
            var axis = (_listA is IFactory fa) ? fa.PartitionAxis : null;
            if (axis != null)
                RunSameListCollisionsSweep(axis.Value);
            else
                RunSameListCollisions();
            return;
        }

        Axis? axisA = (_listA is IFactory fa2) ? fa2.PartitionAxis : null;
        Axis? axisB = (_listB is IFactory fb) ? fb.PartitionAxis : null;
        if (axisA != null && axisA == axisB)
            RunCrossListCollisionsSweep(axisA.Value);
        else
        {
            foreach (var a in _listA)
                foreach (var b in _listB)
                    RunPair(a, b);
        }
    }

    // Called when _listA and _listB are the same reference (self/intra-list collision).
    // Iterates unique unordered pairs (i < j) so each pair is processed exactly once.
    // Uses IReadOnlyList<A> indexed access (e.g. Factory<T>) to avoid GetEnumerator allocation;
    // falls back to foreach for other IEnumerable types.
    // The cast (B)(object)b is safe: A == B is guaranteed when both lists share a reference.
    private void RunSameListCollisions()
    {
        _selfSnapshot ??= new List<A>();
        _selfSnapshot.Clear();

        if (_listA is IReadOnlyList<A> indexed)
            for (int i = 0; i < indexed.Count; i++)
                _selfSnapshot.Add(indexed[i]);
        else
            foreach (var item in _listA)
                _selfSnapshot.Add(item);

        for (int i = 0; i < _selfSnapshot.Count; i++)
        {
            for (int j = i + 1; j < _selfSnapshot.Count; j++)
            {
                var a = _selfSnapshot[i];
                var b = _selfSnapshot[j];
                var effectiveA = GetEffectiveA(a);
                var effectiveB = GetEffectiveB((B)(object)b);
                DeepCollisionCount++;
                if (!CheckCollision(effectiveA, effectiveB)) continue;

                var sep = ComputeSeparationVector(effectiveA, effectiveB);
                if (!TryApplyOneWayGate(a, (B)(object)b, ref sep)) continue;
                ApplyResponse(a, (B)(object)b, sep);
                TryTransferPlatformVelocity(a, (B)(object)b, sep);
                CollisionOccurred?.Invoke(a, (B)(object)b);
                if (AllowDuplicatePairs)
                    CollisionOccurred?.Invoke(b, (B)(object)a);
            }
        }
    }

    private void RunPair(A a, B b)
    {
        var effectiveA = GetEffectiveA(a);
        var effectiveB = GetEffectiveB(b);
        DeepCollisionCount++;
        if (!CheckCollision(effectiveA, effectiveB))
        {
            TryOfferGroundSnap(a, b);
            return;
        }
        var sep = ComputeSeparationVector(effectiveA, effectiveB);
        if (!TryApplyOneWayGate(a, b, ref sep))
        {
            TryOfferGroundSnap(a, b);
            return;
        }
        ApplyResponse(a, b, sep);
        TryTransferPlatformVelocity(a, b, sep);
        CollisionOccurred?.Invoke(a, b);
        TryOfferGroundSnap(a, b);
    }

    // Both lists are already sorted by their respective factories. Uses indexed access where available.
    private void RunCrossListCollisionsSweep(Axis axis)
    {
        var listA = _listA as IReadOnlyList<A> ?? new List<A>(_listA);
        var listB = _listB as IReadOnlyList<B> ?? new List<B>(_listB);

        int startB = 0;

        for (int i = 0; i < listA.Count; i++)
        {
            var a = listA[i];
            var effectiveA = GetEffectiveA(a);
            float aPos = axis == Axis.X ? effectiveA.AbsoluteX : effectiveA.AbsoluteY;
            float aR = effectiveA.BroadPhaseRadius;
            float aLeft = aPos - aR;
            float aRight = aPos + aR;

            // Advance startB past items whose far edge is behind aLeft
            while (startB < listB.Count)
            {
                var testB = GetEffectiveB(listB[startB]);
                float testPos = axis == Axis.X ? testB.AbsoluteX : testB.AbsoluteY;
                if (testPos + testB.BroadPhaseRadius >= aLeft) break;
                startB++;
            }

            for (int j = startB; j < listB.Count; j++)
            {
                var b = listB[j];
                var effectiveB = GetEffectiveB(b);
                float bPos = axis == Axis.X ? effectiveB.AbsoluteX : effectiveB.AbsoluteY;
                float bLeft = bPos - effectiveB.BroadPhaseRadius;
                if (bLeft > aRight) break; // too far; all remaining are also too far

                DeepCollisionCount++;
                if (!CheckCollision(effectiveA, effectiveB))
                {
                    TryOfferGroundSnap(a, b);
                    continue;
                }
                var sep = ComputeSeparationVector(effectiveA, effectiveB);
                if (!TryApplyOneWayGate(a, b, ref sep))
                {
                    TryOfferGroundSnap(a, b);
                    continue;
                }
                ApplyResponse(a, b, sep);
                TryTransferPlatformVelocity(a, b, sep);
                CollisionOccurred?.Invoke(a, b);
                TryOfferGroundSnap(a, b);
            }
        }
    }

    // List is already sorted by factory. Iterates unique unordered pairs (i < j).
    private void RunSameListCollisionsSweep(Axis axis)
    {
        var list = _listA as IReadOnlyList<A> ?? new List<A>(_listA);

        for (int i = 0; i < list.Count; i++)
        {
            var a = list[i];
            var effectiveA = GetEffectiveA(a);
            float aPos = axis == Axis.X ? effectiveA.AbsoluteX : effectiveA.AbsoluteY;
            float aRight = aPos + effectiveA.BroadPhaseRadius;

            for (int j = i + 1; j < list.Count; j++)
            {
                var b = list[j];
                var effectiveB = GetEffectiveB((B)(object)b);
                float bPos = axis == Axis.X ? effectiveB.AbsoluteX : effectiveB.AbsoluteY;
                float bLeft = bPos - effectiveB.BroadPhaseRadius;
                if (bLeft > aRight) break;

                DeepCollisionCount++;
                if (!CheckCollision(effectiveA, effectiveB)) continue;

                var sep = ComputeSeparationVector(effectiveA, effectiveB);
                if (!TryApplyOneWayGate(a, (B)(object)b, ref sep)) continue;
                ApplyResponse(a, (B)(object)b, sep);
                TryTransferPlatformVelocity(a, (B)(object)b, sep);
                CollisionOccurred?.Invoke(a, (B)(object)b);
                if (AllowDuplicatePairs)
                    CollisionOccurred?.Invoke(b, (B)(object)a);
            }
        }
    }

    // Offers this relationship's TileShapeCollection to any IPlatformerEntity side as a
    // ground-snap candidate. Only fires when SlopeMode == PlatformerFloor. No-op otherwise.
    private void TryOfferGroundSnap(A a, B b)
    {
        if (SlopeMode != SlopeCollisionMode.PlatformerFloor) return;

        // Cloud / jump-through platforms: if the player is actively dropping through, suppress
        // snap too — otherwise ConsiderSnappingTo would raycast down and yank the player back
        // onto the cloud the frame after drop-through begins. Flat clouds don't hit this (they
        // use SlopeMode.Standard), but sloped clouds require PlatformerFloor.
        if (AllowDropThrough && OneWayDirection != OneWayDirection.None)
        {
            if (IsSuppressingDropThrough(a) || IsSuppressingDropThrough(b)) return;
        }

        if (a is IPlatformerEntity pa && pa is Entity ea && b is TileShapeCollection tscB)
        {
            pa.Platformer.ConsiderSnappingTo(ea, tscB);
            pa.Platformer.ContributeSlopeProbe(ea, tscB);
        }
        else if (b is IPlatformerEntity pb && pb is Entity eb && a is TileShapeCollection tscA)
        {
            pb.Platformer.ConsiderSnappingTo(eb, tscA);
            pb.Platformer.ContributeSlopeProbe(eb, tscA);
        }
    }

    private static bool IsSuppressingDropThrough(object side) =>
        side is Movement.IPlatformerEntity p && p.Platformer.IsSuppressingOneWayCollision;

    private ICollidable GetEffectiveA(A a) => _firstShapeSelector != null ? _firstShapeSelector(a) : a;
    private ICollidable GetEffectiveB(B b) => _secondShapeSelector != null ? _secondShapeSelector(b) : b;

    // Checks collision using CollisionDispatcher.CollidesWith so Line intersections are handled.
    // Iterates leaf shape pairs so any combination of leaf shapes, entities, and
    // TileShapeCollections is dispatched correctly — including selected-shape vs entity cases.
    private bool CheckCollision(ICollidable a, ICollidable b)
    {
        if (b is TileShapeCollection tsc)
        {
            foreach (var leafA in Entity.GetLeafShapes(a))
                if (tsc.GetSeparationFor(leafA, SlopeMode) != Vector2.Zero)
                    return true;
            return false;
        }

        foreach (var leafA in Entity.GetLeafShapes(a))
            foreach (var leafB in Entity.GetLeafShapes(b))
                if (CollisionDispatcher.CollidesWith(leafA, leafB))
                    return true;
        return false;
    }

    // Returns the separation vector to push 'a' out of 'b'. Returns Vector2.Zero when no
    // physics separation is meaningful (e.g., Lines are infinitely thin).
    private Vector2 ComputeSeparationVector(ICollidable a, ICollidable b)
    {
        if (b is TileShapeCollection tsc)
        {
            foreach (var leafA in Entity.GetLeafShapes(a))
            {
                var sep = tsc.GetSeparationFor(leafA, SlopeMode);
                if (sep != Vector2.Zero) return sep;
            }
            return Vector2.Zero;
        }

        foreach (var leafA in Entity.GetLeafShapes(a))
            foreach (var leafB in Entity.GetLeafShapes(b))
            {
                var sep = CollisionDispatcher.GetSeparationVector(leafA, leafB);
                if (sep != Vector2.Zero) return sep;
            }
        return Vector2.Zero;
    }

    // Applies the one-way gate: returns false when this pair should be skipped entirely
    // (no separation, no event). When returning true, may rewrite 'sep' (e.g. zero X for Up)
    // before the caller applies physics. Throws on unimplemented directions.
    private bool TryApplyOneWayGate(A a, B b, ref Vector2 sep)
    {
        if (OneWayDirection == OneWayDirection.None) return true;

        // Drop-through suppression only applies when this relationship opts in — hard one-way
        // barriers (AllowDropThrough = false) ignore the player's drop-through state.
        if (AllowDropThrough && (IsSuppressingDropThrough(a) || IsSuppressingDropThrough(b)))
            return false;

        switch (OneWayDirection)
        {
            case OneWayDirection.Up:
                if (sep.Y <= 0f) return false;
                if (a is Entity ea)
                {
                    // Velocity gate: an upward-moving entity overlapping deeply enough that SAT
                    // picks the upward exit would otherwise be popped onto the top — wrong for
                    // jump-through platforms. Require the entity to be moving downward (or stationary).
                    if (ea.VelocityY > 0f) return false;
                    // Positional gate: the entity must have been at or above the post-separation
                    // Y last frame — i.e. it was cleanly on top of the tile and physics moved it
                    // into a small overlap. An entity that jumped up into the tile from below and
                    // is now starting to fall would otherwise pass the velocity gate but get
                    // popped onto the top despite never having reached it.
                    //
                    // Slope-aware: on a cloud slope, the surface Y at LastPosition.X differs from
                    // the surface at the current X. Fold that delta in so uphill walking on a
                    // sloped one-way tile isn't rejected every frame. When the B side is a
                    // TileShapeCollection we can query it; otherwise fall back to the flat check.
                    const float epsilon = 0.001f;
                    float surfaceDelta = 0f;
                    if (b is TileShapeCollection tscGate)
                    {
                        float? lastSurface = tscGate.GetHeightmapSurfaceYAt(ea.LastPosition.X);
                        float? thisSurface = tscGate.GetHeightmapSurfaceYAt(ea.Position.X);
                        if (lastSurface.HasValue && thisSurface.HasValue)
                            surfaceDelta = lastSurface.Value - thisSurface.Value;
                    }
                    if (ea.LastPosition.Y < ea.Position.Y + sep.Y + surfaceDelta - epsilon) return false;
                }
                sep = new Vector2(0f, sep.Y);
                return true;
            case OneWayDirection.Down:
            case OneWayDirection.Left:
            case OneWayDirection.Right:
                throw new NotImplementedException(
                    $"OneWayDirection.{OneWayDirection} is not yet implemented — see design/TODOS.md");
            default:
                return true;
        }
    }

    // Moving-platform support: when a platformer entity lands on top of another Entity, feed
    // that entity's horizontal velocity into the platformer behavior so it rides the platform
    // and inherits its momentum on jump. The "landed on top" check uses the sign of the side's
    // separation Y (positive = pushed up). Tile collections are excluded — tiles don't have a
    // meaningful VelocityX and game code shouldn't author moving level geometry through them.
    private static void TryTransferPlatformVelocity(A a, B b, Vector2 sep)
    {
        // A is the platformer, A was pushed up by sep → sep.Y > 0
        if (sep.Y > 0f && a is IPlatformerEntity pa && b is Entity entB && b is not TileShapeCollection)
        {
            pa.Platformer.ContributeGroundVelocity(entB.VelocityX);
        }
        // B is the platformer, B was pushed by -sep → B pushed up when sep.Y < 0
        else if (sep.Y < 0f && b is IPlatformerEntity pb && a is Entity entA && a is not TileShapeCollection)
        {
            pb.Platformer.ContributeGroundVelocity(entA.VelocityX);
        }
    }

    private void ApplyResponse(A a, B b, Vector2 sep)
    {
        if (_moveFirst)
            a.ApplySeparationOffset(CollisionDispatcher.ComputeSeparationOffset(sep, 0f, 1f));
        if (_moveSecond)
            b.ApplySeparationOffset(CollisionDispatcher.ComputeSeparationOffset(-sep, 0f, 1f));
        if (_moveBoth)
        {
            a.ApplySeparationOffset(CollisionDispatcher.ComputeSeparationOffset(sep, _bothFirstMass, _bothSecondMass));
            b.ApplySeparationOffset(CollisionDispatcher.ComputeSeparationOffset(-sep, _bothSecondMass, _bothFirstMass));
        }
        if (_bounce)
        {
            // Pass entity b so velocity exchange happens between entities, not selected child shapes.
            a.AdjustVelocityFromSeparation(sep, b, _bounceMassA, _bounceMassB, _bounceElasticity);
            a.ApplySeparationOffset(CollisionDispatcher.ComputeSeparationOffset(sep, _bounceMassA, _bounceMassB));
        }
    }
}
