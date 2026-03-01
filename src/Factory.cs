using System.Collections;
using System.Collections.Generic;

namespace FlatRedBall2;

public class Factory<T> : IEnumerable<T> where T : Entity, new()
{
    private readonly Screen _screen;
    private readonly List<T> _instances = new();

    public Factory(Screen screen)
    {
        _screen = screen;
        screen.Engine.RegisterFactory(this);
    }

    public IReadOnlyList<T> Instances => _instances;

    public T Create()
    {
        var entity = new T();
        entity.Engine = _screen.Engine;
        _screen.AddEntity(entity);
        _instances.Add(entity);
        entity.CustomInitialize();
        return entity;
    }

    public void Destroy(T instance)
    {
        _instances.Remove(instance);
        _screen.RemoveEntity(instance);
        instance.Destroy();
    }

    public void DestroyAll()
    {
        foreach (var instance in new List<T>(_instances))
            Destroy(instance);
    }

    public IEnumerator<T> GetEnumerator() => _instances.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
