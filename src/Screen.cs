using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.Xna.Framework.Graphics;
using FlatRedBall2.Collision;
using FlatRedBall2.Content;
using FlatRedBall2.Diagnostics;
using FlatRedBall2.Tiled;
using FlatRedBall2.UI;
using FlatRedBall2.Rendering;
using Gum.Forms.Controls;
using Gum.Wireframe;

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

    /// <summary>The camera that defines this screen's view into the world. Modify position/zoom each frame; the engine applies the transform during <see cref="FlatRedBallService.Draw"/>.</summary>
    public Camera Camera { get; } = new Camera();
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
    public Screen() => Overlay = new Overlay(this);

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

    // Tween list — advanced each frame, cleared on screen teardown.
    internal readonly Tweening.TweenList _tweens = new();

    /// <summary>
    /// Controls whether this screen's tweens (and its entities' tweens) advance this frame.
    /// Default <c>true</c>. Override for screen-wide tween pausing independent of
    /// <see cref="IsPaused"/> — e.g., freeze tweens during a cinematic while gameplay still runs.
    /// </summary>
    protected virtual bool ShouldAdvanceTweens => true;

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

    /// <summary>
    /// Override to set up game logic, entities, and factories. Always headless-safe — no
    /// graphics device is required. Called before <see cref="LoadContent"/>.
    /// <para>
    /// Put here: creature/entity state, <c>Factory&lt;T&gt;</c> construction, initial positions,
    /// game-mode flags, anything that works without a GPU.
    /// </para>
    /// </summary>
    public virtual void Initialize() { }

    /// <summary>
    /// Override to set up renderer-dependent resources — Gum UI, textures, layers, fonts.
    /// Requires a graphics device. Called after <see cref="Initialize"/>.
    /// <para>
    /// Put here: <c>Layer</c>, <c>Camera.BackgroundColor</c>, Gum controls, HP bars, labels,
    /// buttons. Anything that would throw in a headless test belongs here instead of
    /// <see cref="Initialize"/>.
    /// </para>
    /// </summary>
    public virtual void LoadContent() { }

    /// <summary>
    /// Convenience lifecycle hook called by the engine on screen activation. The default
    /// implementation calls <see cref="Initialize"/> then <see cref="LoadContent"/> in order.
    /// <para>
    /// Prefer overriding <see cref="Initialize"/> and <see cref="LoadContent"/> individually
    /// so that headless tests can call <see cref="Initialize"/> without a graphics device.
    /// Override <c>CustomInitialize</c> directly only if you genuinely need both in one place
    /// and don't care about headless testability.
    /// </para>
    /// </summary>
    public virtual void CustomInitialize()
    {
        Initialize();
        LoadContent();
    }

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

            Camera.PhysicsUpdate(frameTime.DeltaSeconds);

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
            if (renderable is IAttachable attachable && attachable.Parent != null
                && !attachable.Parent.IsAbsoluteVisible)
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
