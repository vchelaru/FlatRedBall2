using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using Microsoft.Xna.Framework.Graphics;
using FlatRedBall2.Animation;
using FlatRedBall2.Collision;
using FlatRedBall2.Content;
using FlatRedBall2.Diagnostics;
using FlatRedBall2.Tiled;
using FlatRedBall2.UI;
using FlatRedBall2.Rendering;
using Gum.Forms.Controls;
using Gum.Wireframe;
using MonoGameGum.GueDeriving;

namespace FlatRedBall2;

/// <summary>
/// Base class for a game screen — a self-contained unit of game state with its own camera,
/// content, entities, collision relationships, and rendering pipeline. Override
/// <see cref="CustomInitialize"/>, <see cref="CustomActivity"/>, and <see cref="CustomDestroy"/>
/// to build a game screen; switch between screens with <see cref="MoveToScreen{T}"/>.
/// <para>
/// The engine owns a single <see cref="FlatRedBallService.CurrentScreen"/> at a time. On screen
/// transition, the outgoing screen's <see cref="Token"/> is cancelled, its content is unloaded,
/// and its entities are destroyed before the new screen's <see cref="CustomInitialize"/> runs.
/// </para>
/// </summary>
public class Screen
{
    private readonly List<Entity> _entities = new();
    private readonly List<ICollisionRelationship> _collisionRelationships = new();
    private readonly List<GumRenderable> _gumRenderables = new();

    /// <summary>All entities currently managed by this screen (registered via Factory or <see cref="Register"/>).</summary>
    public IReadOnlyList<Entity> Entities => _entities;

    internal readonly CancellationTokenSource _cts = new();

    /// <summary>
    /// A <see cref="CancellationToken"/> that is cancelled automatically when this screen is destroyed
    /// (i.e., when <see cref="MoveToScreen{T}"/> is called). Pass this token to
    /// <see cref="TimeManager.DelaySeconds"/>, <see cref="TimeManager.DelayUntil"/>, or any other
    /// async API to ensure tasks are silently cancelled on screen transition rather than running
    /// against the new screen.
    /// </summary>
    public CancellationToken Token => _cts.Token;

    /// <summary>
    /// All cameras on this screen, in draw order. The engine creates one camera by default;
    /// add more for split-screen / multi-viewport rendering by setting each new camera's
    /// <see cref="Camera.NormalizedViewport"/>. Each camera's transform is applied to a separate
    /// draw pass per frame. Cameras inserted into this list have their <c>Screen</c> back-reference
    /// set automatically so <see cref="Camera.Add(Gum.Wireframe.GraphicalUiElement, Layer?)"/> works
    /// without further wiring.
    /// </summary>
    public IList<Camera> Cameras { get; }

    /// <summary>The first camera on this screen — the default for single-camera games. Shortcut for <c>Cameras[0]</c>.</summary>
    public Camera Camera => Cameras[0];

    private sealed class CameraList : Collection<Camera>
    {
        private readonly Screen _screen;
        public CameraList(Screen screen) { _screen = screen; }
        protected override void InsertItem(int index, Camera item)
        {
            item.Screen = _screen;
            base.InsertItem(index, item);
        }
        protected override void SetItem(int index, Camera item)
        {
            item.Screen = _screen;
            base.SetItem(index, item);
        }
        protected override void RemoveItem(int index)
        {
            if (this[index].Screen == _screen) this[index].Screen = null;
            base.RemoveItem(index);
        }
    }
    /// <summary>This screen's content loader. Unloaded automatically on screen transition.</summary>
    public ContentManagerService ContentManager { get; } = new ContentManagerService();
    /// <summary>The engine that owns this screen. Injected before <see cref="CustomInitialize"/>.</summary>
    public FlatRedBallService Engine { get; internal set; } = null!;

    /// <summary>
    /// Immediate-mode visual overlay for this screen. Call draw methods each frame — objects
    /// appear for one frame and are hidden automatically the next. Resets on screen transition.
    /// </summary>
    public Overlay Overlay { get; }

    /// <summary>Constructs a new screen and its <see cref="Overlay"/>. Engine injection happens later, before <see cref="CustomInitialize"/>.</summary>
    public Screen()
    {
        Overlay = new Overlay(this);
        var cams = new CameraList(this);
        cams.Add(new Camera());
        Cameras = cams;
    }

    /// <summary>
    /// Custom rendering layers owned by this screen. Add to this list to introduce additional
    /// sort buckets (e.g. a parallax background, a HUD on top of gameplay) and pass the layer
    /// to <see cref="Add(IRenderable, Layer?)"/> when registering renderables.
    /// </summary>
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
    /// <summary>All renderables registered on this screen, in insertion order. The render pass sorts a copy by Layer/Z each frame.</summary>
    public IReadOnlyList<IRenderable> RenderList => _renderList;

