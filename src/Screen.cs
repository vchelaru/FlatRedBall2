using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Xna.Framework.Graphics;
using FlatRedBall2.Collision;
using FlatRedBall2.Diagnostics;
using FlatRedBall2.UI;
using FlatRedBall2.Rendering;
using Gum.Forms.Controls;
using Gum.Wireframe;

namespace FlatRedBall2;

public class Screen
{
    private readonly List<Entity> _entities = new();
    private readonly List<ICollisionRelationship> _collisionRelationships = new();
    private readonly List<GumRenderable> _gumRenderables = new();

    internal readonly CancellationTokenSource _cts = new();

    /// <summary>
    /// A <see cref="CancellationToken"/> that is cancelled automatically when this screen is destroyed
    /// (i.e., when <see cref="MoveToScreen{T}"/> is called). Pass this token to
    /// <see cref="TimeManager.DelaySeconds"/>, <see cref="TimeManager.DelayUntil"/>, or any other
    /// async API to ensure tasks are silently cancelled on screen transition rather than running
    /// against the new screen.
    /// </summary>
    public CancellationToken Token => _cts.Token;

    public Camera Camera { get; } = new Camera();
    public ContentManagerService ContentManager { get; } = new ContentManagerService();
    public FlatRedBallService Engine { get; internal set; } = null!;

    public List<Layer> Layers { get; } = new();
    public List<IRenderable> RenderList { get; } = new();

    // Manual entity registration (for entities not created via Factory)
    public void Register(Entity entity)
    {
        entity.Engine = Engine;
        entity._onDestroy = () => RemoveEntity(entity);
        _entities.Add(entity);
        foreach (var child in entity.Children)
        {
            if (child is IRenderable renderable)
                RenderList.Add(renderable);
        }
    }

    // Gum integration
    private readonly Dictionary<GraphicalUiElement, GumRenderable> _gumByVisual = new();

    /// <summary>
    /// Adds a Gum Forms control to this screen. Registered for rendering and input updates.
    /// </summary>
    /// <param name="z">Draw order relative to other Gum elements and world objects on the same Layer.</param>
    public void AddGum(FrameworkElement element, float z = 0f)
        => AddGumVisual(element.Visual, z);

    /// <summary>
    /// Adds a Gum visual element to this screen. Registered for rendering and input updates.
    /// Prefer <see cref="AddGum(FrameworkElement, float)"/> when a Forms control is available.
    /// </summary>
    /// <param name="z">Draw order relative to other Gum elements and world objects on the same Layer.</param>
    public void AddGum(GraphicalUiElement visual, float z = 0f)
        => AddGumVisual(visual, z);

    /// <summary>Removes a Gum element previously added with <see cref="AddGum(FrameworkElement, float)"/>.</summary>
    public void RemoveGum(FrameworkElement element)
        => RemoveGumVisual(element.Visual);

    /// <summary>Removes a Gum visual previously added with <see cref="AddGum(GraphicalUiElement, float)"/>.</summary>
    public void RemoveGum(GraphicalUiElement visual)
        => RemoveGumVisual(visual);

    internal void AddGumForEntity(GraphicalUiElement visual, Entity worldParent, float z)
    {
        var renderable = new GumRenderable(visual) { Z = z, WorldParent = worldParent };
        _gumRenderables.Add(renderable);
        _gumByVisual[visual] = renderable;
        RenderList.Add(renderable);
    }

    private void AddGumVisual(GraphicalUiElement visual, float z)
    {
        var renderable = new GumRenderable(visual) { Z = z };
        _gumRenderables.Add(renderable);
        _gumByVisual[visual] = renderable;
        RenderList.Add(renderable);
    }

    private void RemoveGumVisual(GraphicalUiElement visual)
    {
        if (_gumByVisual.TryGetValue(visual, out var renderable))
        {
            _gumRenderables.Remove(renderable);
            _gumByVisual.Remove(visual);
            RenderList.Remove(renderable);
        }
    }

    /// <summary>Gum visuals that need per-frame input updates. Used by FlatRedBallService.</summary>
    internal IReadOnlyList<GumRenderable> GumRenderables => _gumRenderables;

    // Lifecycle
    public virtual void CustomInitialize() { }
    public virtual void CustomActivity(FrameTime time) { }
    public virtual void CustomDestroy() { }

    // Navigation

    /// <param name="configure">
    /// Optional callback invoked on the new screen instance before <see cref="CustomInitialize"/> runs.
    /// Use this to set public properties that <c>CustomInitialize</c> depends on.
    /// </param>
    public void MoveToScreen<T>(Action<T>? configure = null) where T : Screen, new()
        => Engine.RequestScreenChange(configure);

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

    /// <summary>
    /// Registers a self-collision check: every unordered pair within <paramref name="list"/>
    /// is tested each frame. Equivalent to passing the same list for both arguments, but
    /// clearer at the call site.
    /// </summary>
    public CollisionRelationship<A, A> AddCollisionRelationship<A>(IEnumerable<A> list)
        where A : ICollidable
    {
        var rel = new CollisionRelationship<A, A>(list, list);
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

        // 3. Entity CustomActivity — runs first (context-free; works regardless of screen)
        foreach (var entity in new List<Entity>(_entities))
            entity.CustomActivity(frameTime);

        // 4. Screen CustomActivity — runs after entities, so it can react to their updated state
        CustomActivity(frameTime);
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
