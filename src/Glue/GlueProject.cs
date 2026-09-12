using System;
using System.Collections.Generic;
using System.Linq;
using FlatRedBall2.Glue.Model;

namespace FlatRedBall2.Glue;

/// <summary>
/// A loaded Glue project: every element addressable by its Glue name, the assets they reference, and
/// the entry points for creating screens and entities from those names.
/// </summary>
/// <remarks>
/// This is the context nothing had before it. A <see cref="GlueScreen"/> holds only its own
/// <c>ScreenSave</c>, so on its own it cannot resolve a nested entity, follow a screen transition, or
/// find an asset — every one of those needs to see the whole project.
/// <para>Element names are Glue's own, backslash-separated (<c>Entities\Player</c>). Lookups accept
/// either separator and ignore case, because these names are typed by hand with no compiler to check
/// them.</para>
/// </remarks>
public sealed class GlueProject
{
    private readonly Dictionary<string, ScreenSave> _screens = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, EntitySave> _entities = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<GlueEntity>> _instances = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<GlueEntity>> _listInstances = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> _factoryListsByEntityType = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<GlueEntity>> _pool = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<GlueLoadDiagnostic> _diagnostics = new();

    private GlueProject(GlueLoadResult result, GlueContentSource? content)
    {
        Result = result;
        Content = content;
        _diagnostics.AddRange(result.Diagnostics);

        foreach (var screen in result.Project.Screens.Where(s => !string.IsNullOrEmpty(s.Name)))
            _screens[screen.Name!] = screen;

        foreach (var entity in result.Project.Entities.Where(e => !string.IsNullOrEmpty(e.Name)))
            _entities[entity.Name!] = entity;

        content?.LoadGlobalFiles(result.Project.GlobalFiles, _diagnostics);

        foreach (var screen in result.Project.Screens)
            IndexFactoryLists(screen.NamedObjects);

        foreach (var entity in result.Project.Entities)
            IndexFactoryLists(entity.NamedObjects);
    }

    /// <summary>
    /// Indexes every list whose author opted it into factory spawning
    /// (<see cref="Model.NamedObjectSave.AssociateWithFactory"/>), keyed by the entity type it holds.
    /// </summary>
    /// <remarks>
    /// Top-level only, matching where Glue actually declares lists — the same scope
    /// <see cref="GlueElementBuilder"/> uses. A type can have any number of associated lists at once
    /// (G82): a generic spawn (<see cref="CreateEntity(string, Screen, string?)"/> with no explicit
    /// list) joins every one of them, mirroring FRB1's <c>ListsToAddTo</c>.
    /// </remarks>
    private void IndexFactoryLists(List<NamedObjectSave> namedObjects)
    {
        foreach (var save in namedObjects)
        {
            if (!save.IsList || !save.AssociateWithFactory ||
                string.IsNullOrEmpty(save.SourceClassGenericType) || string.IsNullOrEmpty(save.InstanceName))
            {
                continue;
            }

            if (!_factoryListsByEntityType.TryGetValue(save.SourceClassGenericType, out var listNames))
                _factoryListsByEntityType[save.SourceClassGenericType] = listNames = new List<string>();

            listNames.Add(save.InstanceName);
        }
    }

    /// <summary>The raw load result.</summary>
    public GlueLoadResult Result { get; }

    /// <summary>
    /// Everything the load reported, plus anything reported since — applying display settings, for
    /// instance, happens after the load returns.
    /// </summary>
    public IReadOnlyList<GlueLoadDiagnostic> Diagnostics => _diagnostics;

    /// <summary>Where referenced assets come from, when one was supplied.</summary>
    public GlueContentSource? Content { get; }

    /// <summary>The screen the project starts on, or null if it names none that resolved.</summary>
    public ScreenSave? StartUpScreen => Result.StartUpScreen;

    /// <summary>Loads a project from a <c>.gluj</c> path, optionally with a content source.</summary>
    public static GlueProject Load(
        string glujPath, GlueContentSource? content = null, GlueLoadOptions? options = null) =>
        new(GlueProjectLoader.Load(glujPath, options), content);