    /// <summary>
    /// Registers <paramref name="renderable"/> for drawing. Pass an explicit
    /// <paramref name="layer"/> to override the screen's default <see cref="Layer"/>.
    /// </summary>
    public void Add(IRenderable renderable, Layer? layer = null)
    {
        if (layer != null || Layer != null)
            renderable.Layer = layer ?? Layer;
        _renderList.Add(renderable);
    }

    /// <summary>Unregisters <paramref name="renderable"/> from drawing. Idempotent.</summary>
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
    /// <para><b>Add renderable children before calling <c>Register</c></b> if you want them on this
    /// screen's render list. <c>Register</c> walks the entity's existing children and adds any
    /// <c>IRenderable</c>s to this screen. After registration, <c>entity.Add(...)</c> routes new
    /// renderables through <c>entity.Engine.CurrentScreen</c> instead — which may not be this screen
    /// during initialization (e.g. when registering before <c>Engine.CurrentScreen</c> is set).</para>
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
    /// Adds all visual layers of a <see cref="TileMap"/> to this screen's render list and
    /// registers the map's <see cref="TileMap.LazySpawnManager"/> for per-frame ticking
    /// against this screen's camera. Lazy-spawn placements registered before or after this
    /// call will fire when the camera reaches them.
    /// Individual layer Z values and visibility are respected.
    /// </summary>
    public void Add(TileMap map, Layer? layer = null)
    {
        foreach (var mapLayer in map.Layers)
            Add(mapLayer, layer);
        if (!_lazySpawnSources.Contains(map))
            _lazySpawnSources.Add(map);
    }

    /// <summary>
    /// Adds a single <see cref="TileMapLayer"/> to this screen's render list.
    /// Use this instead of <see cref="Add(TileMap, Layer?)"/> when you need per-layer control
    /// (e.g., assigning different layers to different FRB rendering layers).
    /// </summary>
    public void Add(TileMapLayer mapLayer, Layer? layer = null)
    {
        if (mapLayer.Renderable == null) return;
        mapLayer.Renderable.Layer = layer ?? Layer;
        _renderList.Add(mapLayer.Renderable);
    }

    private readonly List<TileMap> _lazySpawnSources = new();

    /// <summary>
    /// Adds a Gum Forms control to this screen's primary camera HUD. Equivalent to
    /// <c>Cameras[0].Add(element, layer)</c>; for split-screen, call <see cref="Camera.Add(FrameworkElement, Layer?)"/>
    /// directly on the desired camera.
    /// </summary>
    public void Add(FrameworkElement element, Layer? layer = null)
        => Cameras[0].Add(element, layer ?? Layer);

    /// <summary>
    /// Adds a Gum visual element to this screen's primary camera HUD. Equivalent to
    /// <c>Cameras[0].Add(visual, layer)</c>. For split-screen, call <see cref="Camera.Add(GraphicalUiElement, Layer?)"/>
    /// directly on the desired camera; for full-window UI shared across all cameras, use
    /// <see cref="AddOverlay(GraphicalUiElement, Layer?)"/>.
    /// </summary>
    public void Add(GraphicalUiElement visual, Layer? layer = null)
        => Cameras[0].Add(visual, layer ?? Layer);

    /// <summary>Removes a Gum element previously added with <see cref="Add(FrameworkElement, Layer?)"/>.</summary>
    public void Remove(FrameworkElement element)
        => Cameras[0].Remove(element);

    /// <summary>Removes a Gum visual previously added with <see cref="Add(GraphicalUiElement, Layer?)"/>.</summary>
    public void Remove(GraphicalUiElement visual)
        => Cameras[0].Remove(visual);

    // ---------- Overlay (full-window, shared across cameras) ----------

    private GraphicalUiElement? _overlayRoot;
    private float _lastOverlayCanvasWidth = float.NaN;
    private float _lastOverlayCanvasHeight = float.NaN;

    /// <summary>
    /// Lazily-created Gum root for the screen-level overlay. Visuals added via
    /// <see cref="AddOverlay(GraphicalUiElement, Layer?)"/> are parented here and laid out
    /// against the full back-buffer dimensions, drawn in a single pass after the per-camera loop.
    /// Use this for pause menus, title cards, and other UI that should not be split per viewport.
    /// </summary>
    public GraphicalUiElement OverlayRoot => _overlayRoot ??= new ContainerRuntime();

    /// <summary>Test-only counter incremented every time the overlay's canvas dims change and a layout pass runs.</summary>
    internal int OverlayLayoutCallCount { get; private set; }

    /// <summary>
    /// Calls <c>UpdateLayout</c> on <see cref="OverlayRoot"/> only when the supplied canvas dims
    /// differ from those used on the previous call. Same gating discipline as
    /// <see cref="Camera.EnsureHudLayout"/>.
    /// </summary>
    internal void EnsureOverlayLayout(float canvasWidth, float canvasHeight)
    {
        if (canvasWidth == _lastOverlayCanvasWidth && canvasHeight == _lastOverlayCanvasHeight)
            return;
        _lastOverlayCanvasWidth = canvasWidth;
        _lastOverlayCanvasHeight = canvasHeight;
        OverlayRoot.UpdateLayout();
        OverlayLayoutCallCount++;
    }

