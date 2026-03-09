using System.Collections;
using System.Collections.Generic;

namespace FlatRedBall2;

/// <summary>Non-generic interface used by <see cref="FlatRedBallService"/> to destroy all factory instances on screen exit.</summary>
internal interface IFactory
{
    void DestroyAll();
    Axis? PartitionAxis { get; }
    void SortForPartition();
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

    /// <summary>
    /// When set, this factory's entity list is sorted along the chosen axis once per frame before
    /// collision relationships run. Any <see cref="Collision.CollisionRelationship{A,B}"/> whose both
    /// lists are factories sharing the same non-null <see cref="PartitionAxis"/> will automatically use
    /// broad-phase culling — no extra setup needed.
    /// Set to <c>null</c> (default) to disable sorting and broad-phase for this factory.
    /// </summary>
    public Axis? PartitionAxis { get; set; }

    void IFactory.SortForPartition()
    {
        if (PartitionAxis == null) return;
        bool byX = PartitionAxis == Axis.X;
        // Insertion sort — O(n) on nearly-sorted data (entities move slowly relative to sort order).
        for (int i = 1; i < _instances.Count; i++)
        {
            var key = _instances[i];
            float keyVal = byX ? key.AbsoluteX : key.AbsoluteY;
            int j = i - 1;
            while (j >= 0)
            {
                float jVal = byX ? _instances[j].AbsoluteX : _instances[j].AbsoluteY;
                if (jVal <= keyVal) break;
                _instances[j + 1] = _instances[j];
                j--;
            }
            _instances[j + 1] = key;
        }
    }

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
