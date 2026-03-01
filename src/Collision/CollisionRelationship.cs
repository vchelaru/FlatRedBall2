using System;
using System.Collections.Generic;

namespace FlatRedBall2.Collision;

public class CollisionRelationship<A, B> : ICollisionRelationship
    where A : ICollidable
    where B : ICollidable
{
    private readonly IEnumerable<A> _listA;
    private readonly IEnumerable<B> _listB;

    private bool _moveFirst;
    private bool _moveSecond;
    private bool _moveBoth;
    private float _bothFirstMass = 1f;
    private float _bothSecondMass = 1f;

    private bool _bounce;
    private float _bounceMassA = 1f;
    private float _bounceMassB = 1f;
    private float _bounceElasticity = 1f;

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

    public CollisionRelationship<A, B> BounceOnCollision(float firstMass = 1f, float secondMass = 1f, float elasticity = 1f)
    {
        _bounce = true; _bounceMassA = firstMass; _bounceMassB = secondMass; _bounceElasticity = elasticity; return this;
    }

    void ICollisionRelationship.RunCollisions() => RunCollisions();

    internal void RunCollisions()
    {
        foreach (var a in _listA)
        {
            foreach (var b in _listB)
            {
                if (!a.CollidesWith(b)) continue;

                if (_moveFirst)
                    a.SeparateFrom(b, 0f, 1f);
                if (_moveSecond)
                    b.SeparateFrom(a, 0f, 1f);
                if (_moveBoth)
                {
                    a.SeparateFrom(b, _bothFirstMass, _bothSecondMass);
                    b.SeparateFrom(a, _bothSecondMass, _bothFirstMass);
                }

                if (_bounce)
                {
                    a.AdjustVelocityFrom(b, _bounceMassA, _bounceMassB, _bounceElasticity);
                    a.SeparateFrom(b, _bounceMassA, _bounceMassB);
                }

                CollisionOccurred?.Invoke(a, b);
            }
        }
    }
}