    /// <summary>
    /// Adds a Gum visual to the screen-level overlay layer, drawn full-window after every
    /// camera's draw pass. Use for pause menus, dialog boxes, and other UI that must span
    /// the entire window in split-screen.
    /// </summary>
    public void AddOverlay(GraphicalUiElement visual, Layer? layer = null)
    {
        OverlayRoot.Children.Add(visual);
        var renderable = new GumRenderable(visual) { Layer = layer ?? Layer, IsOverlay = true };
        _gumRenderables.Add(renderable);
        _gumByVisual[visual] = renderable;
        _renderList.Add(renderable);
    }

    /// <summary>Adds a Gum Forms control to the screen-level overlay layer. See <see cref="AddOverlay(GraphicalUiElement, Layer?)"/>.</summary>
    public void AddOverlay(FrameworkElement element, Layer? layer = null)
        => AddOverlay(element.Visual, layer);

    /// <summary>Removes a visual previously added with <see cref="AddOverlay(GraphicalUiElement, Layer?)"/>.</summary>
    public void RemoveOverlay(GraphicalUiElement visual)
    {
        OverlayRoot.Children.Remove(visual);
        RemoveGumVisualInternal(visual);
    }

    /// <summary>Removes a Forms control previously added with <see cref="AddOverlay(FrameworkElement, Layer?)"/>.</summary>
    public void RemoveOverlay(FrameworkElement element) => RemoveOverlay(element.Visual);

    internal void AddGumForEntity(GraphicalUiElement visual, Entity worldParent, Layer? layer)
    {
        var renderable = new GumRenderable(visual) { Parent = worldParent, Layer = layer };
        _gumRenderables.Add(renderable);
        _gumByVisual[visual] = renderable;
        _renderList.Add(renderable);
    }

    internal void AddGumForCamera(GraphicalUiElement visual, Camera owningCamera, Layer? layer)
    {
        var renderable = new GumRenderable(visual) { OwningCamera = owningCamera, Layer = layer ?? Layer };
        _gumRenderables.Add(renderable);
        _gumByVisual[visual] = renderable;
        _renderList.Add(renderable);
    }

    internal void RemoveGumForCamera(GraphicalUiElement visual) => RemoveGumVisualInternal(visual);

    private void RemoveGumVisualInternal(GraphicalUiElement visual)
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

    // Tween list — advanced each frame, cleared on screen teardown.
    internal readonly Tweening.TweenList _tweens = new();

    /// <summary>
    /// Controls whether this screen's tweens (and its entities' tweens) advance this frame.
    /// Default <c>true</c>. Override for screen-wide tween pausing independent of
    /// <see cref="IsPaused"/> — e.g., freeze tweens during a cinematic while gameplay still runs.
    /// </summary>
    protected virtual bool ShouldAdvanceTweens => true;

    /// <summary>
    /// Cancels every tween currently owned by this screen. Each tween's value is left frozen
    /// wherever its curve was last sampled — no terminal snap to the target value, no
    /// <c>Ended</c> invocation. Matches the no-setter-fired semantics of <see cref="FlatRedBall.Glue.StateInterpolation.Tweener.Stop"/>
    /// and the auto-cleanup that runs during screen teardown. Does not touch tweens owned by
    /// individual entities — call <see cref="Entity.StopAllTweens"/> on those. Safe to call
    /// when no tweens are active.
    /// </summary>
    public void StopAllTweens() => _tweens.Clear();

    // Pause state

    /// <summary>
    /// Whether this screen is currently paused. While <c>true</c>, entity physics, entity
    /// <see cref="Entity.CustomActivity"/>, and collision processing are all suspended.
    /// <see cref="CustomActivity"/>, Gum UI, and input continue to run normally.
    /// </summary>
    /// <seealso cref="PauseThisScreen"/>
    /// <seealso cref="UnpauseThisScreen"/>
    public bool IsPaused { get; private set; }

    /// <summary>
    /// Freezes entity physics, entity <see cref="Entity.CustomActivity"/>, and collision
    /// processing. <see cref="CustomActivity"/>, Gum UI, and input remain active so
    /// pause-menu logic can still respond to player input.
    /// </summary>
    /// <seealso cref="UnpauseThisScreen"/>
    /// <seealso cref="IsPaused"/>
    public void PauseThisScreen() => IsPaused = true;

    /// <summary>
    /// Resumes a paused screen, re-enabling entity physics, entity
    /// <see cref="Entity.CustomActivity"/>, and collision processing.
    /// </summary>
    /// <seealso cref="PauseThisScreen"/>
    /// <seealso cref="IsPaused"/>
    public void UnpauseThisScreen() => IsPaused = false;