    /// <summary>
    /// Applies the project's display block to <paramref name="target"/>, usually
    /// <c>FlatRedBallService.Default.DisplaySettings</c>.
    /// </summary>
    /// <remarks>
    /// Call this before starting the first screen — FRB2 applies window properties only on
    /// <c>Start</c>. Glue's display block is project-global, so once is enough.
    /// </remarks>
    public void ApplyDisplaySettings(Rendering.DisplaySettings target)
    {
        var source = Result.Project.DisplaySettings;

        if (source is null)
        {
            _diagnostics.Add(new GlueLoadDiagnostic(
                GlueDiagnosticSeverity.Warning,
                "This project has no DisplaySettings block, so its resolution and window setup " +
                "could not be applied.",
                null));
            return;
        }

        GlueDisplayMapper.Apply(source, target, _diagnostics);
    }

    /// <summary>The screen with this Glue name, or null.</summary>
    public ScreenSave? FindScreen(string glueName) =>
        _screens.TryGetValue(Normalize(glueName), out var screen) ? screen : null;

    /// <summary>The entity with this Glue name, or null.</summary>
    public EntitySave? FindEntity(string glueName) =>
        _entities.TryGetValue(Normalize(glueName), out var entity) ? entity : null;

    /// <summary>Every live instance created from this entity name.</summary>
    /// <remarks>
    /// Stands in for FRB1's per-entity factory list, and is what a collision relationship binds to.
    /// Pooling and spatial partitioning are <em>not</em> wired — see the Phase 8 notes.
    /// <para>Returns the actual backing <c>List&lt;GlueEntity&gt;</c>, creating and caching an empty
    /// one on first call for a name with no instances yet. This is load-bearing for a relationship
    /// registered before any instance of that type exists (e.g. an enemy type spawned purely by a
    /// runtime factory): the caller must get back the same live list every time, not a throwaway
    /// empty array, or an instance created later has nothing to be added to.</para>
    /// </remarks>
    public IReadOnlyList<GlueEntity> InstancesOf(string glueName)
    {
        string normalized = Normalize(glueName);

        if (!_instances.TryGetValue(normalized, out var list))
            _instances[normalized] = list = new List<GlueEntity>();

        return list;
    }

    /// <summary>Every live instance tracked under the named Glue list <paramref name="listName"/>.</summary>
    /// <remarks>
    /// Distinct from <see cref="InstancesOf"/>, which is keyed by entity type and so cannot tell two
    /// differently-named lists of the same type apart. This is keyed by the list's own Glue instance
    /// name instead — the identity a collision relationship actually binds to — so
    /// <c>WaveOneEnemies</c> and <c>WaveTwoEnemies</c>, both <c>Entities\Enemy</c>, stay separate
    /// collections even though they hold the same type.
    /// <para>An entity can be live in more than one named list at once: one it was declared a member
    /// of, and/or any list of its type that opted into
    /// <see cref="Model.NamedObjectSave.AssociateWithFactory"/> — see
    /// <see cref="CreateEntity(string, Screen, string?)"/>. Same lazy-create-and-cache behavior as
    /// <see cref="InstancesOf"/>: a relationship can bind to a list name before anything has been
    /// spawned into it.</para>
    /// </remarks>
    public IReadOnlyList<GlueEntity> InstancesOfList(string listName)
    {
        if (!_listInstances.TryGetValue(listName, out var list))
            _listInstances[listName] = list = new List<GlueEntity>();

        return list;
    }

    /// <summary>
    /// The screen <paramref name="screen"/> names as the one to advance to, or null when it names
    /// none — Glue's own idiom for level progression.
    /// </summary>
    public ScreenSave? NextScreenOf(ScreenSave screen) =>
        string.IsNullOrEmpty(screen.NextScreen) ? null : FindScreen(screen.NextScreen!);

    /// <summary>
    /// Builds a screen from its Glue name, ready to hand to the engine's screen machinery.
    /// </summary>
    /// <exception cref="ArgumentException">No element has that name.</exception>
    /// <exception cref="InvalidOperationException">The element is abstract.</exception>
    public GlueScreen CreateScreen(string glueName)
    {
        var save = FindScreen(glueName)
            ?? throw UnknownName(glueName, "screen", _screens.Keys);

        if (save.IsAbstract)
        {
            throw new InvalidOperationException(
                $"'{save.Name}' leaves an object for a derived screen to supply, so it is missing " +
                "its own content by construction and cannot be shown on its own.");
        }

        return new GlueScreen { Save = save, Project = this };
    }

