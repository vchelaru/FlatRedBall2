using System.Collections;
using System.Collections.Generic;

namespace FlatRedBall2;

/// <summary>Non-generic interface used by <see cref="FlatRedBallService"/> to destroy all factory instances on screen exit.</summary>
internal interface IFactory
{
    void DestroyAll();
}

/// <summary>
/// Creates, tracks, and destroys entities of type <typeparamref name="T"/> for a single screen.
/// </summary>
/// <remarks>
/// <para>
/// <c>Factory&lt;T&gt;</c> is the standard way to create entities — use it even when you only need
/// one instance. It registers the entity with the engine, wires up the activity loop, and ensures
/// automatic cleanup when the screen exits.
/// </para>
/// <para>
/// Declare one factory per entity type as a field on your <see cref="Screen"/>, construct it in
/// <see cref="Screen.CustomInitialize"/>, and call <see cref="Create"/> to spawn instances:
/// <code>
/// private Factory&lt;Player&gt; _playerFactory = null!;
///
/// public override void CustomInitialize()
/// {
///     _playerFactory = new Factory&lt;Player&gt;(this);
///     var player = _playerFactory.Create();
/// }
/// </code>
/// </para>
/// </remarks>
public class Factory<T> : IEnumerable<T>, IReadOnlyList<T>, IFactory where T : Entity, new()
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
