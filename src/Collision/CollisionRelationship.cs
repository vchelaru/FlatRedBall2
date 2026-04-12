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
    /// </remarks>
    public SlopeCollisionMode SlopeMode { get; set; } = SlopeCollisionMode.Standard;

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
                ApplyResponse(a, (B)(object)b, sep);
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
        ApplyResponse(a, b, sep);
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
                ApplyResponse(a, b, sep);
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
                ApplyResponse(a, (B)(object)b, sep);
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

        if (a is IPlatformerEntity pa && pa is Entity ea && b is TileShapeCollection tscB)
            pa.Platformer.ConsiderSnappingTo(ea, tscB);
        else if (b is IPlatformerEntity pb && pb is Entity eb && a is TileShapeCollection tscA)
            pb.Platformer.ConsiderSnappingTo(eb, tscA);
    }

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