    // Display settings

    /// <summary>
    /// Override to declare this screen's preferred display configuration.
    /// <para>
    /// Camera properties (<see cref="DisplaySettings.ResizeMode"/>,
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

    /// <summary>
    /// Override to initialize the screen — create entities, set up factories, configure the camera,
    /// load content. Called by the engine when the screen activates.
    /// Do <b>not</b> call <c>base.CustomInitialize()</c>; the base is empty.
    /// </summary>
    public virtual void CustomInitialize() { }

    /// <summary>
    /// Override to run per-frame screen logic. Called after entity activity, collision, and tween
    /// advancement have completed for this frame. Skipped while <see cref="IsPaused"/> is <c>true</c>.
    /// </summary>
    public virtual void CustomActivity(FrameTime time) { }

    /// <summary>
    /// Override to release screen-specific resources before the screen tears down. Runs before
    /// entities and content are destroyed — engine subsystems are still valid here.
    /// </summary>
    public virtual void CustomDestroy() { }

    // Navigation

    /// <summary>
    /// Requests a transition to screen <typeparamref name="T"/> at the start of the next frame.
    /// All entities, collision relationships, Gum UI, and async tasks from the current screen
    /// are destroyed automatically.
    /// </summary>
    /// <param name="configure">
    /// Optional callback invoked on the new screen instance before <see cref="CustomInitialize"/> runs.
    /// Use this to set public properties that <c>CustomInitialize</c> depends on.
    /// <para>
    /// <b>Avoid closing over mutable locals here.</b> The engine retains this callback to replay it
    /// on <see cref="RestartScreen()"/>; because C# closures capture variables by reference, mutating
    /// a captured local after this call will change what restart sees. Pass values directly
    /// (<c>s =&gt; s.LevelIndex = 3</c>) rather than via captured locals.
    /// </para>
    /// <para>
    /// To return data from a sub-screen back to its parent, pass the result through
    /// <paramref name="configure"/> on the return transition:
    /// <c>MoveToScreen&lt;ParentScreen&gt;(s =&gt; s.ReturnedResult = result)</c>.
    /// The parent's <see cref="CustomInitialize"/> then reads the property before building the world.
    /// </para>
    /// <para>
    /// <b>No push/pop screen stack by design.</b> FlatRedBall2 intentionally uses full-screen
    /// transitions only — there is no "freeze parent, activate sub-screen, pop back" stack. The
    /// lifecycle and subsystem-interaction cost of a frozen-parent state (collision, tweens,
    /// timing, content hot-reload all needing to respect it) is not justified by the use cases:
    /// pause menus and HUD overlays belong in Gum as UI layered over the active screen, and
    /// battle / shop / dialog hand-offs fit the <c>MoveToScreen</c> + return-via-configure pattern
    /// above. For full-teardown cases where the parent's type is re-entered fresh (and the return
    /// configure can't be set because the caller doesn't retain a parent reference), store the
    /// payload in a static field on the destination screen and clear it in <c>CustomInitialize</c>.
    /// </para>
    /// </param>
    public void MoveToScreen<T>(Action<T>? configure = null) where T : Screen, new()
        => Engine.RequestScreenChange(configure);

    /// <summary>
    /// Requests a restart of the current screen at the start of the next frame. The screen is
    /// fully torn down (entities, factories, content, Gum, async tasks) and recreated as a fresh
    /// instance of the same type, replaying the most recently retained configure callback.
    /// <para>
    /// The engine retains a single configure slot per session. <see cref="FlatRedBallService.Start{T}"/>
    /// and <see cref="MoveToScreen{T}"/> set it; the typed extension overload of this method
    /// (<c>screen.RestartScreen(s =&gt; s.X = 7)</c>) replaces it.
    /// </para>
    /// <para>
    /// Use this for death/retry flows. Like <see cref="MoveToScreen{T}"/>, the transition is
    /// deferred — code after <c>RestartScreen()</c> in the same frame still runs.
    /// </para>
    /// <para>
    /// <b>Closure gotcha:</b> the retained callback is replayed against its current closure
    /// environment, not a snapshot. If the callback captured a mutable local that has since
    /// changed, restart will see the new value. Prefer literals to captured locals.
    /// </para>
    /// </summary>
    public void RestartScreen() => Engine.RequestScreenRestart(null, RestartMode.DeathRetry);

    /// <summary>
    /// Restarts the current screen using the specified <paramref name="mode"/>. Pass
    /// <see cref="RestartMode.HotReload"/> to opt into the Save/Restore hook pipeline that
    /// preserves session state (score, position, etc.) across a content-change-driven restart.
    /// </summary>
    public void RestartScreen(RestartMode mode) => Engine.RequestScreenRestart(null, mode);

