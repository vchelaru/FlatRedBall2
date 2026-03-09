using System;
using System.Collections.Generic;
using System.Numerics;

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
    /// When <c>true</c> and both lists are the same reference (self-collision), fires
    /// <see cref="CollisionOccurred"/> for both orderings of each colliding pair:
    /// once as <c>(a, b)</c> and once as <c>(b, a)</c>. Physics (separation, bounce)
    /// still runs only once per pair. Default is <c>false</c>.
    /// </summary>
    public bool AllowDuplicatePairs { get; set; }

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
        if (ReferenceEquals(_listA, _listB))
        {
            RunSameListCollisions();
            return;
        }

        foreach (var a in _listA)
            foreach (var b in _listB)
                RunPair(a, b);
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
        if (!CheckCollision(effectiveA, effectiveB)) return;
        var sep = ComputeSeparationVector(effectiveA, effectiveB);
        ApplyResponse(a, b, sep);
        CollisionOccurred?.Invoke(a, b);
    }

    private ICollidable GetEffectiveA(A a) => _firstShapeSelector != null ? _firstShapeSelector(a) : a;
    private ICollidable GetEffectiveB(B b) => _secondShapeSelector != null ? _secondShapeSelector(b) : b;

    // Checks collision using CollisionDispatcher.CollidesWith so Line intersections are handled.
    // Iterates leaf shape pairs so any combination of leaf shapes, entities, and
    // TileShapeCollections is dispatched correctly — including selected-shape vs entity cases.
    private static bool CheckCollision(ICollidable a, ICollidable b)
    {
        if (b is TileShapeCollection tsc)
        {
            foreach (var leafA in Entity.GetLeafShapes(a))
                if (tsc.GetSeparationFor(leafA) != Vector2.Zero)
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
    private static Vector2 ComputeSeparationVector(ICollidable a, ICollidable b)
    {
        if (b is TileShapeCollection tsc)
        {
            foreach (var leafA in Entity.GetLeafShapes(a))
            {
                var sep = tsc.GetSeparationFor(leafA);
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