    /// <summary>
    /// Creates an entity from its Glue name and registers it on <paramref name="screen"/>.
    /// </summary>
    /// <remarks>
    /// The entity's own objects are built before it is returned, so the caller sees a finished
    /// entity. Registration order matches the engine's: the screen owns it, and destroying it
    /// removes it from this project's instance list.
    /// </remarks>
    /// <param name="glueName">The entity's Glue name, e.g. <c>Entities\Enemy</c>.</param>
    /// <param name="screen">The screen to register the entity on.</param>
    /// <param name="listName">
    /// The one Glue list this instance is already known to be a declared member of — used when
    /// building a list's own authored member, whose placement is unambiguous. Leave this null for an
    /// ordinary spawn (the call game code makes at runtime, mirroring FRB1's
    /// <c>&lt;Entity&gt;Factory.CreateNew()</c>): the instance then joins every list of this entity
    /// type that opted into <see cref="Model.NamedObjectSave.AssociateWithFactory"/> — none, one, or
    /// several. Either way the entity is always tracked under <see cref="InstancesOf"/> by type. See
    /// <see cref="InstancesOfList"/>.
    /// </param>
    /// <exception cref="ArgumentException">No element has that name.</exception>
    /// <exception cref="InvalidOperationException">The element is abstract.</exception>
    public GlueEntity CreateEntity(string glueName, Screen screen, string? listName = null)
    {
        var save = FindEntity(glueName)
            ?? throw UnknownName(glueName, "entity", _entities.Keys);

        return CreateEntity(save, screen, listName);
    }

    /// <summary>
    /// Creates an entity from a save this project already holds, skipping the name lookup.
    /// </summary>
    /// <remarks>
    /// For callers already iterating the project's elements — looking the name back up would repeat
    /// work, and would fail outright for a save whose name no longer matches its dictionary key.
    /// </remarks>
    internal GlueEntity CreateEntity(EntitySave save, Screen screen, string? listName = null)
    {
        if (save.IsAbstract)
        {
            throw new InvalidOperationException(
                $"'{save.Name}' leaves an object for a derived entity to supply, so it cannot be " +
                "created on its own.");
        }

        var entity = TakeFromPool(save) ?? new GlueEntity { Save = save, Project = this };

        screen.Register(entity);

        // Rebuilt even for a recycled instance: BuildObjects clears what the previous life left
        // behind, so a reused shell is indistinguishable from a fresh one.
        entity.BuildObjects();

        BindInput(entity, save, screen);

        // Destroy is the only signal that an instance is gone. Without this the instance list keeps
        // handing out corpses, and a collision relationship built on it collides with them forever.
        entity._onDestroy = () => Release(entity);

        Track(save.Name!, entity);
        JoinLists(save, entity, listName);

        return entity;
    }

    /// <summary>
    /// Adds a newly created entity to the named list(s) it belongs to.
    /// </summary>
    /// <remarks>
    /// A list's own declared member always names its list explicitly (threaded down from
    /// <see cref="GlueElementBuilder"/>), so its placement is never inferred here. An ordinary spawn
    /// gives no list — that is the signal to fall back to
    /// <see cref="Model.NamedObjectSave.AssociateWithFactory"/> and join every associated list of this
    /// type, which may be none, one, or several (G82).
    /// </remarks>
    private void JoinLists(EntitySave save, GlueEntity entity, string? listName)
    {
        if (listName is not null)
        {
            TrackInList(listName, entity);
            return;
        }

        if (save.Name is not null && _factoryListsByEntityType.TryGetValue(save.Name, out var listNames))
        {
            foreach (var name in listNames)
                TrackInList(name, entity);
        }
    }

