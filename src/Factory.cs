using System.Collections;
using System.Collections.Generic;

namespace FlatRedBall2;

public class Factory<T> : IEnumerable<T>, IReadOnlyList<T> where T : Entity, new()
{
    private readonly Screen _screen;
    private readonly List<T> _instances = new();

    public Factory(Screen screen)
    {
        _screen = screen;
        screen.Engine.RegisterFactory(this);
    }

    public IReadOnlyList<T> Instances => _instances;

    // IReadOnlyList<T> — allows SelfCollisionRelationship to iterate by index without GetEnumerator.
    public int Count => _instances.Count;
    public T this[int index] => _instances[index];

    public T Create()
    {
        var entity = new T();
        entity.Engine = _screen.Engine;
        _screen.AddEntity(entity);
        _instances.Add(entity);
        entity._onDestroy = () =>
        {
            _instances.Remove(entity);
            _screen.RemoveEntity(entity);
        };
        entity.CustomInitialize();
        return entity;
    }

    /// <summary>Destroys the entity. Equivalent to calling <see cref="Entity.Destroy"/> directly.</summary>
    public void Destroy(T instance) => instance.Destroy();

    public void DestroyAll()
    {
        foreach (var instance in new List<T>(_instances))
            Destroy(instance);
    }

    /// <summary>
    /// Enumerates a snapshot of current instances. Safe to call <see cref="Destroy"/> on any
    /// instance during enumeration — the live list can be modified without affecting the iterator.
    /// </summary>
    public IEnumerator<T> GetEnumerator() => new List<T>(_instances).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