    /// <summary>
    /// Hot-reload restart hook. Called on the OLD screen instance before teardown, while live
    /// game state is still intact. Stuff anything you want preserved (score, timer, collected
    /// items) into <paramref name="state"/>. The matching <see cref="RestoreHotReloadState"/>
    /// runs on the NEW instance after <c>CustomInitialize</c>.
    /// <para>
    /// Only invoked when restart was requested with <see cref="RestartMode.HotReload"/>. Plain
    /// death/retry restarts never call this — by design, so retry can't accidentally preserve
    /// stale state across a death.
    /// </para>
    /// </summary>
    public virtual void SaveHotReloadState(HotReloadState state) { }

    /// <summary>
    /// Hot-reload restart hook. Called on the NEW screen instance after <c>CustomInitialize</c>
    /// has built the fresh world. Read values back out of <paramref name="state"/> and apply
    /// them — these overwrite whatever the configure callback / <c>CustomInitialize</c> set.
    /// <para>
    /// Restore runs after <c>CustomInitialize</c> intentionally: <c>CustomInitialize</c> spawns
    /// the level from scratch, then restore patches saved values on top. The reverse order
    /// would let <c>CustomInitialize</c> clobber whatever restore set.
    /// </para>
    /// </summary>
    public virtual void RestoreHotReloadState(HotReloadState state) { }

    // Content watching

    private readonly List<ContentWatcher> _contentWatchers = new();
    private readonly List<ContentDirectoryWatcher> _contentDirectoryWatchers = new();

    /// <summary>All <see cref="ContentWatcher"/>s registered against this screen.</summary>
    public IReadOnlyList<ContentWatcher> ContentWatchers => _contentWatchers;

    /// <summary>All <see cref="ContentDirectoryWatcher"/>s registered against this screen.</summary>
    public IReadOnlyList<ContentDirectoryWatcher> ContentDirectoryWatchers => _contentDirectoryWatchers;

    /// <summary>
    /// Watches a single content file for changes. Resolves <paramref name="sourcePath"/> against
    /// <see cref="FlatRedBallService.SourceContentRoot"/> (so the user-edited source file is the
    /// one being watched, not the build-output copy), copies the changed source to the build
    /// output before invoking <paramref name="onChanged"/>, and invokes the callback on the game
    /// thread once writes settle.
    /// <para>
    /// If <see cref="FlatRedBallService.SourceContentRoot"/> is <c>null</c> (typically a shipping
    /// build with no <c>.csproj</c> next to the executable), this method returns <c>null</c> and
    /// no watcher is registered — hot-reload is a dev-only convenience.
    /// </para>
    /// <para>
    /// <paramref name="destinationPath"/> defaults to <paramref name="sourcePath"/>. Override when
    /// your build pipeline maps the source to a different runtime path
    /// (e.g. <c>WatchContent("Assets/player.json", ..., "Content/player.json")</c>).
    /// </para>
    /// <para>
    /// For an explicit registration result, call <see cref="TryWatchContent"/>.
    /// </para>
    /// </summary>
    public ContentWatcher? WatchContent(string sourcePath, Action onChanged, string? destinationPath = null)
    {
        TryWatchContent(sourcePath, onChanged, out var watcher, destinationPath);
        return watcher;
    }

    /// <summary>
    /// Attempts to watch a single content file and returns a registration status.
    /// Unlike <see cref="WatchContent(string, Action, string?)"/>, this method lets callers
    /// distinguish "watcher intentionally unavailable in shipping builds" from successful
    /// registration without relying on null checks alone.
    /// </summary>
    public ContentWatchRegistrationStatus TryWatchContent(
        string sourcePath,
        Action onChanged,
        out ContentWatcher? watcher,
        string? destinationPath = null)
    {
        if (Engine.SourceContentRoot == null)
        {
            watcher = null;
            return ContentWatchRegistrationStatus.SourceContentRootUnavailable;
        }

        var srcAbs = Path.Combine(Engine.SourceContentRoot, sourcePath);
        var destAbs = Path.Combine(Engine.OutputContentRoot, destinationPath ?? sourcePath);
        watcher = WatchContent(new FileSystemFileWatcher(srcAbs), onChanged,
            sourceAbsolutePath: srcAbs, destinationAbsolutePath: destAbs);
        return ContentWatchRegistrationStatus.Registered;
    }

    /// <summary>
    /// Watches an injected <see cref="IFileWatcher"/> source. Lower-level overload primarily for
    /// tests and custom file event sources. <paramref name="sourceAbsolutePath"/> /
    /// <paramref name="destinationAbsolutePath"/> are optional; when both are supplied, the
    /// engine copies source → destination before invoking the callback.
    /// </summary>
    public ContentWatcher WatchContent(IFileWatcher source, Action onChanged,
        string? sourceAbsolutePath = null, string? destinationAbsolutePath = null)
    {
        Func<bool>? copy = null;
        if (sourceAbsolutePath != null && destinationAbsolutePath != null)
            copy = () => CopyFileIfNeeded(sourceAbsolutePath, destinationAbsolutePath);
        var watcher = new ContentWatcher(source, onChanged, copy);
        _contentWatchers.Add(watcher);
        return watcher;
    }