    /// <summary>
    /// A recycled instance for <paramref name="save"/>, or null when it is not pooled or none is
    /// free.
    /// </summary>
    /// <remarks>
    /// Pools are per Glue name, not per CLR type. Every loaded entity is a <see cref="GlueEntity"/>,
    /// so a single shared pool would hand back a <c>Door</c> where a <c>Player</c> was asked for —
    /// the hazard G80 describes. Only the shell is reused; its contents are rebuilt.
    /// </remarks>
    private GlueEntity? TakeFromPool(EntitySave save)
    {
        if (!save.PooledByFactory || save.Name is null)
            return null;

        if (!_pool.TryGetValue(save.Name, out var free) || free.Count == 0)
            return null;

        var recycled = free[free.Count - 1];
        free.RemoveAt(free.Count - 1);
        return recycled;
    }

    /// <summary>
    /// Gives a created entity the input its <c>InputDevice</c> asks for.
    /// </summary>
    /// <remarks>
    /// Both behaviours get the same movement input — an entity is one or the other, and the unused
    /// behaviour is never updated. Skipped without an engine, since a test can build an entity with
    /// no input manager behind it.
    /// </remarks>
    private static void BindInput(GlueEntity entity, EntitySave save, Screen screen)
    {
        var input = screen.Engine?.Input;

        if (input is null)
            return;

        var bound = GlueInputBinder.Bind(save, input);

        if (bound.MovementInput is null)
            return;

        entity.Platformer.MovementInput = bound.MovementInput;
        entity.Platformer.JumpInput = bound.JumpInput;
        entity.TopDown.MovementInput = bound.MovementInput;
    }

    private void Track(string glueName, GlueEntity entity)
    {
        if (!_instances.TryGetValue(glueName, out var list))
            _instances[glueName] = list = new List<GlueEntity>();

        list.Add(entity);
    }

    private void TrackInList(string listName, GlueEntity entity)
    {
        if (!_listInstances.TryGetValue(listName, out var list))
            _listInstances[listName] = list = new List<GlueEntity>();

        list.Add(entity);
    }

    /// <summary>
    /// Drops a destroyed instance from the live list, and keeps its shell if the element is pooled.
    /// </summary>
    private void Release(GlueEntity entity)
    {
        Forget(entity);

        if (entity.GlueName is null || entity.Save?.PooledByFactory != true)
            return;

        if (!_pool.TryGetValue(entity.GlueName, out var free))
            _pool[entity.GlueName] = free = new List<GlueEntity>();

        // Guarded because Destroy on an already-destroyed entity would otherwise pool it twice and
        // hand the same instance to two callers.
        if (!free.Contains(entity))
            free.Add(entity);
    }

    /// <summary>Forgets a destroyed instance, so a relationship does not keep collecting corpses.</summary>
    /// <remarks>
    /// Removed from every named list, not just its type-wide one — an entity can be live in several
    /// lists at once (G82) and nothing records which, so this checks them all. Lists per project are
    /// few, so the scan costs nothing worth avoiding.
    /// </remarks>
    internal void Forget(GlueEntity entity)
    {
        if (entity.GlueName is not null && _instances.TryGetValue(entity.GlueName, out var list))
            list.Remove(entity);

        foreach (var namedList in _listInstances.Values)
            namedList.Remove(entity);
    }

    /// <summary>
    /// Glue writes element names with a backslash. Accepting a forward slash costs one replace and
    /// removes the likeliest thing a hand-typed name gets wrong.
    /// </summary>
    private static string Normalize(string glueName) => glueName.Replace('/', '\\');

    /// <summary>The same "no screen named that" error <see cref="CreateScreen"/> raises.</summary>
    /// <remarks>
    /// Exposed so <see cref="Screen.MoveToScreen(string, Action{GlueScreen})"/> can fail at the call
    /// site with the identical message, rather than a frame later inside the deferred change.
    /// </remarks>
    internal static ArgumentException UnknownScreenName(GlueProject project, string requested) =>
        UnknownName(requested, "screen", project._screens.Keys);

    private static ArgumentException UnknownName(
        string requested, string kind, IEnumerable<string> known)
    {
        // Shown plain rather than C#-escaped: the reader is matching this against their project,
        // not pasting it into a string literal.
        string examples = string.Join(", ", known.Take(3).Select(k => $"'{k}'"));

        return new ArgumentException(
            $"No {kind} named '{requested}'. Names are Glue's own and include the folder — " +
            $"for example {examples}.");
    }
}
