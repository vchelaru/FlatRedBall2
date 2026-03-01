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
    private float _firstMass = 1f;
    private float _secondMass = 0f;

    private bool _moveSecond;
    private bool _moveBoth;

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

    public CollisionRelationship<A, B> MoveFirstOnCollision(float firstMass = 1f, float secondMass = 0f)
    {
        _moveFirst = true; _firstMass = firstMass; _secondMass = secondMass; return this;
    }

    public CollisionRelationship<A, B> MoveSecondOnCollision(float firstMass = 0f, float secondMass = 1f)
    {
        _moveSecond = true; _firstMass = firstMass; _secondMass = secondMass; return this;
    }

    public CollisionRelationship<A, B> MoveBothOnCollision(float firstMass = 1f, float secondMass = 1f)
    {
        _moveBoth = true; _firstMass = firstMass; _secondMass = secondMass; return this;
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

                if (_moveFirst || _moveSecond || _moveBoth)
                    a.SeparateFrom(b, _firstMass, _secondMass);

                if (_bounce)
                    a.AdjustVelocityFrom(b, _bounceMassA, _bounceMassB, _bounceElasticity);

                CollisionOccurred?.Invoke(a, b);
            }
        }
    }
}