    /// <summary>
    /// Watches a directory tree for changes. The callback fires once per changed file (after a
    /// global debounce — wait until all writes settle), with the file's path relative to
    /// <paramref name="sourceDirectory"/>. The engine copies each changed file to the matching
    /// path under the build output before invoking the callback.
    /// <para>
    /// Returns <c>null</c> when <see cref="FlatRedBallService.SourceContentRoot"/> is unset
    /// (shipping build).
    /// </para>
    /// <para>
    /// For an explicit registration result, call <see cref="TryWatchContentDirectory"/>.
    /// </para>
    /// </summary>
    public ContentDirectoryWatcher? WatchContentDirectory(string sourceDirectory, Action<string> onChanged,
        string? destinationDirectory = null)
    {
        TryWatchContentDirectory(sourceDirectory, onChanged, out var watcher, destinationDirectory);
        return watcher;
    }

    /// <summary>
    /// Attempts to watch a content directory tree and returns a registration status.
    /// Unlike <see cref="WatchContentDirectory(string, Action{string}, string?)"/>, this method
    /// reports when registration is intentionally unavailable because source content paths do
    /// not exist in the current runtime environment.
    /// </summary>
    public ContentWatchRegistrationStatus TryWatchContentDirectory(
        string sourceDirectory,
        Action<string> onChanged,
        out ContentDirectoryWatcher? watcher,
        string? destinationDirectory = null)
    {
        if (Engine.SourceContentRoot == null)
        {
            watcher = null;
            return ContentWatchRegistrationStatus.SourceContentRootUnavailable;
        }

        var srcAbs = Path.Combine(Engine.SourceContentRoot, sourceDirectory);
        var destAbs = Path.Combine(Engine.OutputContentRoot, destinationDirectory ?? sourceDirectory);
        watcher = WatchContentDirectory(new FileSystemDirectoryWatcher(srcAbs), onChanged,
            sourceAbsoluteRoot: srcAbs, destinationAbsoluteRoot: destAbs);
        return ContentWatchRegistrationStatus.Registered;
    }

    /// <summary>
    /// Watches an injected <see cref="IDirectoryWatcher"/> source. Lower-level overload for tests
    /// and custom directory event sources. When <paramref name="sourceAbsoluteRoot"/> /
    /// <paramref name="destinationAbsoluteRoot"/> are both supplied, the engine copies each
    /// changed file before invoking the callback.
    /// </summary>
    public ContentDirectoryWatcher WatchContentDirectory(IDirectoryWatcher source, Action<string> onChanged,
        string? sourceAbsoluteRoot = null, string? destinationAbsoluteRoot = null)
    {
        ContentDirectoryWatcher? watcher = null;
        Func<string, bool> copy;
        if (sourceAbsoluteRoot != null && destinationAbsoluteRoot != null)
            copy = relPath => CopyFileIfNeeded(
                Path.Combine(sourceAbsoluteRoot, relPath),
                Path.Combine(destinationAbsoluteRoot, relPath),
                watcher!.AutoCopyExtensions);
        else
            copy = _ => true;
        watcher = new ContentDirectoryWatcher(source, onChanged, copy);
        // Default auto-reload policy: PNG edits patch the live Texture2D in-place via
        // Engine.Content.TryReload before onChanged fires. No-op when the texture isn't
        // registered. Set watcher.AutoReloadAction = null to opt out.
        if (destinationAbsoluteRoot != null)
            watcher.AutoReloadAction = relPath =>
                Engine.Content.TryReload(Path.Combine(destinationAbsoluteRoot, relPath));
        _contentDirectoryWatchers.Add(watcher);
        return watcher;
    }

    /// <returns>
    /// <c>false</c> when the source is missing (deletion) OR the destination doesn't exist yet
    /// AND the extension isn't in <paramref name="autoCopyExtensions"/>. The dest-exists gate
    /// filters out editor temp files (Photoshop scratch files, IDE autosaves, lock files) that
    /// appear in the source folder but were never copied to the build output; the allowlist
    /// reopens the gate for known-safe asset types that can legitimately appear as new files
    /// (e.g. a PNG a TMX now references).
    /// </returns>
    private static bool CopyFileIfNeeded(string src, string dest, HashSet<string>? autoCopyExtensions = null)
    {
        // Same path → nothing to copy. Avoids the IOException File.Copy throws on self-copy.
        if (string.Equals(Path.GetFullPath(src), Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase))
            return File.Exists(dest);
        if (!File.Exists(src)) return false;
        if (!File.Exists(dest))
        {
            if (autoCopyExtensions == null || !autoCopyExtensions.Contains(Path.GetExtension(src)))
                return false;
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        }
        File.Copy(src, dest, overwrite: true);
        return true;
    }

