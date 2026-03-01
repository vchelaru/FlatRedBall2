using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using FlatRedBall2.Collision;
using FlatRedBall2.Diagnostics;
using FlatRedBall2.Rendering;

namespace FlatRedBall2;

public class Screen
{
    private readonly List<Entity> _entities = new();
    private readonly List<ICollisionRelationship> _collisionRelationships = new();

    public Camera Camera { get; } = new Camera();
    public ContentManagerService ContentManager { get; } = new ContentManagerService();
    public FlatRedBallService Engine { get; internal set; } = null!;

    public List<Layer> Layers { get; } = new();
    public List<IRenderable> RenderList { get; } = new();

    // Manual entity registration (for entities not created via Factory)
    public void Register(Entity entity)
    {
        entity.Engine = Engine;
        _entities.Add(entity);
        foreach (var child in entity.Children)
        {
            if (child is IRenderable renderable)
                RenderList.Add(renderable);
        }
    }

    // Lifecycle
    public virtual void CustomInitialize() { }
    public virtual void CustomActivity(FrameTime time) { }
    public virtual void CustomDestroy() { }

    // Navigation
    public void MoveToScreen<T>() where T : Screen, new()
        => Engine.RequestScreenChange<T>();

    // Collision relationship overloads
    public CollisionRelationship<A, B> AddCollisionRelationship<A, B>(
        IEnumerable<A> listA, IEnumerable<B> listB)
        where A : ICollidable
        where B : ICollidable
    {
        var rel = new CollisionRelationship<A, B>(listA, listB);
        _collisionRelationships.Add(rel);
        return rel;
    }

    public CollisionRelationship<A, B> AddCollisionRelationship<A, B>(
        A single, IEnumerable<B> list)
        where A : ICollidable
        where B : ICollidable
    {
        var rel = new CollisionRelationship<A, B>(SingleEnumerable(single), list);
        _collisionRelationships.Add(rel);
        return rel;
    }

    public CollisionRelationship<A, ShapeCollection> AddCollisionRelationship<A>(
        IEnumerable<A> entities, ShapeCollection staticGeometry)
        where A : ICollidable
    {
        var rel = new CollisionRelationship<A, ShapeCollection>(entities, SingleEnumerable(staticGeometry));
        _collisionRelationships.Add(rel);
        return rel;
    }

    // Internal update — called by FlatRedBallService
    internal void Update(FrameTime frameTime)
    {
        // 1. Physics pass
        foreach (var entity in _entities)
            entity.PhysicsUpdate(frameTime);

        Camera.PhysicsUpdate(frameTime.DeltaSeconds);

        // 2. Collision phase
        foreach (var rel in _collisionRelationships)
            rel.RunCollisions();

        // TODO: flush async sync context

        // 3. CustomActivity
        CustomActivity(frameTime);
        foreach (var entity in new List<Entity>(_entities))
            entity.CustomActivity(frameTime);
    }

    // Internal draw — called by FlatRedBallService
    internal void Draw(SpriteBatch spriteBatch, RenderDiagnostics diagnostics)
    {
        spriteBatch.GraphicsDevice.Clear(Camera.BackgroundColor);

        SortRenderList();

        IRenderBatch? currentBatch = null;
        IRenderable? previousRenderable = null;

        foreach (var renderable in RenderList)
        {
            var batch = renderable.Batch;
            if (batch != currentBatch)
            {
                if (diagnostics.IsEnabled && currentBatch != null)
                {
                    diagnostics.RecordBreak(currentBatch, batch, renderable.Layer, renderable.Z,
                        previousRenderable?.Name ?? string.Empty, renderable.Name ?? string.Empty);
                }
                currentBatch?.End(spriteBatch);
                batch.Begin(spriteBatch, Camera);
                currentBatch = batch;
            }

            renderable.Draw(spriteBatch, Camera);
            previousRenderable = renderable;
        }

        currentBatch?.End(spriteBatch);
    }

    // Internal entity registration used by Factory
    internal void AddEntity(Entity entity) => _entities.Add(entity);
    internal void RemoveEntity(Entity entity) => _entities.Remove(entity);

    private void SortRenderList()
    {
        // Insertion sort — O(N) for nearly-sorted data; stable
        for (int i = 1; i < RenderList.Count; i++)
        {
            var item = RenderList[i];
            int j = i - 1;
            while (j >= 0 && Compare(RenderList[j], item) > 0)
            {
                RenderList[j + 1] = RenderList[j];
                j--;
            }
            RenderList[j + 1] = item;
        }
    }

    private int Compare(IRenderable a, IRenderable b)
    {
        int layerA = a.Layer != null ? Layers.IndexOf(a.Layer) : -1;
        int layerB = b.Layer != null ? Layers.IndexOf(b.Layer) : -1;
        if (layerA == -1) layerA = int.MaxValue;
        if (layerB == -1) layerB = int.MaxValue;
        if (layerA != layerB) return layerA.CompareTo(layerB);
        return a.Z.CompareTo(b.Z);
    }

    private static IEnumerable<T> SingleEnumerable<T>(T item)
    {
        yield return item;
    }
}
