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
    private readonly Dictionary<string, List<GlueEntity>> _pool = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<GlueEntityList>> _listsToAddTo = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<GlueLoadDiagnostic> _diagnostics = new();
    private readonly HashSet<string> _reportedRegistrations = new(StringComparer.OrdinalIgnoreCase);

    private GlueProject(GlueLoadResult result, GlueContentSource? content, GlueEntityRegistry? entities)
    {
        Result = result;
        Content = content;
        Entities = entities ?? new GlueEntityRegistry();
        _diagnostics.AddRange(result.Diagnostics);

        foreach (var screen in result.Project.Screens.Where(s => !string.IsNullOrEmpty(s.Name)))
            _screens[screen.Name!] = screen;

        foreach (var entity in result.Project.Entities.Where(e => !string.IsNullOrEmpty(e.Name)))
            _entities[entity.Name!] = entity;

        content?.LoadGlobalFiles(result.Project.GlobalFiles, _diagnostics);
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

    /// <summary>
    /// The classes registered as code-behind for this project's entities. Shared with the engine
    /// that loaded the project (<see cref="FlatRedBallService.GlueEntities"/>); a project loaded on
    /// its own has a registry of its own.
    /// </summary>
    public GlueEntityRegistry Entities { get; }

    /// <summary>Loads a project from a <c>.gluj</c> path, optionally with a content source.</summary>
    /// <param name="glujPath">The project file.</param>
    /// <param name="content">Where referenced assets come from.</param>
    /// <param name="options">How the file is read.</param>
    /// <param name="entities">
    /// The registry the project creates entities from. Omit it for a registry of the project's own;
    /// the engine passes its own so registrations outlive a reload.
    /// </param>
    public static GlueProject Load(
        string glujPath, GlueContentSource? content = null, GlueLoadOptions? options = null,
        GlueEntityRegistry? entities = null) =>
        new(GlueProjectLoader.Load(glujPath, options), content, entities);

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
    /// <para>This is the call game code makes at runtime, mirroring FRB1's
    /// <c>&lt;Entity&gt;Factory.CreateNew()</c>: the instance joins every list in
    /// <see cref="ListsToAddTo"/> for its entity type. By default those are the lists the current
    /// screen declared with <see cref="Model.NamedObjectSave.AssociateWithFactory"/>, as in FRB1 where
    /// the screen's init calls <c>AddList</c>; an entity's own lists are never associated by default.
    /// Change the set with <see cref="AddList"/> and <see cref="RemoveList"/>. Either way the entity is
    /// tracked under <see cref="InstancesOf"/> by type.</para>
    /// </remarks>
    /// <param name="glueName">The entity's Glue name, e.g. <c>Entities\Enemy</c>.</param>
    /// <param name="screen">The screen to register the entity on.</param>
    /// <exception cref="ArgumentException">No element has that name.</exception>
    /// <exception cref="InvalidOperationException">The element is abstract.</exception>
    public GlueEntity CreateEntity(string glueName, Screen screen) =>
        CreateEntity(glueName, screen, joinFactoryLists: true);

    /// <summary>
    /// Creates an entity that joins <see cref="ListsToAddTo"/> only when
    /// <paramref name="joinFactoryLists"/> is set. An authored list member passes false: it is placed
    /// in its one list by the builder.
    /// </summary>
    internal GlueEntity CreateEntity(string glueName, Screen screen, bool joinFactoryLists)
    {
        var save = FindEntity(glueName)
            ?? throw UnknownName(glueName, "entity", _entities.Keys);

        return CreateEntity(save, screen, joinFactoryLists);
    }

    /// <summary>
    /// Creates an entity from a save this project already holds, skipping the name lookup.
    /// </summary>
    /// <remarks>
    /// For callers already iterating the project's elements — looking the name back up would repeat
    /// work, and would fail outright for a save whose name no longer matches its dictionary key.
    /// </remarks>
    internal GlueEntity CreateEntity(
        EntitySave save, Screen screen, bool joinFactoryLists = true, Action<GlueEntity>? configure = null)
    {
        if (save.IsAbstract)
        {
            throw new InvalidOperationException(
                $"'{save.Name}' leaves an object for a derived entity to supply, so it cannot be " +
                "created on its own.");
        }

        var entity = TakeFromPool(save);
        bool recycled = entity is not null;
        entity ??= Instantiate(save);

        if (recycled && entity.HasCodeBehind)
        {
            // Code-behind ran CustomInitialize once, on this instance's first life, and may hold
            // references into what that built. So the objects are kept and put back to their
            // authored values, as a hand-written pooled entity is, rather than rebuilt.
            screen.AddEntity(entity);
            entity.AttachRenderablesToScreen();
            entity.ResetEngineState();
            entity.ResetToAuthored();
        }
        else
        {
            screen.Register(entity);

            // Rebuilt even for a recycled instance: BuildObjects clears what the previous life left
            // behind, so a reused shell is indistinguishable from a fresh one.
            entity.BuildObjects();

            // A pooled entity with code-behind is kept whole on destroy; see above.
            entity._isPooled = save.PooledByFactory && entity.HasCodeBehind;
        }

        BindInput(entity, save, screen);

        // Destroy is the only signal that an instance is gone. Without this the instance list keeps
        // handing out corpses, and a collision relationship built on it collides with them forever.
        // It replaces the hook Register installed, so it has to do that hook's job too.
        entity._onDestroy = () =>
        {
            screen.RemoveEntity(entity);

            if (entity._isPooled)
                entity.DetachRenderablesFromScreen();

            Release(entity);
        };

        // Before CustomInitialize / Reset, as Factory<T>.Create(configure) is.
        configure?.Invoke(entity);

        // The lifecycle a hand-written entity has: CustomInitialize on an instance's first life,
        // Reset() when a pooled instance is reused. Both see the Glue data already applied.
        if (!recycled)
            entity.CustomInitialize();
        else if (entity.HasCodeBehind)
            entity.InvokeResetForPool();

        Track(save.Name!, entity);

        if (joinFactoryLists && _listsToAddTo.TryGetValue(save.Name!, out var destinations))
        {
            foreach (var list in destinations)
                list.Add(entity);
        }

        return entity;
    }

    /// <summary>
    /// A new instance of the class registered for this entity, or for the nearest ancestor that has
    /// one, falling back to the shared <see cref="GlueEntity"/> type.
    /// </summary>
    private GlueEntity Instantiate(EntitySave save)
    {
        var entity = SelfAndAncestors(save)
            .Select(name => Entities.TryCreate(name))
            .FirstOrDefault(created => created is not null) ?? new GlueEntity();

        entity.Save = save;
        entity.Project = this;
        return entity;
    }

    /// <summary>
    /// Reports each registered class whose Glue name no entity in this project has, once. Registering
    /// is a plain call that can happen before the project is loaded, so the check waits until a screen
    /// is about to build; a typo would otherwise leave the entity quietly un-bound.
    /// </summary>
    internal void ReportUnknownRegistrations()
    {
        foreach (var name in Entities.RegisteredNames)
        {
            if (_entities.ContainsKey(name) || !_reportedRegistrations.Add(name))
                continue;

            _diagnostics.Add(new GlueLoadDiagnostic(
                GlueDiagnosticSeverity.Warning,
                $"A class is registered for '{name}', but this project has no entity with that name, " +
                "so it will never be created. Names are Glue's own and include the folder.",
                name));
        }
    }

    /// <summary>
    /// The lists a spawn of this entity joins: <see cref="CreateEntity(string, Screen)"/>'s
    /// destinations, FRB1's <c>ListsToAddTo</c>.
    /// </summary>
    /// <remarks>
    /// A loaded screen fills this at init with its <see cref="Model.NamedObjectSave.AssociateWithFactory"/>
    /// lists, and the set is cleared on every screen change.
    /// </remarks>
    /// <exception cref="ArgumentException">No entity has that name.</exception>
    public IReadOnlyList<GlueEntityList> ListsToAddTo(string glueName)
    {
        var save = FindEntity(glueName) ?? throw UnknownName(glueName, "entity", _entities.Keys);
        return _listsToAddTo.TryGetValue(save.Name!, out var lists) ? lists : Array.Empty<GlueEntityList>();
    }

    /// <summary>
    /// Makes every spawn of exactly this entity join <paramref name="list"/>, as FRB1's
    /// <c>Factory.AddList</c> does on one factory. Adding a list already there changes nothing.
    /// </summary>
    /// <remarks>
    /// Works for any list, a screen's or an entity's. It touches only the named entity's set: derived
    /// entities each keep their own, as FRB1's derived factories do, so a Zombie spawn is unaffected by
    /// a call naming its base Enemy. The registration lasts until the screen changes.
    /// </remarks>
    /// <exception cref="ArgumentException">No entity has that name.</exception>
    public void AddList(string glueName, GlueEntityList list)
    {
        var save = FindEntity(glueName) ?? throw UnknownName(glueName, "entity", _entities.Keys);
        AddDestination(save.Name!, list);
    }

    /// <summary>
    /// Stops spawns of exactly this entity joining <paramref name="list"/>, like FRB1's
    /// <c>Factory.RemoveList</c> on one factory. Members already in the list stay.
    /// </summary>
    /// <remarks>
    /// Derived entities are not touched, so removing a base-typed list from Enemy leaves Zombie spawns
    /// joining it; call it for each type to remove it everywhere.
    /// </remarks>
    /// <exception cref="ArgumentException">No entity has that name.</exception>
    public void RemoveList(string glueName, GlueEntityList list)
    {
        var save = FindEntity(glueName) ?? throw UnknownName(glueName, "entity", _entities.Keys);

        if (_listsToAddTo.TryGetValue(save.Name!, out var lists))
            lists.Remove(list);
    }

    private void AddDestination(string entityName, GlueEntityList list)
    {
        if (!_listsToAddTo.TryGetValue(entityName, out var lists))
            _listsToAddTo[entityName] = lists = new List<GlueEntityList>();

        if (!lists.Contains(list))
            lists.Add(list);
    }

    /// <summary>
    /// What a screen does at init for a list that opted into factory spawning: gives it to the
    /// factories the list is documented to reach, skipping abstract entities and ones with no factory.
    /// Returns the entities it registered the list for.
    /// </summary>
    /// <remarks>
    /// The rule follows FRB1's documentation, on purpose. A list always reaches its own entity's
    /// factory. It reaches the factories of derived entities, at any depth, only when its entity is the
    /// most base one (it has no base entity), so a list typed with an in-between entity does not reach
    /// the entities below it. FRB1's code generator instead reaches every descendant of any list type;
    /// that is treated as an FRB1 bug and is not copied.
    /// </remarks>
    internal List<string> AssociateList(string listEntityName, GlueEntityList list)
    {
        var listType = FindEntity(listEntityName);
        var registered = new List<string>();

        if (listType?.Name is null)
            return registered;

        bool reachesDescendants = listType.BaseEntity is null;

        foreach (var entity in _entities.Values)
        {
            if (entity.Name is null || !entity.CreatedByOtherEntities || entity.IsAbstract)
                continue;

            bool isListType = string.Equals(entity.Name, listType.Name, StringComparison.OrdinalIgnoreCase);

            if (!isListType &&
                !(reachesDescendants && SelfAndAncestors(entity).Contains(listType.Name, StringComparer.OrdinalIgnoreCase)))
            {
                continue;
            }

            AddDestination(entity.Name, list);
            registered.Add(entity.Name);
        }

        return registered;
    }

    /// <summary>The entity's own name, then each base's, nearest first. Stops at a cycle or a base that is not an element.</summary>
    private IEnumerable<string> SelfAndAncestors(EntitySave save)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var current = save; current?.Name is not null && seen.Add(current.Name);
             current = current.BaseEntity is null ? null : FindEntity(current.BaseEntity))
        {
            yield return current.Name;
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

    /// <summary>
    /// Drops a destroyed instance from the live list, and keeps its shell if the element is pooled.
    /// </summary>
    private void Release(GlueEntity entity)
    {
        Forget(entity);

        // FRB1's entity Destroy destroys what its lists hold: they are the instance's own.
        foreach (var member in entity.OwnedListMembers())
            member.Destroy();

        if (entity.GlueName is null || entity.Save?.PooledByFactory != true)
            return;

        if (!_pool.TryGetValue(entity.GlueName, out var free))
            _pool[entity.GlueName] = free = new List<GlueEntity>();

        // Guarded because Destroy on an already-destroyed entity would otherwise pool it twice and
        // hand the same instance to two callers.
        if (!free.Contains(entity))
            free.Add(entity);
    }

    /// <summary>
    /// Destroys every instance this project is tracking and empties the pools. Called on each screen
    /// change, where it does for loaded entities what destroying the factories does for
    /// <see cref="Factory{T}"/> ones. The loaded definitions are kept; only instances go.
    /// </summary>
    /// <remarks>
    /// Pools are emptied too: a recycled shell still holds the previous screen's shapes and
    /// registrations, and <see cref="Factory{T}"/> discards its own pool at the same point. The
    /// cached lists <see cref="InstancesOf"/> handed out are emptied in place, not replaced, so a
    /// holder of one sees the change. The spawn destinations (<see cref="ListsToAddTo"/>) are cleared
    /// too; the next screen registers its own. The lists themselves belong to the screen or entity that
    /// declared them, and their members leave as they are destroyed.
    /// </remarks>
    internal void ReleaseInstances()
    {
        var tracked = _instances.Values.SelectMany(l => l).Distinct().ToList();

        foreach (var entity in tracked)
        {
            // Destroying a parent destroys its children, which may already have been forgotten.
            if (entity.GlueName is not null && _instances.TryGetValue(entity.GlueName, out var list) &&
                list.Contains(entity))
            {
                entity.Destroy();
            }
        }

        foreach (var list in _instances.Values)
            list.Clear();

        _pool.Clear();
        _listsToAddTo.Clear();
    }

    /// <summary>Forgets a destroyed instance, so a relationship does not keep collecting corpses.</summary>
    /// <remarks>
    /// The entity removes itself from every list it joined; nothing here has to find them.
    /// </remarks>
    internal void Forget(GlueEntity entity)
    {
        if (entity.GlueName is not null && _instances.TryGetValue(entity.GlueName, out var list))
            list.Remove(entity);

        entity.LeaveLists();
    }

    /// <summary>
    /// Glue writes element names with a backslash. Accepting a forward slash costs one replace and
    /// removes the likeliest thing a hand-typed name gets wrong.
    /// </summary>
    internal static string Normalize(string glueName) => glueName.Replace('/', '\\');

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