    internal void TickContentWatchers(DateTime now)
    {
        // Foreach over count: callbacks may dispose / register watchers.
        for (int i = 0; i < _contentWatchers.Count; i++)
            _contentWatchers[i].Tick(now);
        for (int i = 0; i < _contentDirectoryWatchers.Count; i++)
            _contentDirectoryWatchers[i].Tick(now);
    }

    internal void DisposeContentWatchers()
    {
        foreach (var w in _contentWatchers) w.Dispose();
        _contentWatchers.Clear();
        foreach (var w in _contentDirectoryWatchers) w.Dispose();
        _contentDirectoryWatchers.Clear();
    }

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
    /// <summary>
    /// Registers a collision relationship between a single entity and a group of entities.
    /// </summary>
    public CollisionRelationship<A, B> AddCollisionRelationship<A, B>(
        IEnumerable<A> listA, IEnumerable<B> listB)
        where A : ICollidable
        where B : ICollidable
    {
        var rel = new CollisionRelationship<A, B>(listA, listB);
        _collisionRelationships.Add(rel);
        return rel;
    }

    /// <summary>
    /// Registers a collision relationship between a single entity and a group of entities.
    /// </summary>
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

    /// <summary>
    /// Registers a collision relationship between a group of entities and static geometry.
    /// </summary>
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

            for (int i = 0; i < Cameras.Count; i++)
                Cameras[i].PhysicsUpdate(frameTime.DeltaSeconds);

            // 1.25 Lazy-spawn: tick each enrolled tilemap against the current camera rect so
            //      placements that just scrolled into view spawn their entities BEFORE the
            //      partition sort and collision pass — entities are visible to broad-phase on
            //      the same frame they spawn.
            // TODO (split-screen): only Cameras[0] is consulted; multi-camera union is deferred
            //      (see design/TODOS.md "Split-Screen Support").
            for (int i = 0; i < _lazySpawnSources.Count; i++)
                _lazySpawnSources[i].LazySpawnManager.Update(
                    Camera.Left, Camera.Right, Camera.Bottom, Camera.Top);

            // 1.5 Sort partitioned factories so broad-phase sweep uses up-to-date order.
            Engine?.SortPartitionedFactories();

            // 2. Collision phase
            foreach (var rel in _collisionRelationships)
                rel.RunCollisions();

            // Loops 2.5, 3, 4 fire user callbacks (tween Ended, CustomActivity, AnimationFinished)
            // that may Destroy entities — mutating _entities and _renderList. Reverse-for with a
            // bounds check tolerates mutation without allocating. Forward foreach would throw;
            // snapshot-via-new-List is forbidden here (per-frame hotpath — see engine-tdd skill).

            // 2.5 Tween advancement — entity tweens before CustomActivity so setter-driven
            //     state is visible to user code; screen tweens just before screen CustomActivity.
            if (ShouldAdvanceTweens)
            {
                float dt = frameTime.DeltaSeconds;
                for (int i = _entities.Count - 1; i >= 0; i--)
                {
                    if (i >= _entities.Count) continue;
                    var entity = _entities[i];
                    if (entity.ShouldAdvanceTweens)
                        entity._tweens.Update(dt);
                }
                _tweens.Update(dt);
            }

            // 3. Entity CustomActivity — runs first (context-free; works regardless of screen)
            for (int i = _entities.Count - 1; i >= 0; i--)
            {
                if (i >= _entities.Count) continue;
                _entities[i].CustomActivity(frameTime);
            }

            // 4. Animate sprites
            double animDt = frameTime.DeltaSeconds;
            for (int i = _renderList.Count - 1; i >= 0; i--)
            {
                if (i >= _renderList.Count) continue;
                if (_renderList[i] is Sprite sprite)
                    sprite.AnimateSelf(animDt);
            }

