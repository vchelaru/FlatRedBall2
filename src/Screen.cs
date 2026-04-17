using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Xna.Framework.Graphics;
using FlatRedBall2.Collision;
using FlatRedBall2.Diagnostics;
using FlatRedBall2.Tiled;
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

    /// <summary>
    /// Immediate-mode visual overlay for this screen. Call draw methods each frame — objects
    /// appear for one frame and are hidden automatically the next. Resets on screen transition.
    /// </summary>
    public Overlay Overlay { get; }

    public Screen() => Overlay = new Overlay(this);

    public List<Layer> Layers { get; } = new();

    /// <summary>
    /// Controls how renderables are ordered before drawing each frame.
    /// Defaults to <see cref="Rendering.SortMode.Z"/>.
    /// Set to <see cref="Rendering.SortMode.ZSecondaryParentY"/> for top-down games
    /// where entities at lower world-space Y should appear in front of entities at higher Y.
    /// </summary>
    public Rendering.SortMode SortMode { get; set; } = Rendering.SortMode.Z;

    private Layer? _layer;

    /// <summary>
    /// Default rendering layer for this screen. Setting this propagates to all existing
    /// entities, renderables, and Gum elements. New objects added after this is set
    /// inherit the layer automatically.
    /// </summary>
    public Layer? Layer
    {
        get => _layer;
        set
        {
            _layer = value;
            foreach (var entity in _entities)
                entity.Layer = value;
            foreach (var renderable in _renderList)
                renderable.Layer = value;
            foreach (var gum in _gumRenderables)
                gum.Layer = value;
        }
    }

    private readonly List<IRenderable> _renderList = new();
    public IReadOnlyList<IRenderable> RenderList => _renderList;

    public void Add(IRenderable renderable, Layer? layer = null)
    {
        if (layer != null || Layer != null)
            renderable.Layer = layer ?? Layer;
        _renderList.Add(renderable);
    }

    public void Remove(IRenderable renderable) => _renderList.Remove(renderable);

    /// <summary>
    /// Registers all tiles in <paramref name="tiles"/> for rendering and wires up future
    /// <see cref="Collision.TileShapeCollection.AddTileAtCell"/> /
    /// <see cref="Collision.TileShapeCollection.RemoveTileAtCell"/> /
    /// <see cref="Collision.TileShapeCollection.AddPolygonTileAtCell"/> /
    /// <see cref="Collision.TileShapeCollection.RemovePolygonTileAtCell"/>
    /// calls so newly added or removed tiles stay in sync automatically.
    /// </summary>
    public void Add(Collision.TileShapeCollection tiles, Layer? layer = null)
    {
        if (layer != null || Layer != null)
            tiles.Layer = layer ?? Layer;
        foreach (var rect in tiles.AllTiles)
            _renderList.Add(rect);
        tiles._onTileAdded += _renderList.Add;
        tiles._onTileRemoved += r => _renderList.Remove(r);
    }

    /// <summary>
    /// Registers a manually-created entity with this screen for physics, activity, and lifecycle management.
    /// <para><b>Only call this for entities you instantiated with <c>new</c>.</b> Entities created via
    /// <c>Factory&lt;T&gt;.Create()</c> are registered automatically — calling <c>Register</c> on them
    /// would add them to the update loop twice.</para>
    /// </summary>
    public void Register(Entity entity)
    {
        entity.Engine = Engine;
        entity._onDestroy = () => RemoveEntity(entity);
        _entities.Add(entity);
        foreach (var child in entity.Children)
        {
            if (child is IRenderable renderable)
                _renderList.Add(renderable);
        }
    }

    // Gum integration
    private readonly Dictionary<GraphicalUiElement, GumRenderable> _gumByVisual = new();

    /// <summary>
    /// Adds all visual layers of a <see cref="TileMap"/> to this screen's render list.
    /// Individual layer Z values and visibility are respected.
    /// </summary>
    public void Add(TileMap map, Layer? layer = null)
    {
        foreach (var mapLayer in map.Layers)
            Add(mapLayer, layer);
    }

    /// <summary>
    /// Adds a single <see cref="TileMapLayer"/> to this screen's render list.
    /// Use this instead of <see cref="Add(TileMap, Layer?)"/> when you need per-layer control
    /// (e.g., assigning different layers to different FRB rendering layers).
    /// </summary>
    public void Add(TileMapLayer mapLayer, Layer? layer = null)
    {
        mapLayer.Renderable.Layer = layer ?? Layer;
        _renderList.Add(mapLayer.Renderable);
    }

    /// <summary>
    /// Adds a Gum Forms control to this screen. Registered for rendering and input updates.
    /// </summary>
    public void Add(FrameworkElement element, Layer? layer = null)
        => AddGumVisual(element.Visual, layer ?? Layer);

    /// <summary>
    /// Adds a Gum visual element to this screen. Registered for rendering and input updates.
    /// Prefer <see cref="Add(FrameworkElement, Layer?)"/> when a Forms control is available.
    /// </summary>
    public void Add(GraphicalUiElement visual, Layer? layer = null)
        => AddGumVisual(visual, layer ?? Layer);

    /// <summary>Removes a Gum element previously added with <see cref="Add(FrameworkElement, Layer?)"/>.</summary>
    public void Remove(FrameworkElement element)
        => RemoveGumVisual(element.Visual);

    /// <summary>Removes a Gum visual previously added with <see cref="Add(GraphicalUiElement, Layer?)"/>.</summary>
    public void Remove(GraphicalUiElement visual)
        => RemoveGumVisual(visual);

    internal void AddGumForEntity(GraphicalUiElement visual, Entity worldParent, Layer? layer)
    {
        var renderable = new GumRenderable(visual) { Parent = worldParent, Layer = layer };
        _gumRenderables.Add(renderable);
        _gumByVisual[visual] = renderable;
        _renderList.Add(renderable);
    }

    private void AddGumVisual(GraphicalUiElement visual, Layer? layer)
    {
        var renderable = new GumRenderable(visual) { Layer = layer };
        _gumRenderables.Add(renderable);
        _gumByVisual[visual] = renderable;
        _renderList.Add(renderable);
    }

    private void RemoveGumVisual(GraphicalUiElement visual)
    {
        if (_gumByVisual.TryGetValue(visual, out var renderable))
        {
            _gumRenderables.Remove(renderable);
            _gumByVisual.Remove(visual);
            _renderList.Remove(renderable);
        }
    }

    /// <summary>Gum visuals that need per-frame input updates. Used by FlatRedBallService.</summary>
    internal IReadOnlyList<GumRenderable> GumRenderables => _gumRenderables;

    internal void SetGumRenderableLayer(GraphicalUiElement visual, Layer? layer)
    {
        if (_gumByVisual.TryGetValue(visual, out var renderable))
            renderable.Layer = layer;
    }

    // Pause state
    public bool IsPaused { get; private set; }
    public void PauseThisScreen() => IsPaused = true;
    public void UnpauseThisScreen() => IsPaused = false;

    // Display settings

    /// <summary>
    /// Override to declare this screen's preferred display configuration.
    /// <para>
    /// Camera properties (<see cref="DisplaySettings.Zoom"/>, <see cref="DisplaySettings.ResizeMode"/>,
    /// <see cref="DisplaySettings.FixedAspectRatio"/>, etc.) are applied every time this screen activates,
    /// whether via <see cref="FlatRedBallService.Start{T}"/> or <see cref="MoveToScreen{T}"/>.
    /// </para>
    /// <para>
    /// Window properties (<see cref="DisplaySettings.PreferredWindowWidth"/>,
    /// <see cref="DisplaySettings.PreferredWindowHeight"/>, <see cref="DisplaySettings.AllowUserResizing"/>)
    /// are only applied when this screen is the <em>starting</em> screen. They are ignored during
    /// mid-game transitions so the window never pops or resizes while the player is playing.
    /// </para>
    /// <para>
    /// Return <c>null</c> (the default) to inherit the engine's default
    /// <see cref="FlatRedBallService.DisplaySettings"/> unchanged.
    /// </para>
    /// </summary>
    public virtual DisplaySettings? PreferredDisplaySettings => null;

    // Lifecycle
    public virtual void CustomInitialize() { }
    public virtual void CustomActivity(FrameTime time) { }
    public virtual void CustomDestroy() { }

    // Navigation

    /// <param name="configure">
    /// Optional callback invoked on the new screen instance before <see cref="CustomInitialize"/> runs.
    /// Use this to set public properties that <c>CustomInitialize</c> depends on.
    /// <para>
    /// <b>Avoid closing over mutable locals here.</b> The engine retains this callback to replay it
    /// on <see cref="RestartScreen"/>; because C# closures capture variables by reference, mutating
    /// a captured local after this call will change what restart sees. Pass values directly
    /// (<c>s =&gt; s.LevelIndex = 3</c>) rather than via captured locals.
    /// </para>
    /// </param>
    public void MoveToScreen<T>(Action<T>? configure = null) where T : Screen, new()
        => Engine.RequestScreenChange(configure);

    /// <summary>
    /// Requests a restart of the current screen at the start of the next frame. The screen is
    /// fully torn down (entities, factories, content, Gum, async tasks) and recreated as a fresh
    /// instance of the same type, replaying the original configure callback passed to
    /// <see cref="FlatRedBallService.Start{T}"/> or <see cref="MoveToScreen{T}"/>.
    /// <para>
    /// Use this for death/retry flows. Like <see cref="MoveToScreen{T}"/>, the transition is
    /// deferred — code after <c>RestartScreen()</c> in the same frame still runs.
    /// </para>
    /// <para>
    /// <b>Closure gotcha:</b> the original configure callback is replayed against its current
    /// closure environment, not a snapshot. If the configure captured a mutable local that has
    /// since changed, restart will see the new value. Avoid this by passing literals to configure
    /// (e.g. <c>s =&gt; s.LevelIndex = 3</c>), not captured locals.
    /// </para>
    /// </summary>
    public void RestartScreen() => Engine.RequestScreenRestart(null);

    // Collision relationship overloads
    /// <summary>
    /// Registers a collision relationship between two different collidable groups.
    /// Each entity in <paramref name="listA"/> is tested against each entity in <paramref name="listB"/> each frame.
    /// </summary>
    /// <remarks>
    /// Quick overload guide:
    /// <para>- Two groups: <c>AddCollisionRelationship&lt;A, B&gt;(listA, listB)</c></para>
    /// <para>- Self-collision: <c>AddCollisionRelationship&lt;A&gt;(list)</c></para>
    /// <para>- Tiles: <c>AddCollisionRelationship(entities, tiles)</c> (no explicit type args)</para>
    /// Common mistake: <c>AddCollisionRelationship&lt;Enemy&gt;(_enemies, _players)</c>.
    /// With one type argument, the compiler chooses the self-collision overload,
    /// so the second argument is invalid for that method.
    /// </remarks>
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

    public CollisionRelationship<A, TGeometry> AddCollisionRelationship<A, TGeometry>(
        IEnumerable<A> entities, TGeometry staticGeometry)
        where A : ICollidable
        where TGeometry : ICollidable
    {
        var rel = new CollisionRelationship<A, TGeometry>(entities, SingleEnumerable(staticGeometry));
        _collisionRelationships.Add(rel);
        return rel;
    }

    /// <summary>
    /// Registers a collision relationship between a group of entities and a
    /// <see cref="Collision.TileShapeCollection"/>. Type argument <typeparamref name="A"/> is
    /// inferred from <paramref name="entities"/>, so no explicit type arguments are needed:
    /// <code>AddCollisionRelationship(_playerFactory, _tiles).MoveFirstOnCollision();</code>
    /// </summary>
    public CollisionRelationship<A, Collision.TileShapeCollection> AddCollisionRelationship<A>(
        IEnumerable<A> entities, Collision.TileShapeCollection tiles)
        where A : ICollidable
    {
        var rel = new CollisionRelationship<A, Collision.TileShapeCollection>(entities, SingleEnumerable(tiles));
        _collisionRelationships.Add(rel);
        return rel;
    }

    // Internal update — called by FlatRedBallService
    internal void Update(FrameTime frameTime)
    {
        if (!IsPaused)
        {
            // 1. Physics pass
            foreach (var entity in _entities)
                entity.PhysicsUpdate(frameTime);

            Camera.PhysicsUpdate(frameTime.DeltaSeconds);

            // 1.5 Sort partitioned factories so broad-phase sweep uses up-to-date order.
            Engine?.SortPartitionedFactories();

            // 2. Collision phase
            foreach (var rel in _collisionRelationships)
                rel.RunCollisions();

            // 3. Entity CustomActivity — runs first (context-free; works regardless of screen)
            foreach (var entity in new List<Entity>(_entities))
                entity.CustomActivity(frameTime);

            // 4. Animate sprites
            double animDt = frameTime.DeltaSeconds;
            foreach (var renderable in _renderList)
                if (renderable is Sprite sprite)
                    sprite.AnimateSelf(animDt);
        }

        // 5. Screen CustomActivity — always runs so pause menu logic can respond to input
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

    internal void SortRenderList()
    {
        // Insertion sort — O(N) for nearly-sorted data; stable
        for (int i = 1; i < _renderList.Count; i++)
        {
            var item = _renderList[i];
            int j = i - 1;
            while (j >= 0 && Compare(_renderList[j], item) > 0)
            {
                _renderList[j + 1] = _renderList[j];
                j--;
            }
            _renderList[j + 1] = item;
        }
    }

    private int Compare(IRenderable a, IRenderable b)
    {
        int layerA = a.Layer != null ? Layers.IndexOf(a.Layer) : -1;
        int layerB = b.Layer != null ? Layers.IndexOf(b.Layer) : -1;
        if (layerA != layerB) return layerA.CompareTo(layerB);

        int zCmp = a.Z.CompareTo(b.Z);
        if (zCmp != 0) return zCmp;

        if (SortMode == Rendering.SortMode.ZSecondaryParentY)
        {
            // Higher world Y = further away = drawn first (behind).
            // Lower world Y = closer to viewer = drawn last (in front).
            float parentYA = GetParentY(a);
            float parentYB = GetParentY(b);
            return parentYB.CompareTo(parentYA); // descending
        }

        return 0;
    }

    private static float GetParentY(IRenderable renderable) =>
        renderable is IAttachable a ? (a.Parent?.AbsoluteY ?? a.AbsoluteY) : 0f;

    private static IEnumerable<T> SingleEnumerable<T>(T item)
    {
        yield return item;
    }
}