            // 4.5 Fire-and-forget texture-overload lifetimes — tick down and destroy when expired.
            // Reverse iteration so removals don't shift unscanned indices.
            for (int i = _fireAndForgetLifetimes.Count - 1; i >= 0; i--)
            {
                var (entity, remaining) = _fireAndForgetLifetimes[i];
                remaining -= frameTime.DeltaSeconds;
                if (remaining <= 0f)
                {
                    _fireAndForgetLifetimes.RemoveAt(i);
                    entity.Destroy();
                }
                else
                {
                    _fireAndForgetLifetimes[i] = (entity, remaining);
                }
            }
        }

        // 5. Screen CustomActivity — always runs so pause menu logic can respond to input
        CustomActivity(frameTime);
    }

    // Internal draw — called by FlatRedBallService once per camera in Screen.Cameras. The engine
    // has already cleared the back buffer (LetterboxColor or BackgroundColor) and painted the
    // active camera's BackgroundColor inside its viewport, so do not call GraphicsDevice.Clear
    // here — it would ignore the viewport on MonoGame's OpenGL backend and wipe out gutters.
    internal void Draw(SpriteBatch spriteBatch, RenderDiagnostics diagnostics, Camera activeCamera)
    {
        SortRenderList();

        IRenderBatch? currentBatch = null;
        IRenderable? previousRenderable = null;

        foreach (var renderable in RenderList)
        {
            if (renderable is IAttachable attachable && attachable.Parent != null
                && !attachable.Parent.IsAbsoluteVisible)
                continue;

            // Per-camera HUD ownership: a GumRenderable bound to one camera (or marked as overlay)
            // must not be drawn under any other camera's transform. Overlay renderables are drawn
            // in a separate post-camera pass by FlatRedBallService.
            if (renderable is GumRenderable gum && !gum.ShouldDrawForCamera(activeCamera))
                continue;

            var batch = renderable.Batch;
            if (batch != currentBatch)
            {
                if (diagnostics.IsEnabled && currentBatch != null)
                {
                    diagnostics.RecordBreak(currentBatch, batch, renderable.Layer, renderable.Z,
                        previousRenderable?.Name ?? string.Empty, renderable.Name ?? string.Empty);
                }
                currentBatch?.End(spriteBatch);
                batch.Begin(spriteBatch, activeCamera);
                currentBatch = batch;
            }

            renderable.Draw(spriteBatch, activeCamera);
            previousRenderable = renderable;
        }

        currentBatch?.End(spriteBatch);
    }

    // Internal overlay draw — invoked once per frame by FlatRedBallService AFTER the per-camera
    // loop. Walks _gumRenderables (not _renderList) since overlays are independent of layer/Z
    // sort against world-space content; their order among themselves matches insertion order.
    internal void DrawOverlay(SpriteBatch spriteBatch, RenderDiagnostics diagnostics, Camera primaryCamera)
    {
        IRenderBatch? currentBatch = null;
        foreach (var renderable in _gumRenderables)
        {
            if (!renderable.IsOverlay) continue;
            var batch = renderable.Batch;
            if (batch != currentBatch)
            {
                currentBatch?.End(spriteBatch);
                batch.Begin(spriteBatch, primaryCamera);
                currentBatch = batch;
            }
            renderable.Draw(spriteBatch, primaryCamera);
        }
        currentBatch?.End(spriteBatch);
    }

    // Fire-and-forget timed lifetimes — tracked alongside _entities and ticked in Update.
    // Parallel-list (rather than a per-entity field) so the texture-overload helper can attach
    // a duration without touching the Entity API surface or burdening every entity with a timer.
    private readonly List<(Entity entity, float remaining)> _fireAndForgetLifetimes = new();

    /// <summary>
    /// Spawns a one-shot, self-destroying entity that plays <paramref name="animationName"/> from
    /// <paramref name="animations"/> at (<paramref name="x"/>, <paramref name="y"/>) and destroys
    /// itself when the animation finishes. Returned <see cref="Entity"/> is fully wired into the
    /// screen — set <c>Velocity</c>, attach to a parent, or <c>Add</c> shapes for collision before
    /// the next frame.
    /// <para>
    /// <b>Plays once</b> regardless of the animation chain's loop authoring — the helper forces
    /// <see cref="Sprite.IsLooping"/> to <c>false</c> on the spawned sprite. If you want a looping
    /// effect with timed cleanup, author a real entity instead.
    /// </para>
    /// </summary>
    public Entity CreateFireAndForget(AnimationChainList animations, string animationName, float x, float y)
    {
        var entity = new Entity { X = x, Y = y };
        // Add sprite as a child BEFORE Register so Register's child-walk routes the sprite into
        // THIS screen's render list. Calling entity.Add(sprite) after Register would route through
        // entity.Engine.CurrentScreen instead, which may not be this screen during initialization.
        var sprite = new Sprite { AnimationChains = animations, IsLooping = false };
        entity.Add(sprite);
        Register(entity);
        sprite.PlayAnimation(animationName);
        sprite.AnimationFinished += () => entity.Destroy();
        return entity;
    }

    /// <summary>
    /// Spawns a one-shot, self-destroying entity that displays <paramref name="texture"/> at
    /// (<paramref name="x"/>, <paramref name="y"/>) for <paramref name="duration"/> seconds and
    /// then destroys itself. Returned <see cref="Entity"/> is fully wired into the screen — set
    /// <c>Velocity</c>, attach to a parent, or <c>Add</c> shapes for collision before the next frame.
    /// </summary>
    public Entity CreateFireAndForget(Texture2D texture, float x, float y, float duration)
    {
        var entity = new Entity { X = x, Y = y };
        var sprite = new Sprite { Texture = texture };
        entity.Add(sprite);
        Register(entity);

        _fireAndForgetLifetimes.Add((entity, duration));
        return entity;
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
